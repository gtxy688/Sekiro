using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 回血状态 — 使用药葫芦恢复生命值。
    /// 优先级 5，可被 Hit/Stun/Deathblow 打断（打断时药葫芦不返还）。
    /// 动画结束后自动回复生命并回到 GroundedState。
    /// </summary>
    public class HealState : PlayerBaseState
    {
        public override int Priority => 5;

        private float _healTimer;
        private bool _hasHealed;

        /// <summary>回血动画时长（秒）</summary>
        private const float HealDuration = 0.8f;

        /// <summary>
        /// 进入回血状态。
        /// </summary>
        public override void Enter()
        {
            _healTimer = HealDuration;
            _hasHealed = false;

            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = true;
                Ctx.Animator.SetTrigger("heal");
            }
        }

        /// <summary>
        /// 每帧执行：等待回血动画结束，动画完成后恢复生命。
        /// </summary>
        public override void Execute()
        {
            _healTimer -= Time.deltaTime;

            // 回血动画完成，执行治疗
            if (!_hasHealed && _healTimer <= 0f)
            {
                _hasHealed = true;
                Ctx.CombatController?.Heal();
                PlayerSM.TransitionTo<GroundedState>();
            }
        }

        /// <summary>
        /// 退出回血状态。被提前打断时已消耗的药葫芦不返还。
        /// </summary>
        public override void Exit()
        {
            if (Ctx.Animator != null)
                Ctx.Animator.applyRootMotion = false;

            // 如果动画被中断且还未治疗，药葫芦已消耗（由 PlayerCombatController 在 Heal() 时消耗）
            // 此处不再重复处理
        }
    }
}
