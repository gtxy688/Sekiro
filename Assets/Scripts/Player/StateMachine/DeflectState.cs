using UnityEngine;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 弹刀状态 — 播放弹刀动画，与 DeflectSystem 协同。
    /// DeflectSystem 负责弹刀窗口计时和判定，
    /// DeflectState 负责动画表现和状态切换。
    /// 弹刀窗口结束后回到 GroundedState。
    /// </summary>
    public class DeflectState : PlayerBaseState
    {
        public override int Priority => 4;

        /// <summary>
        /// 进入弹刀状态，播放弹刀动画。
        /// OnDeflectPressed 已由 PlayerCombatController 转发给 DeflectSystem。
        /// </summary>
        public override void Enter()
        {
            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = true;
                Ctx.Animator.SetTrigger("deflect");
            }

            // 设置架势系统为格挡状态（降低恢复速度）
            Ctx.PostureSystem?.SetBlocking(true);
        }

        /// <summary>
        /// 每帧执行：等待弹刀/格挡窗口结束。
        /// </summary>
        public override void Execute()
        {
            if (Ctx.DeflectSystem == null) return;

            // DeflectSystem 内部管理弹刀/格挡状态
            // 当完全退出弹刀和格挡状态时，返回地面
            if (!Ctx.DeflectSystem.IsDeflecting && !Ctx.DeflectSystem.IsBlocking)
            {
                PlayerSM.TransitionTo<GroundedState>();
                return;
            }

            // 松开右键 → 提前退出
            if (Ctx.InputReader != null && !Ctx.InputReader.IsDeflectHeld())
            {
                // 但给 DeflectSystem 自行管理退出时机
                // 只有 DeflectSystem 也标记为非活动时才退出
            }
        }

        /// <summary>
        /// 退出弹刀状态。
        /// </summary>
        public override void Exit()
        {
            if (Ctx.Animator != null)
                Ctx.Animator.applyRootMotion = false;

            Ctx.PostureSystem?.SetBlocking(false);
        }
    }
}
