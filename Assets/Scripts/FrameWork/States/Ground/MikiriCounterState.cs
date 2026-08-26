using UnityEngine;

// 识破（踩刀）状态（M17）：突刺危字攻击 + 玩家垫步 → 触发识破
// 播踩刀动画 → 大幅涨攻击者架势 → 崩解时踩刀剩余动画就是忍杀确认窗口
// 叶子状态，位于 GroundedState.SubStateMachine
public class MikiriCounterState : BaseState
{
    private const string MikiriAnim = "Mikiri";
    private const float FallbackDuration = 2.5f;

    private HierarchicalState parent;
    private CharacterBody attacker;
    private float timer;
    private float postureGain;
    private bool finisherWindow;
    private bool hasSeenMikiri;

    public MikiriCounterState(CharacterBody body, HierarchicalState parent, HitData hit) : base(body)
    {
        this.parent = parent;
        this.attacker = hit.attacker;
        postureGain = body.Config != null ? body.Config.MikiriPostureGain : 30f;
    }

    public override void OnEnter()
    {
        timer = 0f;
        hasSeenMikiri = false;

        if (!AnimUtil.TryPlay(body.Animator, MikiriAnim))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少识破状态：{MikiriAnim}");
        }

        finisherWindow = false;
        if (attacker != null)
        {
            finisherWindow = attacker.AccumulatePosture(
                postureGain,
                allowBreak: true,
                source: PostureBreakSource.Mikiri);
            // 没打崩也要打断挥刀，播被识破硬直（只狼：踩刀把这一招废掉）。
            if (!finisherWindow)
            {
                attacker.ForceMikiriStun();
            }
        }

        CombatEventBus.TriggerWeaponDeflected(
            CombatFxPoint.BetweenWeapons(attacker, body, body.transform.position + Vector3.up * 1.2f),
            DeflectType.Perfect);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, MikiriAnim))
        {
            hasSeenMikiri = true;
            if (info.normalizedTime >= 0.95f)
            {
                ExpireWindow();
            }
            return;
        }

        if (hasSeenMikiri && !body.Animator.IsInTransition(0))
        {
            ExpireWindow();
            return;
        }

        // 没切到 Clip 时才用兜底，不能用 0.8s 配置把确认窗口提前掐掉。
        if (!hasSeenMikiri && timer >= FallbackDuration)
        {
            ExpireWindow();
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (finisherWindow && cmd is AttackCommand)
        {
            CombatManager.Instance?.TryExecuteFinisher(body, FinisherKind.Mikiri);
        }
        return true;
    }

    private void ExpireWindow()
    {
        if (finisherWindow && attacker != null && attacker.IsPostureBroken)
        {
            attacker.RecoverFromBreak(0.8f);
        }
        parent.SubStateMachine.ChangeState(new IdleState(body, parent));
    }
}
