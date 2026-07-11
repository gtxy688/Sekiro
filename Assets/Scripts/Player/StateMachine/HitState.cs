using UnityEngine;

/// <summary>
/// 受击状态 — 玩家未防御时被 Boss 击中。
/// 播放受击动画，动画结束后回到 GroundedState。
/// 优先级 7，可被 Stun/Deathblow 打断。
/// </summary>
public class HitState : PlayerBaseState
{
    public override int Priority => 7;

    private float _hitTimer;

    /// <summary>受击硬直时长（秒）</summary>
    private const float HitDuration = 0.5f;

    /// <summary>
    /// 进入受击状态。
    /// </summary>
    public override void Enter()
    {
        _hitTimer = HitDuration;

        if (Ctx.Animator != null)
        {
            Ctx.Animator.applyRootMotion = true;
            Ctx.Animator.SetTrigger("hit");
        }
    }

    /// <summary>
    /// 每帧执行：等待受击动画结束。
    /// </summary>
    public override void Execute()
    {
        _hitTimer -= Time.deltaTime;

        if (_hitTimer <= 0f)
        {
            PlayerSM.TransitionTo<GroundedState>();
        }
    }

    /// <summary>
    /// 退出受击状态。
    /// </summary>
    public override void Exit()
    {
        if (Ctx.Animator != null)
            Ctx.Animator.applyRootMotion = false;
    }
}
