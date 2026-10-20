using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.Player;
namespace ARPG.FrameWork.States.Ground
{
    // 地面待机叶子：清除水平移动残留，并将移动、攻击、防御和垫步命令切入对应状态。
    public class IdleState : BaseState
    {
        HierarchicalState parent; // 记住自己的父节点，用来切子状态
    
        public IdleState(CharacterBody body, HierarchicalState parent) : base(body) 
        {
            this.parent = parent;
        }

        public override void OnEnter()
        {
            AnimUtil.TryCrossFade(body.Animator, "Idle", 0.1f); 

            // 清除水平速度，保留重力方向的速度。
            body.Rb.velocity = new Vector3(0, body.Rb.velocity.y, 0);
        
            body.MoveDirection = Vector2.zero;
        }

        public override bool HandleCommand(ICommand cmd)
        {
            // 地面父层先处理跳跃、喝药等公共命令；到达这里的命令只决定当前地面动作。
            if (cmd is MoveCommand moveCmd)
            {
                // 忽略死区内输入，避免原地反复进入移动状态。
                if (moveCmd.Direction.sqrMagnitude > 0.01f)
                {
                    body.MoveDirection = moveCmd.Direction;
                    bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
                        && LockOnManager.Instance.Target != null;
                    string enter = locked ? "IdleToStrafe" : "IdleToWalk";
                    parent.SubStateMachine.ChangeState(new MoveState(body, parent, enter));
                }
            
                // 死区内输入也视为已消费，避免缓冲反复重试。
                return true; 
            }

            if (cmd is AttackCommand)
            {
                // AttackCommand 只有“想攻击”的含义；具体哪一招由 CharacterBody 的配置入口提供。
                parent.SubStateMachine.ChangeState(new AttackState(body, parent, body.GetAttackConfig()));
                return true;
            }

            if (cmd is DeflectCommand deflectCmd)
            {
                // 将真实按下的 PressId 继续传递，DeflectState 真正进入时才开启弹反窗口。
                parent.SubStateMachine.ChangeState(
                    new DeflectState(body, parent, pressId: deflectCmd.PressId));
                return true;
            }

            if (cmd is DodgeCommand)
            {
                parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
                return true;
            }
        
            return false; 
        }
    }
}
