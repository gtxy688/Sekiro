// 玩家喝药时打重箭。自身记住执行器，避免 Sequence 丢掉 Running。
public class BT_HealPunish : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly CharacterBody player;
    private readonly BossMoveTable table;
    private readonly BT_ExecuteMove executor;

    public BT_HealPunish(CharacterBody body, BossMoveTable table, CharacterBody player, BT_ExecuteMove executor)
    {
        this.body = body;
        this.table = table;
        this.player = player;
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
        if (body.IsAttacking || body.IsParried) return NodeState.Failure;
        if (player == null || !player.IsHealing) return NodeState.Failure;
        BossMoveEntry heavy = table.FindById("Bow_Heavy");
        if (heavy == null) return NodeState.Failure;
        if (blackboard != null && blackboard.IsOnCooldown(heavy.id, heavy.cooldown))
            return NodeState.Failure;
        return executor.Begin(heavy);
    }
}
