using UnityEngine;

// 具体的动作节点：追击玩家 (生成 MoveCommand)
public class BT_MoveToTarget : Node
{
    private CharacterBody body;
    private Transform target;
    private float stopDistance;

    public BT_MoveToTarget(CharacterBody body, Transform target, float stopDistance)
    {
        this.body = body;
        this.target = target;
        this.stopDistance = stopDistance;
    }

    public override NodeState Evaluate()
    {
        float distance = Vector3.Distance(body.transform.position, target.position);
        
        // 如果已经到了攻击距离，返回成功，让 Sequence 继续往下走（比如去执行攻击）
        if (distance <= stopDistance)
        {
            body.TryExecuteCommand(new MoveCommand(Vector2.zero));
            Vector3 face = target.position - body.transform.position;
            face.y = 0f;
            if (face.sqrMagnitude > 0.001f)
                body.RotateYaw(face.normalized, body.Config != null ? body.Config.RotationSpeed : 720f);
            return NodeState.Success;
        }

        Vector3 dir3D = (target.position - body.transform.position).normalized;
        Vector2 moveDir = new Vector2(dir3D.x, dir3D.z);
        body.MoveUsesWorldDir = true;
        body.TryExecuteCommand(new MoveCommand(moveDir));

        return NodeState.Running; 
    }
}