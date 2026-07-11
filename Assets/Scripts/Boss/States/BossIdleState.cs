using UnityEngine;

/// <summary>
/// Boss 待机状态 — 选择下一个行为。
/// 在此状态下 Boss 评估与玩家的距离和状态，决定下一步行动。
/// 如果玩家不在攻击范围内，切换到 MoveState 追击。
/// </summary>
public class BossIdleState : State
{
    /// <summary>待机持续时间（秒），之后进行行为决策</summary>
    private const float IdleDuration = 0.5f;

    private float _idleTimer;
    private BossStateMachine BossSM => (BossStateMachine)_stateMachine;

    /// <summary>
    /// 进入待机状态，重置计时器
    /// </summary>
    public override void Enter()
    {
        _idleTimer = 0f;

        if (BossSM.Context?.Animator != null)
            BossSM.Context.Animator.SetFloat("moveSpeed", 0f);
    }

    /// <summary>
    /// 每帧执行：等待一段时间后决策下一个行为
    /// </summary>
    public override void Execute()
    {
        var ctx = BossSM.Context;
        if (ctx == null) return;

        _idleTimer += Time.deltaTime;

        if (_idleTimer >= IdleDuration)
        {
            float distance = GetDistanceToPlayer(ctx);

            if (distance > ctx.AttackRange)
            {
                // 玩家不在攻击范围内，追击
                _stateMachine.TransitionTo<BossMoveState>();
            }
            else
            {
                // 在攻击范围内，选择攻击
                var playerState = new PlayerStateInfo
                {
                    distanceToBoss = distance,
                    playerComboCount = ctx.DeflectSystem != null
                        ? ctx.DeflectSystem.PlayerComboCount : 0
                };

                int seed = Random.Range(0, 10000);
                var attack = BossAIController.SelectAttack(playerState, seed);

                if (attack != null && attack.isRanged)
                {
                    // 远程攻击（射箭）— 由 AttackState 处理
                    ctx.AIController.TransitionTo(BossAIState.Attack);
                }
                else
                {
                    ctx.AIController.TransitionTo(BossAIState.Attack);
                }

                _stateMachine.TransitionTo<BossAttackState>();
            }
        }
    }

    /// <summary>
    /// 退出待机状态
    /// </summary>
    public override void Exit()
    {
        _idleTimer = 0f;
    }

    /// <summary>
    /// 计算与玩家的距离
    /// </summary>
    private float GetDistanceToPlayer(BossContext ctx)
    {
        if (ctx.BossTransform == null || ctx.PlayerTransform == null)
            return float.MaxValue;

        return Vector3.Distance(
            ctx.BossTransform.position,
            ctx.PlayerTransform.position);
    }
}
