using UnityEngine;

/// <summary>
/// 硬直状态 — 架势崩溃后的强制硬直。
/// 持续 1.5 秒后自动回到 GroundedState 并重置架势。
/// 优先级 8，仅可被 Deathblow 打断（玩家处决 Boss 时）。
/// </summary>
public class StunState : PlayerBaseState
{
    public override int Priority => 8;

    private float _stunTimer;

    /// <summary>硬直时长（秒）</summary>
    private const float StunDuration = 1.5f;

    /// <summary>
    /// 进入硬直状态。
    /// </summary>
    public override void Enter()
    {
        _stunTimer = StunDuration;

        if (Ctx.Animator != null)
        {
            Ctx.Animator.applyRootMotion = true;
            Ctx.Animator.SetTrigger("stun");
        }

        // 广播玩家架势崩溃事件
        CombatEvents.RaisePlayerPostureBreak();
    }

    /// <summary>
    /// 每帧执行：等待硬直结束。
    /// </summary>
    public override void Execute()
    {
        _stunTimer -= Time.deltaTime;

        if (_stunTimer <= 0f)
        {
            // 重置架势
            Ctx.PostureSystem?.Reset();
            PlayerSM.TransitionTo<GroundedState>();
        }
    }

    /// <summary>
    /// 退出硬直状态。
    /// </summary>
    public override void Exit()
    {
        _stunTimer = 0f;

        if (Ctx.Animator != null)
            Ctx.Animator.applyRootMotion = false;
    }
}
