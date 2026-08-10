using UnityEngine;

// 继承自 BaseState，作为 GroundedState (地面父状态) 的子叶子节点
public class MoveState : BaseState
{
    private HierarchicalState parent;
    
    // 移动和转身速度，实战中这些参数通常写在 CharacterBody 或一个独立的 ScriptableObject 配置文件中
    private float moveSpeed = 6f; 
    private float rotationSpeed = 15f; 

    public MoveState(CharacterBody body, HierarchicalState parent) : base(body) 
    {
        this.parent = parent;
    }

    // 1. 生命周期：进入跑动
    public override void OnEnter() 
    { 
        // 0.1秒淡入跑动动画，显得平滑
        body.Animator.CrossFade("Run", 0.1f); 
    }


    // 2. 生命周期：执行真正的位移与转身
    public override void OnUpdate()
    {
        // 从 Body 的黑板数据中获取当前最新的意图方向
        Vector2 inputDir = body.MoveDirection;

        // 【位移】：将 2D 的输入方向转换为 3D 的世界速度向量，保留 Y 轴原本的重力下落速度
        Vector3 targetVelocity = new Vector3(inputDir.x * moveSpeed, body.Rb.velocity.y, inputDir.y * moveSpeed);
        
        // 赋予刚体真正的物理速度
        body.Rb.velocity = targetVelocity;

        // 【转身】：如果当前有明确的方向输入，就让角色平滑地转过去
        if (inputDir.sqrMagnitude > 0.01f)
        {
            // 将 2D 向量转为 3D 前方向量
            Vector3 lookDirection = new Vector3(inputDir.x, 0, inputDir.y);
            
            // 计算目标旋转四元数
            Quaternion targetRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            
            // 利用 Slerp (球面插值) 实现平滑转身，避免人物像平移木偶一样瞬间回头
            body.transform.rotation = Quaternion.Slerp(body.transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
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
        return false;
    }
}