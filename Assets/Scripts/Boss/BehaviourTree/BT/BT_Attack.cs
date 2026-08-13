using UnityEngine;
// 具体的动作节点：执行攻击 (读取 HFSM 状态)
public class BT_Attack : Node
{
    private CharacterBody body;

    public BT_Attack(CharacterBody body)
    {
        this.body = body;
    }

    public override NodeState Evaluate()
    {
        // 尝试发送攻击指令
        bool isAccepted = body.TryExecuteCommand(new AttackCommand());

        if (isAccepted)
        {
            // 身体接受了指令，下一帧就会切入 AttackState。这一帧先返回 Running
            return NodeState.Running;
        }
        else
        {
            // 身体拒绝了指令（比如正在受击硬直 StunnedState），返回 Failure
            return NodeState.Failure;
        }
    }
}