using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 忍杀/处决状态 — 对架势崩溃的 Boss 执行忍杀终结技。
    /// 最高优先级 9，不可被任何状态打断。
    /// 播放处决动画后回到 GroundedState。
    /// </summary>
    public class DeathblowState : PlayerBaseState
    {
        public override int Priority => 9;

        private float _deathblowTimer;
        private bool _hasTriggered;

        /// <summary>处决动画时长（秒）</summary>
        private const float DeathblowDuration = 2f;

        /// <summary>
        /// 进入处决状态。
        /// </summary>
        public override void Enter()
        {
            _deathblowTimer = DeathblowDuration;
            _hasTriggered = false;

            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = true;
                Ctx.Animator.SetTrigger("deathblow");
            }
        }

        /// <summary>
        /// 每帧执行：等待处决动画结束。
        /// </summary>
        public override void Execute()
        {
            _deathblowTimer -= Time.deltaTime;

            // 处决动画中途触发事件
            if (!_hasTriggered && _deathblowTimer < DeathblowDuration * 0.5f)
            {
                _hasTriggered = true;
                Sekiro.Core.Events.CombatEvents.RaiseDeathblow();
            }

            if (_deathblowTimer <= 0f)
            {
                PlayerSM.TransitionTo<GroundedState>();
            }
        }

        /// <summary>
        /// 退出处决状态。
        /// </summary>
        public override void Exit()
        {
            if (Ctx.Animator != null)
                Ctx.Animator.applyRootMotion = false;
        }
    }
}
