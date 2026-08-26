using UnityEngine;

// 回生待机（M14）：Dead 倒地 → Deading 躺地等待；
// 按攻击键 → Revive 爬起；超时未确认 → 真死（已躺着，不再重播倒地）
public class RevivePendingState : BaseState
{
    private HierarchicalState parent;
    private float timer;
    private float timeout = 3f;
    private float fallTimer;
    private float fallDuration = 1.2f;
    private float reviveTimer;
    private float reviveDuration = 1.5f;
    private bool lying;
    private bool reviving;

    public RevivePendingState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        timer = 0f;
        fallTimer = 0f;
        reviveTimer = 0f;
        lying = false;
        reviving = false;
        AnimUtil.TryCrossFade(body.Animator, "Dead", 0.1f);
    }

    public override void OnUpdate()
    {
        if (reviving)
        {
            reviveTimer += Time.deltaTime;
            if (reviveTimer >= reviveDuration)
            {
                body.MainStateMachine.ChangeState(new GroundedState(body));
            }
            return;
        }

        timer += Time.deltaTime;

        if (!lying)
        {
            fallTimer += Time.deltaTime;
            if (IsFallFinished())
            {
                lying = true;
                AnimUtil.TryCrossFade(body.Animator, "Deading", 0.05f);
            }
        }

        if (timer >= timeout)
        {
            CombatEventBus.TriggerDeath(body);
            body.MainStateMachine.ChangeState(new DeadState(body, false, alreadyDowned: true));
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (reviving) return true;

        if (cmd is AttackCommand)
        {
            body.Revive();
            reviving = true;
            reviveTimer = 0f;
            AnimUtil.TryCrossFade(body.Animator, "Revive", 0.1f);
            return true;
        }
        return true;
    }

    private bool IsFallFinished()
    {
        var info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, "Dead") && info.normalizedTime >= 0.95f) return true;
        return fallTimer >= fallDuration;
    }
}
