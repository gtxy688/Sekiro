using UnityEngine;

// Mid/Heavy 倒地（含躺地）播完后的起身。期间挨刀按新的一次受击处理。
public class StandingState : BaseState
{
    private const float FallbackDuration = 2f;

    private string animName;
    private float timer;
    private bool seenStart;
    private bool waitForAnim;

    public StandingState(CharacterBody body) : base(body) { }

    public override void OnEnter()
    {
        timer = 0f;
        seenStart = false;
        animName = HitReactionUtil.StandingAnim(body);
        waitForAnim = AnimUtil.TryCrossFade(body.Animator, animName, 0.08f);
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
                if (info.normalizedTime >= 0.99f && !body.Animator.IsInTransition(0))
                {
                    GoIdle();
                    return;
                }
            }

            if (timer >= FallbackDuration)
                GoIdle();
            return;
        }

        GoIdle();
    }

    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    // 已经算站起来：不要拦截，让 ReceiveHit 按新一击完整播。
    public override bool OnHitReceived(HitData hit)
    {
        return false;
    }

    void GoIdle()
    {
        GroundedState ground = body.MainStateMachine.CurrentState as GroundedState;
        if (ground != null)
            ground.SubStateMachine.ChangeState(new IdleState(body, ground));
        else
            body.MainStateMachine.ChangeState(new GroundedState(body));
    }
}
