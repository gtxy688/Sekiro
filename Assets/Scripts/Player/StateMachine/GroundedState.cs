using UnityEngine;
using Sekiro.Core.Input;

namespace Sekiro.Player.StateMachine.States
{
    /// <summary>
    /// 地面状态 — 处理空闲、移动、锁定移动三种子状态。
    /// 读取 WASD 输入驱动 CharacterController 移动，并检测战斗输入跳转到对应战斗状态。
    /// 基础层优先级 0，可被所有战斗状态打断。
    /// </summary>
    public class GroundedState : PlayerBaseState
    {
        public override int Priority => 0;

        private PlayerContext Ctx => PlayerSM.Context;

        /// <summary>
        /// 进入地面状态，重置移动参数。
        /// </summary>
        public override void Enter()
        {
            if (Ctx.Animator != null)
            {
                Ctx.Animator.applyRootMotion = false;
            }
        }

        /// <summary>
        /// 每帧执行：处理移动输入 + 检测战斗输入跳转。
        /// </summary>
        public override void Execute()
        {
            if (Ctx.InputReader == null) return;

            // 读取移动输入并更新动画参数
            Vector2 moveInput = Ctx.InputReader.GetMoveInput();

            if (Ctx.Animator != null)
            {
                Ctx.Animator.SetFloat("moveX", moveInput.x, 0.1f, Time.deltaTime);
                Ctx.Animator.SetFloat("moveZ", moveInput.y, 0.1f, Time.deltaTime);
                Ctx.Animator.SetFloat("speed", moveInput.magnitude, 0.1f, Time.deltaTime);
            }

            // 物理移动由 PlayerController 的 Update 处理（保持独立）

            // 检测战斗输入 → 跳转到对应状态
            // 优先级：Deflect(4) > Dodge(3) > Attack(1)

            // 弹刀（右键）优先级最高
            if (Ctx.InputReader.IsDeflectPressed())
            {
                Ctx.DeflectSystem?.OnDeflectPressed();
                PlayerSM.TryTransitionTo<DeflectState>();
                return;
            }

            // 闪避（Shift）
            if (Ctx.InputReader.IsDodgePressed())
            {
                // 突刺危 → 识破，否则闪避
                if (Ctx.DangerSystem != null && Ctx.DangerSystem.CanMikiri(Ctx.Transform.position, Ctx.Transform.forward))
                {
                    PlayerSM.TryTransitionTo<MikiriState>();
                }
                else
                {
                    PlayerSM.TryTransitionTo<DodgeState>();
                }
                return;
            }

            // 攻击（左键）
            if (Ctx.InputReader.IsAttackPressed())
            {
                PlayerSM.TryTransitionTo<AttackState>();
                return;
            }

            // 跳跃（空格）
            if (Ctx.InputReader.IsJumpPressed())
            {
                PlayerSM.TryTransitionTo<AirborneState>();
                return;
            }

            // 回血（E）
            if (Ctx.InputReader.IsHealPressed())
            {
                if (Ctx.CombatController != null && Ctx.CombatController.HealingCharges > 0)
                {
                    PlayerSM.TryTransitionTo<HealState>();
                }
                return;
            }

            // 架势崩溃 → 硬直状态
            if (Ctx.PostureSystem != null && Ctx.PostureSystem.IsBroken)
            {
                PlayerSM.TryTransitionTo<StunState>();
                return;
            }

            // 处决检测：Boss 架势崩溃 + 攻击键
            if (Ctx.InputReader.IsAttackPressed())
            {
                // 处决条件由外部系统通过 CombatEvents 驱动
                // 此处仅检测事件回调触发的状态跳转
            }
        }

        /// <summary>
        /// 退出地面状态。
        /// </summary>
        public override void Exit()
        {
            // 保留移动参数，让动画平滑过渡
        }
    }
}
