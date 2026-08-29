using UnityEngine;

// 被完美弹反 / 被识破后的硬直：攻击者被打断，播指定 Clip，动画结束回待机。
// 由 CharacterBody.ForceParryStun / ForceMikiriStun 强切（顶层走 GroundedState 初始子状态）。
public class ParriedState : BaseState
{
    private const string DefaultAnim = "Deflected";

    private readonly string animName;
    private readonly bool freezeYawAfterExit;
    private float timer;
    private float duration;
    private bool hasSeenAnim;
    private bool triedNestedPath;

    public ParriedState(CharacterBody body, string animName = null, bool freezeYawAfterExit = false) : base(body)
    {
        this.animName = string.IsNullOrEmpty(animName) ? DefaultAnim : animName;
        this.freezeYawAfterExit = freezeYawAfterExit;
        duration = body.Config != null ? body.Config.ParriedDuration : 0.35f;
    }

    public override void OnEnter()
    {
        timer = 0f;
        hasSeenAnim = false;
        triedNestedPath = false;
        body.IsParried = true;
        // 钉住打断当下的朝向。识破还会把冻结留到下一招（freezeYawAfterExit）。
        body.FreezeCombatYaw();

        if (!AnimUtil.TryPlay(body.Animator, animName))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少硬直状态：{animName}");
            AnimUtil.TryCrossFade(body.Animator, body.ResolveHurtAnim(HurtContext.Deflected), 0.05f);
        }
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        AnimatorStateInfo next = body.Animator.IsInTransition(0)
            ? body.Animator.GetNextAnimatorStateInfo(0)
            : info;
        if (!triedNestedPath && !AnimUtil.IsPlaying(info, animName)
            && !AnimUtil.IsPlaying(next, animName) && timer > 0.05f)
        {
            triedNestedPath = true;
            AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
            info = body.Animator.GetCurrentAnimatorStateInfo(0);
            next = body.Animator.IsInTransition(0)
                ? body.Animator.GetNextAnimatorStateInfo(0)
                : info;
        }

        bool animDone = false;
        if (AnimUtil.IsPlaying(info, animName) || AnimUtil.IsPlaying(next, animName))
        {
            hasSeenAnim = true;
            if (AnimUtil.IsPlaying(info, animName) && info.normalizedTime >= 0.95f)
                animDone = true;
        }
        else if (hasSeenAnim && !body.Animator.IsInTransition(0))
        {
            animDone = true;
        }

        // 硬直 = max(动画, 配置下限)：ParriedDuration 是最小硬直。
        if (animDone && timer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
            return;
        }

        if (!hasSeenAnim && timer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    public override void OnExit()
    {
        body.IsParried = false;
        if (freezeYawAfterExit)
        {
            return;
        }

        body.ClearCombatYawFrozen();
        body.SetSuppressRootYaw(false);
    }
}
