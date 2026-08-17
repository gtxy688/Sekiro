using UnityEngine;

// 被完美弹反后的硬直（M4）：攻击者被弹开，播被弹反动画（HurtContext.Deflected），
// 短暂不可动后回待机。动画名走 CharacterConfig 受击动画映射（接口预留）
// 由 CharacterBody.ForceParryStun 强切（顶层走 GroundedState 初始子状态），
// 结束直接切顶层 GroundedState，不依赖 parent 引用
public class ParriedState : BaseState
{
    private float timer;
    private float duration;

    public ParriedState(CharacterBody body) : base(body)
    {
        duration = body.Config != null ? body.Config.ParriedDuration : 0.35f;
    }

    public override void OnEnter()
    {
        timer = 0f;
        // 被弹反动画：HurtContext.Deflected 映射（留空则回退普通受击动画）
        body.Animator.CrossFade(body.ResolveHurtAnim(HurtContext.Deflected), 0.05f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    // 硬直期间吞掉所有命令
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }
}
