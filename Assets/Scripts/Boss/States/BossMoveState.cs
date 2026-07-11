using UnityEngine;

/// <summary>
/// Boss 移动/追击状态 — 靠近玩家到攻击距离。
/// Boss 朝玩家方向移动，到达攻击范围后切换回 IdleState 进行决策。
/// </summary>
public class BossMoveState : State
{
    private BossStateMachine BossSM => (BossStateMachine)_stateMachine;

    /// <summary>
    /// 进入移动状态，播放移动动画
    /// </summary>
    public override void Enter()
    {
        if (BossSM.Context?.Animator != null)
            BossSM.Context.Animator.SetFloat("moveSpeed", 1f);
    }

    /// <summary>
    /// 每帧执行：朝玩家移动，到达攻击范围后切换到待机
    /// </summary>
    public override void Execute()
    {
        var ctx = BossSM.Context;
        if (ctx == null) return;
        if (ctx.BossTransform == null || ctx.PlayerTransform == null) return;

        Vector3 bossPos = ctx.BossTransform.position;
        Vector3 playerPos = ctx.PlayerTransform.position;

        // 计算方向（忽略 Y 轴）
        Vector3 dir = playerPos - bossPos;
        dir.y = 0f;
        float distance = dir.magnitude;

        if (distance <= ctx.AttackRange)
        {
            // 到达攻击范围，切换到待机
            _stateMachine.TransitionTo<BossIdleState>();
            return;
        }

        // 朝向玩家
        if (dir.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
            ctx.BossTransform.rotation = Quaternion.Slerp(
                ctx.BossTransform.rotation,
                targetRot,
                Time.deltaTime * 10f);
        }

        // 移动
        Vector3 moveAmount = dir.normalized * ctx.MoveSpeed * Time.deltaTime;
        ctx.BossTransform.position += moveAmount;
    }

    /// <summary>
    /// 退出移动状态，停止移动动画
    /// </summary>
    public override void Exit()
    {
        if (BossSM.Context?.Animator != null)
            BossSM.Context.Animator.SetFloat("moveSpeed", 0f);
    }
}
