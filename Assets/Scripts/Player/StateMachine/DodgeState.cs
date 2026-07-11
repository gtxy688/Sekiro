using UnityEngine;

/// <summary>
/// 闪避状态 — 执行闪避动画和位移。
/// 使用 Root Motion 进行移动，具有无敌帧。
/// 动画结束后回到 GroundedState。
/// </summary>
public class DodgeState : PlayerBaseState
{
    public override int Priority => 3;

    private float _dodgeTimer;
    private bool _isInvincible;

    /// <summary>闪避持续时间（秒）</summary>
    private const float DodgeDuration = 0.5f;

    /// <summary>无敌帧持续时间（秒），约 6-8 帧@60fps</summary>
    private const float InvincibilityDuration = 0.1f;

    /// <summary>
    /// 进入闪避状态。
    /// </summary>
    public override void Enter()
    {
        _dodgeTimer = DodgeDuration;
        _isInvincible = true;

        if (Ctx.Animator != null)
        {
            Ctx.Animator.applyRootMotion = true;
            Ctx.Animator.SetTrigger("dodge");
        }
    }

    /// <summary>
    /// 每帧执行：递减闪避计时器，管理无敌帧。
    /// </summary>
    public override void Execute()
    {
        _dodgeTimer -= Time.deltaTime;

        // 无敌帧超时
        if (_isInvincible && _dodgeTimer < DodgeDuration - InvincibilityDuration)
        {
            _isInvincible = false;
        }

        // 闪避结束
        if (_dodgeTimer <= 0f)
        {
            PlayerSM.TransitionTo<GroundedState>();
        }
    }

    /// <summary>
    /// 退出闪避状态。
    /// </summary>
    public override void Exit()
    {
        _isInvincible = false;

        if (Ctx.Animator != null)
            Ctx.Animator.applyRootMotion = false;
    }
}
