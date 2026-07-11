using UnityEngine;

/// <summary>
/// Boss 攻击状态 — 执行攻击的前摇→判定→后摇帧计数流程。
/// 支持单次攻击和三连斩多段攻击。
/// 攻击判定帧内通过 Physics.OverlapSphere 检测玩家命中。
/// </summary>
public class BossAttackState : State
{
    /// <summary>攻击阶段枚举</summary>
    private enum AttackPhase
    {
        Startup,
        Active,
        Recovery
    }

    private BossStateMachine BossSM => (BossStateMachine)_stateMachine;

    private AttackPhase _phase;
    private float _phaseTimer;
    private float _frameTime;
    private AttackData _currentAttack;
    private bool _hasHit;

    /// <summary>60fps 下单帧时间（秒）</summary>
    private const float FrameDuration = 1f / 60f;

    /// <summary>
    /// 进入攻击状态，选择招式并开始前摇
    /// </summary>
    public override void Enter()
    {
        var ctx = BossSM.Context;
        if (ctx == null) return;

        _hasHit = false;
        _frameTime = FrameDuration;

        // 选择攻击招式
        if (ctx.AIController != null && ctx.AIController.IsCounterAttackPending)
        {
            // 弹刀成功后的反击：使用横斩
            _currentAttack = BossAttackData.CreateHorizontalSlash();
            ctx.AIController.ConsumeCounterAttack();
        }
        else
        {
            float distance = GetDistanceToPlayer(ctx);
            var playerState = new PlayerStateInfo
            {
                distanceToBoss = distance,
                playerComboCount = ctx.DeflectSystem != null
                    ? ctx.DeflectSystem.PlayerComboCount : 0
            };
            int seed = Random.Range(0, 10000);
            _currentAttack = BossAIController.SelectAttack(playerState, seed);
        }

        if (_currentAttack == null)
            _currentAttack = BossAttackData.CreateHorizontalSlash();

        // 播放动画
        if (ctx.Animator != null)
            ctx.Animator.SetTrigger(_currentAttack.animName);

        // 开始前摇阶段
        _phase = AttackPhase.Startup;
        _phaseTimer = _currentAttack.startupFrames * _frameTime;
    }

    /// <summary>
    /// 每帧执行：推进攻击阶段（前摇→判定→后摇）
    /// </summary>
    public override void Execute()
    {
        _phaseTimer -= Time.deltaTime;

        switch (_phase)
        {
            case AttackPhase.Startup:
                if (_phaseTimer <= 0f)
                {
                    _phase = AttackPhase.Active;
                    _phaseTimer = _currentAttack.activeFrames * _frameTime;
                }
                break;

            case AttackPhase.Active:
                if (!_hasHit)
                    TryHitPlayer();

                if (_phaseTimer <= 0f)
                {
                    _phase = AttackPhase.Recovery;
                    _phaseTimer = _currentAttack.recoveryFrames * _frameTime;
                }
                break;

            case AttackPhase.Recovery:
                if (_phaseTimer <= 0f)
                {
                    // 攻击结束，回到待机
                    _stateMachine.TransitionTo<BossIdleState>();
                }
                break;
        }
    }

    /// <summary>
    /// 退出攻击状态，清理攻击数据
    /// </summary>
    public override void Exit()
    {
        _currentAttack = null;
        _hasHit = false;
    }

    /// <summary>
    /// 在判定帧内检测是否命中玩家（使用 Physics.OverlapSphere）
    /// </summary>
    private void TryHitPlayer()
    {
        var ctx = BossSM.Context;
        if (ctx == null || ctx.BossTransform == null) return;
        if (_currentAttack == null || _currentAttack.isRanged) return;

        Vector3 hitPos = ctx.BossTransform.position
            + ctx.BossTransform.forward * _currentAttack.hitboxRadius;

        Collider[] hits = Physics.OverlapSphere(hitPos, _currentAttack.hitboxRadius);

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i].transform == ctx.PlayerTransform)
            {
                _hasHit = true;

                // 计算攻击方向（Boss → 玩家）
                Vector3 attackDir = (ctx.PlayerTransform.position - ctx.BossTransform.position).normalized;
                Vector3 playerForward = ctx.PlayerTransform.forward;

                // 通过 Combat 层事件传递完整攻击数据
                BossAttackEvents.RaiseBossAttackHitPlayer(_currentAttack, attackDir, playerForward);
                break;
            }
        }
    }

    /// <summary>
    /// 计算与玩家的距离
    /// </summary>
    private float GetDistanceToPlayer(BossContext ctx)
    {
        if (ctx.BossTransform == null || ctx.PlayerTransform == null)
            return 0f;
        return Vector3.Distance(ctx.BossTransform.position, ctx.PlayerTransform.position);
    }
}
