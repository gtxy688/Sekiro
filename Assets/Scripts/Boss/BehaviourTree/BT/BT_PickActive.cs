using UnityEngine;

// 主动层：按距离加权抽招并播完。Busy 时续跑，不因距离微调取消。
public class BT_PickActive : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;

    public BT_PickActive(CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor)
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
        if (executor.IsBusy) return executor.Evaluate();
        if (body.IsPostureBroken || body.IsParried) return NodeState.Failure;
        float dist = Vector3.Distance(body.transform.position, target.position);
        // 对齐参考文档：>7m 仍有远程招可抽（Bow_ThenSlash/Bow_Shot/Slash_Rush2，minRange 7/7/5），
        // 距离档完全由 BossMovePicker 的 minRange/maxRange 过滤；全冷却或缺状态抽不到才落回追击。
        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Active, body, body.Animator, blackboard, dist);
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }
}
