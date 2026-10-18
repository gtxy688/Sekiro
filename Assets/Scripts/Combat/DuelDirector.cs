using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 编排处决与投技的双方对齐、状态锁定和结束结算，同时只允许一场成对演出。
    // 当前演出统一保存在 PairedPerformance 中，复战时解除双方锁定并清除。
    public class DuelDirector : MonoBehaviour, ICombatResettable
    {
        // 一次成对演出。处决与投技共用同一个槽位，因为它们本来就不可能同时发生。
        private class PairedPerformance
        {
            public CharacterBody Initiator;   // 处决发起者 / 投技攻击者
            public CharacterBody Victim;      // 被处决者 / 被投者
            public bool Resolved;             // 处决：已清命；投技：双方都已到点
            public bool InitiatorDone;        // 投技用：发起方 Clip 播完
            public bool VictimDone;           // 投技用：受方 Clip 播完
        }

        [Header("处决")]
        [Tooltip("处决触发距离（Ground 忍杀才卡距离；弹反/识破确认窗口里双方已贴身，不卡）")]
        public float finisherRange = 2f;

        private PairedPerformance active;

        // 外部注入的主对手；查询忍杀目标时优先检查它，再遍历 Scope 中的其他对手。
        private CharacterBody bossRef;

        // 注册进本场战斗的重置清单：复战时 EncounterScope.ResetAll() 才能丢掉残留演出。
        private void Start()
        {
            EncounterScope.Ensure().Register(this);
        }

        private void OnDestroy()
        {
            if (EncounterScope.Current != null)
                EncounterScope.Current.Unregister(this);
        }

        // 注入本场对手。复战换 Boss / 连战切场时重新调一次即可，不用动编排逻辑。
        public void Configure(CharacterBody boss)
        {
            bossRef = boss;
        }

        public void ResetForEncounter()
        {
            if (active == null) return;

            // 复战重开时若演出还没走完，必须解锁双方，否则参与者会永久卡在 IsFinisherLocked。
            if (active.Initiator != null) active.Initiator.IsFinisherLocked = false;
            if (active.Victim != null) active.Victim.IsFinisherLocked = false;
            active = null;
        }

        // 当前仅玩家阵营能发起处决，资格不绑定某个固定对象引用。
        private static bool CanInitiateFinisher(CharacterBody initiator)
        {
            return HitReactionUtil.IsPlayer(initiator);
        }

        // 供输入层在按键按下当帧判断：只有此刻真的能开演，才生成 FinisherCommand。
        // 不能用 TryExecuteAvailableFinisher 探测，它会直接切状态并消耗机会。
        public bool HasAvailableFinisher(CharacterBody initiator)
        {
            if (initiator == null || !CanInitiateFinisher(initiator)) return false;
            if (initiator.IsPostureBroken || active != null) return false;

            CharacterBody victim = FindAnyBrokenOpponent(initiator);
            if (victim == null) return false;

            return CanExecuteFinisher(
                initiator,
                victim,
                FinisherKindFor(victim.CurrentPostureBreakSource));
        }

        // ===== 成对忍杀=====
        public bool TryExecuteFinisher(CharacterBody initiator, FinisherKind kind = FinisherKind.Ground)
        {
            if (initiator == null) return false;
            if (!CanInitiateFinisher(initiator)) return false;
            if (initiator.IsPostureBroken) return false;
            if (active != null) return false;

            CharacterBody victim = FindFinisherVictim(initiator, kind);
            if (victim == null) return false;

            string playerAnim = ResolveFinisherAnim(kind, initiator.Animator);
            string bossAnim = ResolveFinisherAnim(kind, victim.Animator);
            if (string.IsNullOrEmpty(playerAnim) || string.IsNullOrEmpty(bossAnim))
            {
                Debug.LogError(
                    $"成对忍杀状态缺失：{kind}。请同时检查 {initiator.name} 与 {victim.name} 的 Animator（Mikiri/Miriki 拼写都算）。");
                return false;
            }

            // 弹反/识破确认窗口里双方已经贴身演完反制，不再用 Ground 处决的距离门卡住。
            if (kind == FinisherKind.Ground)
            {
                float dist = Vector3.Distance(
                    initiator.transform.position, victim.transform.position);
                if (dist > finisherRange) return false;
            }

            // 动画与距离校验通过后再对齐并锁定双方，避免失败请求残留演出状态。
            AlignFinisherFacing(initiator, victim, playerAnim, bossAnim);

            active = new PairedPerformance { Initiator = initiator, Victim = victim };

            initiator.ActiveAttack = null;
            initiator.MoveDirection = Vector3.zero;
            victim.MoveDirection = Vector3.zero;
            initiator.IsFinisherLocked = true;
            victim.IsFinisherLocked = true;
            initiator.KengekiArmed = false;
            victim.KengekiArmed = false;

            initiator.DisableWeaponHit();
            victim.DisableWeaponHit();
            CombatEventBus.TriggerFinisherOpportunityChanged(victim, false);

            // 双方分别进入配套叶子状态，结算和统一解锁由发起方动画结束驱动。
            victim.EnterGrounded(new FinisherVictimState(victim, bossAnim),
                "duel: finisher victim");
            initiator.EnterGrounded(new FinisherState(initiator, victim, playerAnim),
                "duel: finisher initiator");

            CombatEventBus.TriggerFinisherStarted(
                victim.transform.position, initiator, victim, kind);
            CombatEventBus.TriggerCameraShake(1f);
            return true;
        }

        // 按当前崩解来源选忍杀类型。连招窗口里再按攻击也走这里，优先于 NextCombo。
        public bool TryExecuteAvailableFinisher(CharacterBody initiator)
        {
            CharacterBody victim = FindAnyBrokenOpponent(initiator);
            if (victim == null) return false;

            return TryExecuteFinisher(
                initiator,
                FinisherKindFor(victim.CurrentPostureBreakSource));
        }

        // 取首个崩解对手，并检查其崩解来源是否匹配请求的忍杀类型。
        private CharacterBody FindFinisherVictim(CharacterBody initiator, FinisherKind kind)
        {
            CharacterBody candidate = FindAnyBrokenOpponent(initiator);
            if (candidate == null) return null;
            return MatchesBreakSource(kind, candidate.CurrentPostureBreakSource) ? candidate : null;
        }

        private CharacterBody FindAnyBrokenOpponent(CharacterBody initiator)
        {
            if (bossRef != null && bossRef != initiator && bossRef.IsPostureBroken)
                return bossRef;

            // 多对手时遍历 EncounterScope 的对手列表，主对手优先（上面的 bossRef 已处理）。
            EncounterScope scope = EncounterScope.Current;
            if (scope == null || scope.Opponents == null) return null;

            for (int i = 0; i < scope.Opponents.Count; i++)
            {
                CharacterBody opponent = scope.Opponents[i];
                if (opponent != null && opponent != initiator && opponent.IsPostureBroken)
                    return opponent;
            }
            return null;
        }

        private bool CanExecuteFinisher(
            CharacterBody initiator,
            CharacterBody victim,
            FinisherKind kind)
        {
            if (string.IsNullOrEmpty(ResolveFinisherAnim(kind, initiator.Animator)) ||
                string.IsNullOrEmpty(ResolveFinisherAnim(kind, victim.Animator)))
            {
                return false;
            }

            return kind != FinisherKind.Ground ||
                   Vector3.Distance(initiator.transform.position, victim.transform.position) <= finisherRange;
        }

        // 只转朝向、不瞬移。成对 Root 才能对上。双方 Clip 短名可以不同（Mikiri/Miriki）。
        private static void AlignFinisherFacing(
            CharacterBody player,
            CharacterBody boss,
            string playerAnim,
            string bossAnim)
        {
            if (player == null || boss == null) return;

            Vector3 toBoss = boss.transform.position - player.transform.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.0001f) return;

            player.SnapYaw(toBoss, playerAnim);
            boss.SnapYaw(-toBoss, bossAnim);
        }

        // 动画结束由 FinisherState 调用；若 Clip 仍残留事件也不会重复清命。
        public void ExecuteFinisher(CharacterBody source)
        {
            if (source == null || active == null || source != active.Initiator) return;
            if (active.Victim == null || active.Resolved) return;

            active.Resolved = true;
            active.Victim.ClearLife();
            CombatEventBus.TriggerFinisherOpportunityChanged(active.Victim, false);
        }

        public bool IsFinisherResolved(CharacterBody player)
        {
            return player != null && active != null &&
                   player == active.Initiator && active.Resolved;
        }

        // 清除演出记录并恢复存活双方的操作与地面状态，最终败北的受方保持锁定。
        public void CompleteFinisherSequence(CharacterBody player)
        {
            if (player == null || active == null || player != active.Initiator) return;

            CharacterBody victim = active.Victim;
            active = null;

            player.IsFinisherLocked = false;
            if (victim != null && victim.LivesRemaining > 0)
                victim.IsFinisherLocked = false;

            CombatEventBus.TriggerFinisherEnded(player, victim);

            if (victim != null)
            {
                victim.ClearCombatYawFrozen();
                victim.SetSuppressRootYaw(false);
            }

            if (victim != null && victim.LivesRemaining > 0)
                victim.EnterGrounded("duel: finisher sequence complete");

            player.EnterGrounded("duel: finisher sequence complete");
        }

        // ===== Elbow 投技 =====
        // 打中玩家后双方播 Elbow_Danger。不瞬移，只水平对视。
        public bool TryStartGrabThrow(CharacterBody attacker, CharacterBody victim)
        {
            if (attacker == null || victim == null || attacker == victim) return false;
            if (attacker.IsFinisherLocked || victim.IsFinisherLocked) return false;
            if (active != null) return false;
            if (!AnimUtil.HasState(attacker.Animator, GrabThrowState.AnimName)
                || !AnimUtil.HasState(victim.Animator, GrabThrowState.AnimName))
            {
                Debug.LogError($"投技缺少 {GrabThrowState.AnimName}：请检查 {attacker.name} 与 {victim.name} 的 Animator。");
                return false;
            }

            // 共用演出槽位，清除攻击残留后让双方进入投技动画。
            active = new PairedPerformance { Initiator = attacker, Victim = victim };

            attacker.DisableWeaponHit();
            victim.DisableWeaponHit();
            attacker.AttackUninterruptible = false;
            attacker.IsAttackRecoveryOpen = false;
            attacker.ActiveAttack = null;
            attacker.CurrentMoveEntry = null;
            attacker.CurrentMoveWindow = null;

            Vector3 toVictim = victim.transform.position - attacker.transform.position;
            toVictim.y = 0f;
            if (toVictim.sqrMagnitude > 0.0001f)
            {
                attacker.SnapYaw(toVictim, GrabThrowState.AnimName);
                victim.SnapYaw(-toVictim, GrabThrowState.AnimName);
            }

            attacker.IsFinisherLocked = true;
            victim.IsFinisherLocked = true;

            attacker.EnterGrounded(new GrabThrowState(attacker),
                "duel: grab throw attacker");
            victim.EnterGrounded(new GrabThrowState(victim),
                "duel: grab throw victim");

            CombatEventBus.TriggerCameraShake(0.45f);
            return true;
        }

        public void CompleteGrabThrow(CharacterBody source)
        {
            if (active == null || active.Resolved) return;
            if (source == null) return;

            if (source == active.Initiator)
                active.InitiatorDone = true;
            else if (source == active.Victim)
                active.VictimDone = true;
            else
                return;

            // 双方 Clip 长度可能不同，等两边都到点再一起回 Idle，避免短的一方把长的掐掉。
            if (!active.InitiatorDone || !active.VictimDone) return;

            active.Resolved = true;
            CharacterBody attacker = active.Initiator;
            CharacterBody victim = active.Victim;
            active = null;

            if (attacker != null)
            {
                attacker.IsFinisherLocked = false;
                attacker.EnterGrounded("duel: grab throw complete");
            }
            if (victim != null && victim.CurrentHP > 0)
            {
                victim.IsFinisherLocked = false;
                victim.EnterGrounded("duel: grab throw complete");
            }
        }

        private static FinisherKind FinisherKindFor(PostureBreakSource source)
        {
            switch (source)
            {
                case PostureBreakSource.Deflect:
                    return FinisherKind.Deflect;
                case PostureBreakSource.Mikiri:
                    return FinisherKind.Mikiri;
                default:
                    return FinisherKind.Ground;
            }
        }

        private static bool MatchesBreakSource(FinisherKind kind, PostureBreakSource source)
        {
            switch (kind)
            {
                case FinisherKind.Deflect:
                    return source == PostureBreakSource.Deflect;
                case FinisherKind.Mikiri:
                    return source == PostureBreakSource.Mikiri;
                default:
                    return source == PostureBreakSource.Attack;
            }
        }

        private static string ResolveFinisherAnim(FinisherKind kind, Animator animator)
        {
            switch (kind)
            {
                case FinisherKind.Deflect:
                    return AnimUtil.ResolveState(animator, "Finsher_Deflect");
                case FinisherKind.Mikiri:
                    return AnimUtil.ResolveState(animator, "Finsher_Mikiri", "Finsher_Miriki");
                default:
                    return AnimUtil.ResolveState(animator, "Finsher_Ground");
            }
        }
    }
}
