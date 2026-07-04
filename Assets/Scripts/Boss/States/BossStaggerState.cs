using UnityEngine;
using Sekiro.Core.StateMachine;
using Sekiro.Boss.AI;

namespace Sekiro.Boss.States
{
    /// <summary>
    /// Boss 硬直状态 — 被弹刀后的短暂停顿（约 0.3 秒）。
    /// 硬直结束后回到待机状态。
    /// </summary>
    public class BossStaggerState : State
    {
        private BossStateMachine BossSM => (BossStateMachine)_stateMachine;

        private float _staggerTimer;

        /// <summary>
        /// 进入硬直状态，设置硬直时长并播放硬直动画
        /// </summary>
        public override void Enter()
        {
            var ctx = BossSM.Context;

            float duration = 0.3f;
            if (ctx?.AIController != null)
                duration = ctx.AIController.StaggerDuration;

            _staggerTimer = duration;

            if (ctx?.Animator != null)
                ctx.Animator.SetTrigger("stagger");
        }

        /// <summary>
        /// 每帧执行：倒计时硬直时间
        /// </summary>
        public override void Execute()
        {
            _staggerTimer -= Time.deltaTime;

            if (_staggerTimer <= 0f)
            {
                _stateMachine.TransitionTo<BossIdleState>();
            }
        }

        /// <summary>
        /// 退出硬直状态
        /// </summary>
        public override void Exit()
        {
            _staggerTimer = 0f;
        }
    }
}
