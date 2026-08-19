using UnityEngine;
public class IdleState : BaseState
{
    HierarchicalState parent; // 记住自己的父节点，用来切子状态
    
    public IdleState(CharacterBody body, HierarchicalState parent) : base(body) 
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        // 平滑过渡到 Idle 动画，0.1f 的淡入时间避免动画切换生硬
        body.Animator.CrossFade("Idle", 0.1f); 

        // 确保进入 Idle 时，角色的物理速度被完全清空，防止“滑冰”现象
        body.Rb.velocity = new Vector3(0, body.Rb.velocity.y, 0);
        
        // 确保移动意图数据归零
        body.MoveDirection = Vector2.zero;
    }

    // 处理传递到底层的命令 传下来的命令可能是 MoveCommand 
    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is MoveCommand moveCmd)
        {
            // 判断摇杆推的力度是否足够大（防止手柄死区漂移导致的鬼畜原地踏步）
            if (moveCmd.Direction.sqrMagnitude > 0.01f)
            {
                // 把移动方向写入 Body 黑板，供 MoveState 读取
                body.MoveDirection = moveCmd.Direction;
                bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
                    && LockOnManager.Instance.Target != null;
                string enter = locked ? "IdleToStrafe" : "IdleToWalk";
                parent.SubStateMachine.ChangeState(new MoveState(body, parent, enter));
            }
            
            // 无论力度大小，只要是移动指令，Idle 状态就宣称“我处理完了”。
            // 如果力度小没切状态，返回 true 可以让大脑清空缓冲，避免堆积。
            return true; 
        }

        // M6：待机时按攻击 → 用默认招式配置进入 AttackState
        // 配置为空时 AttackState 内部有保护，会直接退回 Idle，不会崩
        if (cmd is AttackCommand)
        {
            parent.SubStateMachine.ChangeState(new AttackState(body, parent, body.GetAttackConfig()));
            return true;
        }

        // M6：待机时按防御 → 进入弹反/防御姿态（弹反窗口逻辑 M4 细化）
        if (cmd is DeflectCommand)
        {
            parent.SubStateMachine.ChangeState(new DeflectState(body, parent));
            return true;
        }

        // M6：待机时按闪避 → 垫步（位移由动画根运动驱动，无敌帧 M4 细化）
        if (cmd is DodgeCommand)
        {
            parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
            return true;
        }
        
        return false; 
    }
}