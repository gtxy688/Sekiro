using UnityEngine;

// 玩家喝药时打重箭。Boss 空闲且未在出招时才记惩罚；正在打别的招则直接放弃，不存储。
public class BT_HealPunish : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly CharacterBody player;
    private readonly BossMoveTable table;
    private readonly BT_ExecuteMove executor;

    private float punishAt = -1f;

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
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            punishAt = -1f;
            executor.ResetMove();
            return NodeState.Failure;
        }
        if (executor.IsBusy) return executor.Evaluate();

        if (player == null || player.IsDowned)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }

        // 喝药期间 Boss 若在出招：不记、不存，这次惩罚作废。
        if (player.IsHealing)
        {
            if (body.IsAttacking)
            {
                punishAt = -1f;
                return NodeState.Failure;
            }
            if (punishAt < 0f)
                ArmIfPlayerHealing();
        }

        if (punishAt < 0f) return NodeState.Failure;

        if (body.IsAttacking)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }

        if (Time.time < punishAt)
            return NodeState.Running;

        if (table == null)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }
        BossMoveEntry heavy = table.FindById("Bow_Heavy");
        if (heavy == null)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }
        punishAt = -1f;
        return executor.Begin(heavy, interruptCurrent: false);
    }

    public void ArmIfPlayerHealing()
    {
        if (punishAt >= 0f) return;
        if (player == null || !player.IsHealing || player.IsDowned) return;
        if (body == null || body.IsParried || body.IsPostureBroken || body.IsFinisherLocked) return;
        // 喝药瞬间 Boss 已在出招 → 不射重箭，也不排队。
        if (body.IsAttacking) return;
        float delay = body.Config != null ? Mathf.Max(0f, body.Config.HealPunishDelay) : 0f;
        punishAt = Time.time + delay;
    }
}
