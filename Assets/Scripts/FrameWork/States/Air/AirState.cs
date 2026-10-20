using UnityEngine;

using ARPG.FrameWork.Body;
using ARPG.FrameWork.States.Base;
using ARPG.FrameWork.States.Ground;
namespace ARPG.FrameWork.States.Air
{

    // 空中父状态：子状态按 Jump → Jumping → Fall 切动画；空中刀是独立叶子。
    // 根运动保持开启（XZ 跟动画）；高度由刚体的竖直速度与重力控制。
    // 地面跳跃或踩空都会进入这里；父状态负责落地出口，子状态负责空中动作。
    public class AirState : HierarchicalState
    {
        public AirState(CharacterBody body) : base(body) { }

        public override void OnEnter()
        {
            body.IsAirborne = true;
            // 每次进空重置：同一滞空只能 Jump2 一次，落地再跳重新给
            body.ResetAirJump2();
            base.OnEnter();
        }

        public override void OnExit()
        {
            body.IsAirborne = false;
            base.OnExit();
        }

        protected override BaseState GetInitialSubState()
        {
            return new AirIdleState(body, this);
        }

        public override void OnUpdate()
        {
            // 子状态先更新“是否可以离开空中”，父状态再据此切回地面，避免落地动作被提前掐断。
            base.OnUpdate();

            if (SubStateMachine.CurrentState is AirAttackState airAtk && airAtk.WantsImmediateLand)
            {
                LeaveAir();
                return;
            }

            AirIdleState air = SubStateMachine.CurrentState as AirIdleState;
            if (air == null || !air.CanLeaveAir) return;
            LeaveAir();
        }

        private void LeaveAir()
        {
            // 落地不等于自动清除架势崩解；仍崩解时要回倒地状态，不能直接待机。
            if (body.IsPostureBroken)
            {
                body.EnterGrounded(new StaggerBrokenState(body), "air: landed while posture broken");
            }
            else
            {
                body.EnterGrounded("air: landed");
            }
        }
    }

}
