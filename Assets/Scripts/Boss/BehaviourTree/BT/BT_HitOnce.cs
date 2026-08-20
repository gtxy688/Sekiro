using UnityEngine;

// 简单进攻：出一刀，等这一刀的 AttackState 结束再报成功并写冷却。
// 给「隔一会打一下」用，不走连段。
public class BT_HitOnce : Node
{
    private readonly CharacterBody body;
    private bool started;

    public BT_HitOnce(CharacterBody body)
    {
        this.body = body;
    }

    public override NodeState Evaluate()
    {
        if (!started)
        {
            body.ActiveAttack = (body.AttackSet != null && body.AttackSet.Length > 0)
                ? body.AttackSet[0]
                : body.LightAttack;

            if (!body.TryExecuteCommand(new AttackCommand()))
                return NodeState.Failure;

            started = true;
            return NodeState.Running;
        }

        if (body.IsAttacking)
            return NodeState.Running;

        started = false;
        blackboard?.SetCooldown("attack");
        return NodeState.Success;
    }
}
