using System.Collections;
using UnityEngine;

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
    public CharacterBody BossRef;           // 场景里拖 Boss（单 Boss 战）

    private CharacterBody activeFinisherPlayer;
    private CharacterBody activeFinisherVictim;
    private bool finisherResolved;

    private void Awake()
    {
        // 单例防重
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
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

        // 3. 伤害数据来自 AttackConfig（SO），这里只做转发（含危字标记 M17 / 击退强度）
        if (hitbox.Config == null) return;
        target.ReceiveHit(attacker, hitbox.Config.BaseDamage, hitbox.Config.PostureDamage, hitPoint,
                          hitbox.Config.Perilous != PerilousType.None, hitbox.Config.Perilous,
                          hitbox.Config.Knockback);

        // 命中顿帧（打击感）
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
        CombatEventBus.TriggerWeaponDeflected(point, DeflectType.Normal);
    }

    // ===== 顿帧（打击感）：短暂减速全局时间，营造命中重量感 =====
    public void HitStop(float duration = -1f)
    {
        if (!enableHitStop) return;
        if (duration < 0f) duration = hitStopDuration;

        StopAllCoroutines();
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        Time.timeScale = 0.05f;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }

    // ===== 成对忍杀（M10）=====
    public bool TryExecuteFinisher(
        CharacterBody player,
        FinisherKind kind = FinisherKind.Ground)
    {
        if (BossRef == null || player == null) return false;
        if (activeFinisherPlayer != null) return false;
        if (!BossRef.IsPostureBroken) return false;
        if (!MatchesBreakSource(kind, BossRef.CurrentPostureBreakSource)) return false;

        float dist = Vector3.Distance(player.transform.position, BossRef.transform.position);
        if (dist > finisherRange) return false;

        string animName = ResolveFinisherAnim(kind);
        if (!AnimUtil.HasState(player.Animator, animName) ||
            !AnimUtil.HasState(BossRef.Animator, animName))
        {
            Debug.LogError(
                $"成对忍杀状态缺失：{animName}。请同时检查 {player.name} 与 {BossRef.name} 的 Animator。");
            return false;
        }

        activeFinisherPlayer = player;
        activeFinisherVictim = BossRef;
        finisherResolved = false;
        player.ActiveAttack = null;

        AlignFinisherPair(player, BossRef, kind);
        player.DisableWeaponHit();
        BossRef.DisableWeaponHit();
        CombatEventBus.TriggerFinisherOpportunityChanged(BossRef, false);

        BossRef.MainStateMachine.ChangeState(
            new GroundedState(BossRef, new FinisherVictimState(BossRef, animName)));
        player.MainStateMachine.ChangeState(
            new GroundedState(player, new FinisherState(player, BossRef, animName)));

        CombatEventBus.TriggerFinisher(BossRef.transform.position);
        CombatEventBus.TriggerCameraShake(1f);
        return true;
    }

    // 玩家动画命中帧调用；幂等保护确保重复 Event 不会重复清命。
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
        activeFinisherPlayer = null;
        activeFinisherVictim = null;
        finisherResolved = false;

        if (victim != null && victim.LivesRemaining > 0)
        {
            victim.MainStateMachine.ChangeState(new GroundedState(victim));
        }
        player.MainStateMachine.ChangeState(new GroundedState(player));
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

    private static string ResolveFinisherAnim(FinisherKind kind)
    {
        switch (kind)
        {
            case FinisherKind.Deflect:
                return "Finsher_Deflect";
            case FinisherKind.Mikiri:
                return "Finsher_Mikiri";
            default:
                return "Finsher_Ground";
        }
    }

    private static void AlignFinisherPair(
        CharacterBody player,
        CharacterBody victim,
        FinisherKind kind)
    {
        Vector3 offset = Vector3.forward;
        if (player.Config != null)
        {
            switch (kind)
            {
                case FinisherKind.Deflect:
                    offset = player.Config.FinisherDeflectOffset;
                    break;
                case FinisherKind.Mikiri:
                    offset = player.Config.FinisherMikiriOffset;
                    break;
                default:
                    offset = player.Config.FinisherGroundOffset;
                    break;
            }
        }

        player.transform.position = victim.transform.TransformPoint(offset);

        Vector3 playerToVictim = victim.transform.position - player.transform.position;
        playerToVictim.y = 0f;
        if (playerToVictim.sqrMagnitude < 0.001f) return;

        player.transform.rotation = Quaternion.LookRotation(
            playerToVictim.normalized,
            Vector3.up);
        victim.transform.rotation = Quaternion.LookRotation(
            -playerToVictim.normalized,
            Vector3.up);
    }
}
