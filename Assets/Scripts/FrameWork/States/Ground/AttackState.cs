using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;

namespace ARPG.FrameWork.States.Ground
{
    // 地面攻击状态：出招/连招/多段判定/转向全部继承 AttackStateBase，
    // 差异只有四处：退出的待机形态、派生同款、地面专属的格挡/垫步取消、后摇接移动。
    public class AttackState : AttackStateBase
    {
        public AttackState(CharacterBody body, HierarchicalState parent, AttackConfig config)
            : base(body, parent, config)
        {
        }

        protected override void ExitToIdle()
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }

        protected override BaseState NewSelf(AttackConfig next)
        {
            return new AttackState(body, parent, next);
        }

        // 地面：前摇/后摇窗口内可格挡取消
        protected override bool CancelToDeflect(bool canCancel)
        {
            if (!canCancel) return false;
            parent.SubStateMachine.ChangeState(new DeflectState(body, parent));
            return true;
        }

        // 地面：前摇/后摇窗口内可垫步取消
        protected override bool CancelToDodge(bool canCancel)
        {
            if (!canCancel) return false;
            parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
            return true;
        }

        // 地面：后摇段收到移动即进入四向循环；空中只记录方向（基类默认）
        protected override bool HandleMoveCommand(MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            if (animTime >= config.RecoveryWindowStart &&
                moveCmd.Direction.sqrMagnitude >= 0.01f)
            {
                // 锁定攻击接移动时直接进入四向循环。
                // 若走默认 IdleToWalk，其前向根运动会让角色额外朝目标冲出一段。
                string enterAnim = HasCombatTarget() ? null : "IdleToWalk";
                parent.SubStateMachine.ChangeState(new MoveState(body, parent, enterAnim));
            }
            return true;
        }
    }
}
