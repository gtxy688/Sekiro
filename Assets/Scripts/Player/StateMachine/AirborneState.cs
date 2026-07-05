using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 空中状态 — 处理跳跃和下落。
    /// 落地后自动切换回 GroundedState。
    /// 可执行踩头（对扫击危字的反击）。
    /// </summary>
    public class AirborneState : PlayerBaseState
    {
        public override int Priority => 0;

        /// <summary>
        /// 进入空中状态，触发跳跃动画。
        /// </summary>
        public override void Enter()
        {
            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = false;
                Ctx.Animator.SetTrigger("jump");
            }
        }

        /// <summary>
        /// 每帧执行：检测落地和踩头条件。
        /// </summary>
        public override void Execute()
        {
            // 落地检测
            if (Ctx.CharacterController != null && Ctx.CharacterController.isGrounded)
            {
                if (Ctx.Animator != null)
                    Ctx.Animator.SetTrigger("land");

                PlayerSM.TransitionTo<GroundedState>();
                return;
            }

            // 踩头检测（对扫击危）
            if (Ctx.DangerSystem != null && Ctx.DangerSystem.HasActiveDanger)
            {
                float velY = Ctx.CharacterController != null
                    ? Ctx.CharacterController.velocity.y : -1f;

                if (Ctx.DangerSystem.CanStomp(Ctx.Transform.position, velY))
                {
                    // 踩头成功：架势伤害 + 事件
                    Ctx.PostureSystem?.ReducePosture(30f);
                    Ctx.HitStopManager?.Trigger(0.05f);
                    // 踩头后玩家弹起，继续在空中
                    if (Ctx.Animator != null)
                        Ctx.Animator.SetTrigger("stomp");
                }
            }
        }

        /// <summary>
        /// 退出空中状态。
        /// </summary>
        public override void Exit()
        {
        }
    }
}
