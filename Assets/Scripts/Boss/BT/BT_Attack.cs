using UnityEngine;
// 具体的动作节点：执行攻击 (读取 HFSM 状态)
public class BT_Attack : Node
{
    private CharacterBody body;
    private bool isHeavy;

    public BT_Attack(CharacterBody body, bool isHeavy = false)
    {
        this.body = body;
        this.isHeavy = isHeavy;
    }

    public override NodeState Evaluate()
    {
        // // 1. 【状态握手】如果身体目前已经在攻击状态了，说明攻击正在进行中
        // if (body.MainStateMachine.CurrentState is AttackState) // 假设你的攻击大状态叫 AttackState
        // {
        //     return NodeState.Running; // 告诉行为树：别催，正在砍呢，挂起等待！
        // }

        // // 2. 如果身体不在攻击状态，说明这是第一次调用，或者攻击刚刚结束
        
        // // 尝试发送攻击指令
        // bool isAccepted = body.TryExecuteCommand(new AttackCommand { IsHeavy = this.isHeavy });
        
        // if (isAccepted)
        // {
        //     // 身体接受了指令，下一帧就会切入 AttackState。这一帧先返回 Running。
        //     return NodeState.Running;
        // }
        // else
        // {
        //     // 如果身体拒绝了指令（比如正在受击硬直 StunnedState），
        //     // 那么攻击失败，返回 Failure。
        //     return NodeState.Failure;
        // }
        return NodeState.Success; // 这里暂时直接返回成功，方便测试行为树
    }
}