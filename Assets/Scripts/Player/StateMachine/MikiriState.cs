using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 识破状态 — 对突刺危的精确反击。
    /// 向前踏步踩住敌人武器，造成架势伤害和长时间硬直。
    /// 动画结束后回到 GroundedState。
    /// </summary>
    public class MikiriState : PlayerBaseState
    {
        public override int Priority => 2;

        private float _mikiriTimer;

        /// <summary>识破动作时长（秒）</summary>
        private const float MikiriDuration = 0.6f;

        /// <summary>识破架势伤害</summary>
        private const float PostureDamage = 50f;

        /// <summary>
        /// 进入识破状态。
        /// </summary>
        public override void Enter()
        {
            _mikiriTimer = MikiriDuration;

            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = true;
                Ctx.Animator.SetTrigger("mikiri");
            }

            // 对 Boss 造成架势伤害（通过事件）
            Sekiro.Core.Events.CombatEvents.RaiseBossDamaged(PostureDamage);

            // 帧冻结
            Ctx.HitStopManager?.Trigger(0.067f);

            // 消除当前危字
            Ctx.DangerSystem?.DeactivateDanger();
        }

        /// <summary>
        /// 每帧执行：等待识破动画结束。
        /// </summary>
        public override void Execute()
        {
            _mikiriTimer -= Time.deltaTime;

            if (_mikiriTimer <= 0f)
            {
                PlayerSM.TransitionTo<GroundedState>();
            }
        }

        /// <summary>
        /// 退出识破状态。
        /// </summary>
        public override void Exit()
        {
            if (Ctx.Animator != null)
                Ctx.Animator.applyRootMotion = false;
        }
    }
}
