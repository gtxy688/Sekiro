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
        bool animDone = false;
        if (AnimUtil.IsPlaying(info, DeflectedAnim))
        {
            hasSeenAnim = true;
            if (info.normalizedTime >= 0.95f) animDone = true;
        }
        else if (hasSeenAnim && !body.Animator.IsInTransition(0))
        {
            animDone = true;
        }

        // 硬直 = max(被弹动画, 配置下限)：ParriedDuration 是"最小硬直"而非"没动画的兜底"。
        // 只有配置下限到位，弹反方（Boss 弹反玩家时）才能在玩家恢复前完成收刀+反击出手，
        // 回合制才成立：被弹方稳定被压出一段反击窗口。
        if (animDone && timer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
            return;
        }

        // 没接到 Deflected 动画时用配置时长兜底，避免卡死
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
