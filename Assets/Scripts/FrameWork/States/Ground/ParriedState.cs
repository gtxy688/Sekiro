using UnityEngine;

// 被完美弹反后的硬直（M4）：攻击者被弹开，播 Deflected，动画结束回待机。
// 由 CharacterBody.ForceParryStun 强切（顶层走 GroundedState 初始子状态）。
public class ParriedState : BaseState
{
    private const string DeflectedAnim = "Deflected";

    private float timer;
    private float duration;
    private bool hasSeenAnim;

    public ParriedState(CharacterBody body) : base(body)
    {
        duration = body.Config != null ? body.Config.ParriedDuration : 0.35f;
    }

    public override void OnEnter()
    {
        timer = 0f;
        hasSeenAnim = false;
        body.IsParried = true;

        if (!AnimUtil.HasState(body.Animator, DeflectedAnim))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少被弹反状态：{DeflectedAnim}");
            body.Animator.CrossFade(body.ResolveHurtAnim(HurtContext.Deflected), 0.05f);
            return;
        }

        body.Animator.CrossFade(DeflectedAnim, 0.05f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, DeflectedAnim))
        {
            hasSeenAnim = true;
            if (info.normalizedTime >= 0.95f)
            {
                body.MainStateMachine.ChangeState(new GroundedState(body));
                return;
            }
        }
        else if (hasSeenAnim && !body.Animator.IsInTransition(0))
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
            return;
        }

        // 没接到 Deflected 时用配置时长兜底，避免卡死
        if (!hasSeenAnim && timer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    // 硬直期间吞掉所有命令
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    public override void OnExit()
    {
        body.IsParried = false;
    }
}
