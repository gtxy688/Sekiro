using UnityEngine;

// 按表行顺序播完各段动画。五连垫步结束后有概率改打重箭。
public class BT_ExecuteMove : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;

    private BossMoveEntry entry;
    private BossAnimSequence sequence;
    private int segment;
    private bool started;
    private bool waitingAttack;
    private bool allowInterrupt;

    public bool IsBusy => started;

    public BT_ExecuteMove(CharacterBody body, BossMoveTable table)
    {
        this.body = body;
        this.table = table;
    }

    public void ResetMove()
    {
        if (started && body != null)
        {
            body.CurrentMoveEntry = null;
            body.CurrentMoveWindow = null;
        }
        started = false;
        waitingAttack = false;
        allowInterrupt = false;
        entry = null;
        sequence = null;
        segment = 0;
    }

    public NodeState Begin(BossMoveEntry move, bool interruptCurrent = false)
    {
        ResetMove();
        if (move == null) return NodeState.Failure;
        sequence = BossMovePicker.ChooseSequence(move, body.Animator);
        if (sequence == null) return NodeState.Failure;
        entry = move;
        started = true;
        segment = 0;
        allowInterrupt = interruptCurrent;
        return FireCurrentSegment();
    }

    public override NodeState Evaluate()
    {
        if (!started) return NodeState.Failure;

        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            ResetMove();
            return NodeState.Failure;
        }

        if (waitingAttack)
        {
            if (body.IsAttacking) return NodeState.Running;
            waitingAttack = false;
            segment++;
            TryInterruptAir5();
            if (sequence == null || segment >= sequence.states.Length)
            {
                blackboard?.SetCooldown(entry.id);
                ResetMove();
                return NodeState.Success;
            }
            return FireCurrentSegment();
        }

        return FireCurrentSegment();
    }

    private void TryInterruptAir5()
    {
        if (entry == null) return;
        if (entry.id != "Bow_Air5" && entry.id != "Kengeki_Air5") return;
        if (segment <= 0) return;
        float chance = table != null ? table.air5HeavyInterruptChance : 0f;
        if (Random.value > chance) return;
        BossMoveEntry heavy = table != null ? table.FindById("Bow_Heavy") : null;
        if (heavy == null) return;
        if (blackboard != null && blackboard.IsOnCooldown(heavy.id, heavy.cooldown)) return;
        if (!BossMovePicker.AnySequencePlayable(heavy, body.Animator)) return;

        blackboard?.SetCooldown(entry.id);
        entry = heavy;
        sequence = BossMovePicker.ChooseSequence(heavy, body.Animator);
        segment = 0;
    }

    private NodeState FireCurrentSegment()
    {
        string anim = sequence.states[segment];
        BossMoveWindow w = BossMovePicker.WindowFor(entry, segment);
        body.CurrentMoveEntry = entry;
        body.CurrentMoveWindow = w;
        AttackConfig baked = BossAttackBaker.Bake(entry, anim, w);
        body.ActiveAttack = baked;
        if (!body.StartAttack(baked, allowInterrupt))
        {
            ResetMove();
            return NodeState.Failure;
        }
        waitingAttack = true;
        return NodeState.Running;
    }
}
