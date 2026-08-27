using UnityEngine;

// 被完美弹刀后：硬直中等待，结束后在交锋距离内抽还击招。
public class BT_Kengeki : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;

    public BT_Kengeki(CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor)
    {
        this.body = body;
        this.table = table;
        this.target = target;
        this.executor = executor;
    }

    public override void SetBlackboard(Blackboard bb)
    {
        base.SetBlackboard(bb);
        executor?.SetBlackboard(bb);
    }

    public override NodeState Evaluate()
    {
        if (IsTargetHealing(target))
        {
            executor?.ResetMove();
            return NodeState.Failure;
        }

        if (body.IsPostureBroken) return NodeState.Failure;
        // 硬直中先占住 Selector，避免 Busy 的交锋招 Evaluate 失败后落到走位。
        if (body.IsParried)
        {
            executor?.ResetMove();
            return NodeState.Running;
        }
        if (executor != null && executor.IsBusy)
            return executor.Evaluate();

        if (!body.KengekiArmed) return NodeState.Failure;

        float dist = Vector3.Distance(body.transform.position, target.position);
        if (dist > table.kengekiMaxRange)
        {
            body.KengekiArmed = false;
            return NodeState.Failure;
        }

        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Kengeki, body, body.Animator, blackboard, dist);
        body.KengekiArmed = false;
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }

    private static bool IsTargetHealing(Transform target)
    {
        if (target == null) return false;
        CharacterBody player = target.GetComponent<CharacterBody>();
        if (player == null) player = target.GetComponentInParent<CharacterBody>();
        return player != null && player.IsHealing;
    }
}
