using UnityEngine;
using Sekiro.Core.StateMachine;

namespace Sekiro.Boss.States
{
    /// <summary>
    /// Boss 架势崩溃状态 — 架势槽充满后进入。
    /// 持续 2 秒，期间 Boss 无法行动，可被玩家处决。
    /// 结束后回到 IdleState。
    /// </summary>
    public class BossCollapseState : State
    {
        private BossStateMachine BossSM => (BossStateMachine)_stateMachine;

        private float _collapseTimer;

        /// <summary>崩溃持续时间（秒）</summary>
        private const float CollapseDuration = 2f;

        /// <summary>
        /// 进入崩溃状态，播放崩溃动画并启动计时器。
        /// </summary>
        public override void Enter()
        {
            _collapseTimer = CollapseDuration;

            var ctx = BossSM.Context;
            if (ctx?.Animator != null)
                ctx.Animator.SetTrigger("collapse");
        }

        /// <summary>
        /// 每帧执行：等待崩溃时间结束。
        /// 期间可被玩家处决打断（由外部调用 TransitionTo）。
        /// </summary>
        public override void Execute()
        {
            _collapseTimer -= Time.deltaTime;

            if (_collapseTimer <= 0f)
            {
                // 重置架势后回到待机
                _stateMachine.TransitionTo<BossIdleState>();
            }
        }

        /// <summary>
        /// 退出崩溃状态。
        /// </summary>
        public override void Exit()
        {
            _collapseTimer = 0f;
        }
    }
}
