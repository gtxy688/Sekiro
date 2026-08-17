using UnityEngine;

// 地面受击子状态：双脚着地被打的硬直，结束后回地面
// 受击动画按 HurtContext 从 CharacterConfig 映射（受击表现接口，留空回退普通受击）
public class GroundStunnedState : BaseState
{
    private float stunTimer;
    private readonly HurtContext context;

    public GroundStunnedState(CharacterBody body, HierarchicalState parent, HurtContext context) : base(body)
    {
        this.context = context;
    }

    public override void OnEnter()
    {
        stunTimer = 0f;

        // 受击动画：按语境解析（Normal/Heavy 击飞/Guard 格挡/Deflected 被弹反）
        body.Animator.CrossFade(body.ResolveHurtAnim(context), 0.05f);
    }

    public override void OnUpdate()
    {
        stunTimer += Time.deltaTime;

        if (stunTimer >= body.StunDuration)
        {
            // 硬直结束，退出受击父状态，回到地面
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }
}
