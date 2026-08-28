using UnityEngine;

// Hurt_Mid 倒地结束前按下防御：播起身进防，播完按住则举刀，松开则 Idle。
public class MidToGuardState : BaseState
{
    private const float FallbackDuration = 1.2f;

    private string animName;
    private float timer;
    private bool released;
    private bool seenStart;
    private bool waitForAnim;

    public MidToGuardState(CharacterBody body) : base(body) { }

    public override void OnEnter()
    {
        body.IsGuarding = true;
        released = false;
        timer = 0f;
        seenStart = false;
        animName = HitReactionUtil.MidToGuardAnim(body);
        waitForAnim = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
    }

    public override void OnExit()
    {
        body.IsGuarding = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is DeflectCommand)
        {
            released = false;
            return true;
        }
        if (cmd is IdleCommand)
        {
            released = true;
            return true;
        }
        return true;
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (waitForAnim && body.Animator != null)
        {
            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, animName))
            {
                seenStart = true;
                if (info.normalizedTime >= 0.95f)
                    Finish();
                return;
            }

            if (seenStart || timer >= FallbackDuration)
                Finish();
            return;
        }

        if (timer >= FallbackDuration)
            Finish();
    }

    // 起身防还没进举刀循环：被打按新受击打断。
    public override bool OnHitReceived(HitData hit)
    {
        return false;
    }

    void Finish()
    {
        GroundedState ground = body.MainStateMachine.CurrentState as GroundedState;
        if (ground == null) return;
        if (released)
            ground.SubStateMachine.ChangeState(new IdleState(body, ground));
        else
            ground.SubStateMachine.ChangeState(new DeflectState(body, ground));
    }
}
