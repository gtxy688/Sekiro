using UnityEngine;

// 地面受击子状态：双脚着地被打的硬直，结束后回地面
public class GroundStunnedState : BaseState
{
    private float stunTimer;

    public GroundStunnedState(CharacterBody body, HierarchicalState parent) : base(body) { }

    public override void OnEnter()
    {
        stunTimer = 0f;

        // 播地面受击动画（M5 接动画前为占位名）
        body.Animator.CrossFade("Hurt_Ground", 0.05f);
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
