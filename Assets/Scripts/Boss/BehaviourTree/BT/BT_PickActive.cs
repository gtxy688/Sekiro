using UnityEngine;

// 主动层：按距离加权抽招并播完。Busy 时续跑，不因距离微调取消。
public class BT_PickActive : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;
    private readonly float roamAfterAttack;
    private readonly float roamAfterAttackJitter;

    public BT_PickActive(
        CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor,
        float roamAfterAttack = 2.5f, float roamAfterAttackJitter = 1.2f)
    {
        this.body = body;
        this.table = table;
        this.target = target;
        this.executor = executor;
        this.roamAfterAttack = roamAfterAttack;
        this.roamAfterAttackJitter = roamAfterAttackJitter;
    }

    public override void SetBlackboard(Blackboard bb)
    {
        base.SetBlackboard(bb);
        executor?.SetBlackboard(bb);
    }

    public override NodeState Evaluate()
    {
        if (BTUtil.IsTargetIncapacitated(target))
        {
            executor.ResetMove();
            return NodeState.Failure;
        }

        // 必须先于 IsBusy：突刺播到一半被识破时 executor 仍 Busy，旧顺序会
        // Evaluate→Failure，Selector 解锁后落到走位。
        if (body.IsPostureBroken || body.IsParried)
        {
            executor.ResetMove();
            return NodeState.Failure;
        }
        if (executor.IsBusy)
        {
            NodeState busy = executor.Evaluate();
            if (busy != NodeState.Running)
                ArmRoamGap();
            return busy;
        }

        bool isRoamGapActive = IsRoamGapActive();
        if (isRoamGapActive)
            return NodeState.Failure;

        float dist = Vector3.Distance(body.transform.position, target.position);
        // 对齐参考文档：>7m 仍有远程招可抽（Bow_ThenSlash/Bow_Shot/Slash_Rush2，minRange 7/7/5），
        // 距离档完全由 BossMovePicker 的 minRange/maxRange 过滤；全冷却或缺状态抽不到才落回追击。
        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Active, body, body.Animator, blackboard, dist);
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }

    private void ArmRoamGap()
    {
        if (blackboard == null) return;
        float gap = roamAfterAttack + UnityEngine.Random.Range(0f, Mathf.Max(0f, roamAfterAttackJitter));
        blackboard.ActiveGapDuration = gap;
        blackboard.SetCooldown("active_gap");
    }

    private bool IsRoamGapActive()
    {
        if (blackboard == null) return false;
        float gap = blackboard.ActiveGapDuration;
        if (gap <= 0.01f) gap = roamAfterAttack;
        return blackboard.IsOnCooldown("active_gap", gap);
    }
}
