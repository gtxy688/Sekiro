using System.Collections;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Combat
{

    // 命中判定中间层（单例）：Hitbox 扫到 Hurtbox → 报告这里 → 统一查全局规则 → 调 target.ReceiveHit
    // 用户决策：方案 B（低耦合，全局规则集中一处），禁止 Hitbox 直接调 target.ReceiveHit
    public class CombatManager : MonoBehaviour
    {
        public static CombatManager Instance { get; private set; }

        [Header("拼刀参数")]
        [Tooltip("拼刀时双方架势增长系数（默认 1 = 按对方招式 PostureDamage 全额涨）")]
        public float clashPostureMultiplier = 1f;

        [Header("打击感")]
        public bool enableHitStop = true;   // 命中/弹反顿帧开关
        public float hitStopDuration = 0.05f;

        [Header("处决（M10）")]
        public float finisherRange = 2f;        // 处决触发距离
        public CharacterBody PlayerRef;         // 场景里拖玩家；唯一处决发起者
        public CharacterBody BossRef;           // 场景里拖 Boss（单 Boss 战）

        private CharacterBody activeFinisherPlayer;
        private CharacterBody activeFinisherVictim;
        private bool finisherResolved;
        private CharacterBody grabThrowAttacker;
        private CharacterBody grabThrowVictim;
        private bool grabThrowResolved;
        private bool grabThrowAttackerDone;
        private bool grabThrowVictimDone;
        private Coroutine hitStopRoutine;

        private void Awake()
        {
            // 单例防重
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            ValidateFinisherRefs();
        }

        private void Start()
        {
            // 身体胶囊同时承担 Hurtbox 扫描。互撞冲量会挤开站位，所以忽略 PhysX 互撞；
            // 玩家仍在 CharacterBody.LateUpdate 里做胶囊分离，不会穿过 Boss。
            IgnoreCharacterPhysics(PlayerRef, BossRef);

            // 擂台外圈空气墙：走位/垫步/击退都出不了场（坠落兜底仍在 CharacterBody）
            ArenaBoundary.Ensure();
        }

        private static void IgnoreCharacterPhysics(CharacterBody a, CharacterBody b)
        {
            if (a == null || b == null) return;

            Collider[] aCols = a.GetComponentsInChildren<Collider>(true);
            Collider[] bCols = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < aCols.Length; i++)
            {
                if (aCols[i] == null) continue;
                for (int j = 0; j < bCols.Length; j++)
                {
                    if (bCols[j] == null) continue;
                    Physics.IgnoreCollision(aCols[i], bCols[j], true);
                }
            }
        }

        private void ValidateFinisherRefs()
        {
            if (PlayerRef == null)
                Debug.LogError("CombatManager.PlayerRef 未绑定，处决无法发起。");
            if (BossRef == null)
                Debug.LogError("CombatManager.BossRef 未绑定，处决无法发起。");
            if (PlayerRef != null && PlayerRef == BossRef)
                Debug.LogError("CombatManager.PlayerRef 与 BossRef 指向同一角色，处决已禁用。");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // A 的武器扫到 B 的 Hurtbox
        public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
        {
            CharacterBody attacker = hitbox.Owner;
            CharacterBody target = hurtbox.Owner;

            // 1. 排除打到自己（双保险，Hitbox 侧已过滤）
            if (attacker == null || target == null || attacker == target) return;

            // 2. 全局规则扩展位（后续：减伤 Buff、全场无敌、友军伤害开关等）

            // 3. 伤害数据来自 AttackConfig（SO），这里只做转发（含危字标记 M17 / 击退 / 受击等级）
            if (hitbox.Config == null) return;
            target.ReceiveHit(attacker, hitbox.Config.BaseDamage, hitbox.Config.PostureDamage, hitPoint,
                              hitbox.Config.Perilous != PerilousType.None, hitbox.Config.Perilous,
                              hitbox.Config.Knockback, hitbox.Config.HitGrade, false);

            // 命中顿帧（打击感）
            HitStop();
        }

        // 箭扫到 Hurtbox。伤害由调用方从招式表解析，不读 Hitbox.Config。
        public void ReportProjectileHit(
            CharacterBody attacker,
            Hurtbox hurtbox,
            Vector3 hitPoint,
            int healthDmg,
            float postureDmg,
            float knockback,
            HitGrade hitGrade)
        {
            CharacterBody target = hurtbox != null ? hurtbox.Owner : null;
            if (attacker == null || target == null || attacker == target) return;

            target.ReceiveHit(attacker, healthDmg, postureDmg, hitPoint,
                false, PerilousType.None, knockback, hitGrade, true);
            HitStop();
        }

        // 双方 Hitbox 相交 → 拼刀：只狼里拼刀双方都涨架势，不打伤害
        public void ReportClash(Hitbox a, Hitbox b, Vector3 point)
        {
            if (a.Owner == null || b.Owner == null || a.Owner == b.Owner) return;

            // 各按对方招式的架势伤害涨架势（乘以拼刀系数）
            if (b.Config != null)
                a.Owner.AccumulatePosture(b.Config.PostureDamage * clashPostureMultiplier);
            if (a.Config != null)
                b.Owner.AccumulatePosture(a.Config.PostureDamage * clashPostureMultiplier);

            // 表现层事件：打铁音效/火花（M13/M15 订阅）
            CombatEventBus.TriggerWeaponDeflected(
                CombatFxPoint.BetweenHitboxes(a, b, point), DeflectType.Normal);
        }

        // ===== 顿帧（打击感）：短暂减速全局时间，营造命中重量感 =====
        public void HitStop(float duration = -1f)
        {
            if (!enableHitStop || GamePause.IsPaused) return;
            if (duration < 0f) duration = hitStopDuration;

            if (hitStopRoutine != null)
                StopCoroutine(hitStopRoutine);
            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            if (GamePause.IsPaused)
            {
                hitStopRoutine = null;
                yield break;
            }

            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            // 顿帧期间若打开暂停，结束时保持冻结，不要拨回 1
            Time.timeScale = GamePause.IsPaused ? 0f : 1f;
            hitStopRoutine = null;
        }

        // ===== 成对忍杀（M10）=====
        // 正向身份断言：只有玩家能发起，只有 Boss 能被处决，禁止自处决。
        public bool TryExecuteFinisher(
            CharacterBody initiator,
            FinisherKind kind = FinisherKind.Ground)
        {
            if (PlayerRef == null || BossRef == null || initiator == null) return false;
            if (initiator != PlayerRef) return false;
            if (initiator == BossRef) return false;
            if (initiator.IsPostureBroken) return false;
            if (activeFinisherPlayer != null) return false;
            if (!BossRef.IsPostureBroken) return false;
            if (!MatchesBreakSource(kind, BossRef.CurrentPostureBreakSource)) return false;

            string playerAnim = ResolveFinisherAnim(kind, initiator.Animator);
            string bossAnim = ResolveFinisherAnim(kind, BossRef.Animator);
            if (string.IsNullOrEmpty(playerAnim) || string.IsNullOrEmpty(bossAnim))
            {
                Debug.LogError(
                    $"成对忍杀状态缺失：{kind}。请同时检查 {initiator.name} 与 {BossRef.name} 的 Animator（Mikiri/Miriki 拼写都算）。");
                return false;
            }

            // 弹反/识破确认窗口里双方已经贴身演完反制，不再用 Ground 处决的距离门卡住。
            if (kind == FinisherKind.Ground)
            {
                float dist = Vector3.Distance(
                    initiator.transform.position, BossRef.transform.position);
                if (dist > finisherRange) return false;
            }

            AlignFinisherFacing(initiator, BossRef, playerAnim, bossAnim);

            activeFinisherPlayer = initiator;
            activeFinisherVictim = BossRef;
            finisherResolved = false;
            initiator.ActiveAttack = null;
            initiator.MoveDirection = Vector3.zero;
            BossRef.MoveDirection = Vector3.zero;
            initiator.IsFinisherLocked = true;
            BossRef.IsFinisherLocked = true;
            initiator.KengekiArmed = false;
            BossRef.KengekiArmed = false;

            initiator.DisableWeaponHit();
            BossRef.DisableWeaponHit();
            CombatEventBus.TriggerFinisherOpportunityChanged(BossRef, false);

            BossRef.MainStateMachine.ChangeState(
                new GroundedState(BossRef, new FinisherVictimState(BossRef, bossAnim)));
            initiator.MainStateMachine.ChangeState(
                new GroundedState(initiator, new FinisherState(initiator, BossRef, playerAnim)));

            CombatEventBus.TriggerFinisherStarted(
                BossRef.transform.position, initiator, BossRef, kind);
            CombatEventBus.TriggerCameraShake(1f);
            return true;
        }

        // 按当前崩解来源选忍杀类型。连招窗口里再按攻击也走这里，优先于 NextCombo。
        public bool TryExecuteAvailableFinisher(CharacterBody initiator)
        {
            if (BossRef == null || !BossRef.IsPostureBroken) return false;

            FinisherKind kind;
            switch (BossRef.CurrentPostureBreakSource)
            {
                case PostureBreakSource.Deflect:
                    kind = FinisherKind.Deflect;
                    break;
                case PostureBreakSource.Mikiri:
                    kind = FinisherKind.Mikiri;
                    break;
                default:
                    kind = FinisherKind.Ground;
                    break;
            }

            return TryExecuteFinisher(initiator, kind);
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
            if (source == null || source != activeFinisherPlayer) return;
            if (activeFinisherVictim == null || finisherResolved) return;

            finisherResolved = true;
            activeFinisherVictim.ClearLife();
            CombatEventBus.TriggerFinisherOpportunityChanged(activeFinisherVictim, false);
        }

        public bool IsFinisherResolved(CharacterBody player)
        {
            return player != null &&
                   player == activeFinisherPlayer &&
                   finisherResolved;
        }

        public void CompleteFinisherSequence(CharacterBody player)
        {
            if (player == null || player != activeFinisherPlayer) return;

            CharacterBody victim = activeFinisherVictim;
            if (player != null) player.IsFinisherLocked = false;
            if (victim != null && victim.LivesRemaining > 0)
                victim.IsFinisherLocked = false;
            activeFinisherPlayer = null;
            activeFinisherVictim = null;
            finisherResolved = false;

            CombatEventBus.TriggerFinisherEnded(player, victim);

            if (victim != null)
            {
                victim.ClearCombatYawFrozen();
                victim.SetSuppressRootYaw(false);
            }

            if (victim != null && victim.LivesRemaining > 0)
            {
                victim.MainStateMachine.ChangeState(new GroundedState(victim));
            }
            player.MainStateMachine.ChangeState(new GroundedState(player));
        }

        // Elbow 投技：打中玩家后双方播 Elbow_Danger。不瞬移，只水平对视。
        public bool TryStartGrabThrow(CharacterBody attacker, CharacterBody victim)
        {
            if (attacker == null || victim == null || attacker == victim) return false;
            if (attacker.IsFinisherLocked || victim.IsFinisherLocked) return false;
            if (activeFinisherPlayer != null) return false;
            if (grabThrowAttacker != null) return false;
            if (!AnimUtil.HasState(attacker.Animator, GrabThrowState.AnimName)
                || !AnimUtil.HasState(victim.Animator, GrabThrowState.AnimName))
            {
                Debug.LogError($"投技缺少 {GrabThrowState.AnimName}：请检查 {attacker.name} 与 {victim.name} 的 Animator。");
                return false;
            }

            grabThrowAttacker = attacker;
            grabThrowVictim = victim;
            grabThrowResolved = false;
            grabThrowAttackerDone = false;
            grabThrowVictimDone = false;

            attacker.DisableWeaponHit();
            victim.DisableWeaponHit();
            attacker.IsAttacking = false;
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

            attacker.MainStateMachine.ChangeState(
                new GroundedState(attacker, new GrabThrowState(attacker)));
            victim.MainStateMachine.ChangeState(
                new GroundedState(victim, new GrabThrowState(victim)));

            CombatEventBus.TriggerCameraShake(0.45f);
            return true;
        }

        public void CompleteGrabThrow(CharacterBody source)
        {
            if (grabThrowResolved) return;
            if (source == null) return;
            if (source == grabThrowAttacker)
                grabThrowAttackerDone = true;
            else if (source == grabThrowVictim)
                grabThrowVictimDone = true;
            else
                return;

            // 双方 Clip 长度可能不同，等两边都到点再一起回 Idle，避免短的一方把长的掐掉。
            if (!grabThrowAttackerDone || !grabThrowVictimDone) return;

            grabThrowResolved = true;
            CharacterBody attacker = grabThrowAttacker;
            CharacterBody victim = grabThrowVictim;
            grabThrowAttacker = null;
            grabThrowVictim = null;

            if (attacker != null)
            {
                attacker.IsFinisherLocked = false;
                attacker.MainStateMachine.ChangeState(new GroundedState(attacker));
            }
            if (victim != null && victim.CurrentHP > 0)
            {
                victim.IsFinisherLocked = false;
                victim.MainStateMachine.ChangeState(new GroundedState(victim));
            }
        }

        private static bool MatchesBreakSource(
            FinisherKind kind,
            PostureBreakSource source)
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
