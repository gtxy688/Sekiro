using UnityEngine;

// 地面受击子状态：双脚着地被打的硬直，结束后回地面
// 受击动画按 HurtContext 从 CharacterConfig 映射（受击表现接口，留空回退普通受击）
public class GroundStunnedState : BaseState
{
    private const float HeavyFallbackDuration = 2.5f;

    private float stunTimer;
    private readonly HurtContext context;
    private string animName;
    private float duration;
    private bool waitForAnim;
    private bool seenStart;

    public GroundStunnedState(CharacterBody body, HierarchicalState parent, HurtContext context) : base(body)
    {
        this.context = context;
    }

    public override void OnEnter()
    {
        stunTimer = 0f;
        seenStart = false;
        animName = body.ResolveHurtAnim(context);
        bool played = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);

        // 破防后倒地受击跟 Hurt_Heavy 播完走，不要被普通 StunDuration（0.5s）掐掉
        waitForAnim = context == HurtContext.Heavy && played;
        duration = waitForAnim ? HeavyFallbackDuration : body.StunDuration;
        if (!played)
        {
            AnimUtil.TryCrossFade(body.Animator, body.ResolveHurtAnim(HurtContext.Normal), 0.05f);
        }
    }

    public override void OnUpdate()
    {
        stunTimer += Time.deltaTime;

        if (waitForAnim && body.Animator != null)
        {
            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, animName))
            {
                if (info.normalizedTime < 0.5f)
                {
                    seenStart = true;
                    if (info.length > 0.05f)
                    {
                        duration = Mathf.Max(body.StunDuration, info.length);
                    }
                }

                if (seenStart && info.normalizedTime >= 0.99f && !body.Animator.IsInTransition(0))
                {
                    body.MainStateMachine.ChangeState(new GroundedState(body));
                    return;
                }
            }

            if (stunTimer >= duration)
            {
                body.MainStateMachine.ChangeState(new GroundedState(body));
            }
            return;
        }

        if (stunTimer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }
}
