using UnityEngine;

// 移动状态（全权根运动：位移由 Run 动画 Root 曲线驱动，这里只负责转身）
public class MoveState : BaseState
{
    private HierarchicalState parent;

    // 转身速度从 CharacterConfig(SO) 读取，禁止硬编码（容错给默认值）
    private float rotationSpeed = 720f;

    public MoveState(CharacterBody body, HierarchicalState parent) : base(body) 
    {
        this.parent = parent;
        if (body.Config != null)
        {
            rotationSpeed = body.Config.RotationSpeed;
        }
    }

    // 1. 生命周期：进入跑动
    public override void OnEnter() 
    { 
        // 0.1秒淡入行走动画，显得平滑
        body.Animator.CrossFade("Walk", 0.1f); 
    }


    // 2. 生命周期：转身（位移完全交给 OnAnimatorMove 的根运动桥接）
    public override void OnUpdate()
    {
        // 从 Body 的黑板数据中获取当前最新的意图方向
        Vector2 inputDir = body.MoveDirection;

        // 【位移】：不再直接设置速度！跑动位移来自 Run 动画的 Root 曲线
        // （CharacterBody.OnAnimatorMove 会把动画位移转成物理速度）

        // 【转身】：如果当前有明确的方向输入，就让角色平滑地转过去
        if (inputDir.sqrMagnitude > 0.01f)
        {
            Vector3 moveDir;
            if (IsLockedOnTarget())
            {
                // M11：锁定中 → 面向 Boss（位移仍由根运动驱动，形成绕圈效果）
                Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
                toBoss.y = 0f;
                moveDir = toBoss.normalized;
            }
            else
            {
                moveDir = body.InputToWorldDir(inputDir);
            }

            // 计算目标旋转四元数
            Quaternion targetRotation = Quaternion.LookRotation(moveDir, Vector3.up);
            
            // 用 RotateTowards 按 度/秒 平滑转身，避免人物像平移木偶一样瞬间回头
            body.transform.rotation = Quaternion.RotateTowards(body.transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    // 是否锁定中且目标有效（M11）
    private bool IsLockedOnTarget()
    {
        return LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
            && LockOnManager.Instance.Target != null;
    }

    // 3. 生命周期：退出跑动
    public override void OnExit() 
    { 
        // 离开移动状态时，不需要在这里把 velocity 设为 0。
        // 因为下一个接手的状态（比如 IdleState 或 AttackState）会在它们的 OnEnter 里负责刹车。
    }

    // 4. 核心：指令防火墙与分发
    public override bool HandleCommand(ICommand cmd)
    {
        // 情况 A：收到移动指令 (连续刷新)
        if (cmd is MoveCommand moveCmd)
        {
            if (moveCmd.Direction.sqrMagnitude < 0.01f)
            {
                // 玩家松开了摇杆！意图归零，切换回 IdleState
                body.MoveDirection = Vector2.zero;
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            }
            else
            {
                // 玩家还在推摇杆，可能只是换了个角度 (比如从左转到了右)
                // 【绝不切换状态！】只更新 Body 黑板上的方向，OnUpdate 会自动读取新方向进行转身
                body.MoveDirection = moveCmd.Direction;
            }
            
            // 无论如何，移动指令已被消化
            return true; 
        }

        // M6：移动中按攻击 → 直接进 AttackState（攻击中会原地停住，由 AttackState 接管）
        if (cmd is AttackCommand)
        {
            parent.SubStateMachine.ChangeState(new AttackState(body, parent, body.GetAttackConfig()));
            return true;
        }

        // M6：移动中按防御 → 进弹反/防御姿态
        if (cmd is DeflectCommand)
        {
            parent.SubStateMachine.ChangeState(new DeflectState(body, parent));
            return true;
        }

        // M6：移动中按闪避 → 垫步
        if (cmd is DodgeCommand)
        {
            parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
            return true;
        }
        return false;
    }
}