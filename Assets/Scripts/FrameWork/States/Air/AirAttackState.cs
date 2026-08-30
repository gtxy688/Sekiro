using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Air
{

    // 空中攻击状态：出招/连招/多段判定/转向全部继承 AttackStateBase，
    // 差异只有四处：退出的空中待机形态、派生同款、空中专属的 Jump2 连段、落地立刻离空。
    public class AirAttackState : AttackStateBase
    {
        private float enterTime;

        public AirAttackState(CharacterBody body, HierarchicalState parent, AttackConfig config)
            : base(body, parent, config)
        {
        }

        // 落地立刻离空：刚进招给一帧宽限，上升中不当落地，避免贴地起跳误掐
        public override bool WantsImmediateLand
        {
            get
            {
                if (Time.time - enterTime < 0.08f) return false;
                if (body.Rb != null && body.Rb.velocity.y > 0.1f) return false;
                return body.IsGrounded;
            }
        }

        protected override void OnAttackEnter()
        {
            enterTime = Time.time;
        }

        protected override void ExitToIdle()
        {
            parent.SubStateMachine.ChangeState(new AirIdleState(body, parent, resumeAirborne: true));
        }

        protected override BaseState NewSelf(AttackConfig next)
        {
            return new AirAttackState(body, parent, next);
        }

        // 空中：窗口内可 Jump2（空中二段）连段
        protected override bool HandleJumpCommand(bool canCancel)
        {
            if (!canCancel) return false;
            // 必须先 Jump2 再切状态：先 ChangeState 会走 AirIdle OnEnter 播 Jump
            // 已用过 Jump2：吃掉命令、连段继续，不要切走 AirAttack
            body.TryAirJump2(out bool played);
            if (played)
                parent.SubStateMachine.ChangeState(new AirIdleState(body, parent, startInJump2: true));
            return true;
        }
    }
}
