using UnityEngine;

// 空中受击子状态：腾空被打的硬直，结束后按是否落地决定去向
public class AirStunnedState : BaseState
{
    private float stunTimer;

    public AirStunnedState(CharacterBody body, HierarchicalState parent) : base(body) { }

    public override void OnEnter()
    {
        stunTimer = 0f;

        // 播空中受击动画（M5 接动画前为占位名）
        body.Animator.CrossFade("Hurt_Air", 0.05f);
    }

    public override void OnUpdate()
    {
        stunTimer += Time.deltaTime;

        if (stunTimer >= body.stunDuration)
        {
            // 硬直结束时看物理状态：落地回地面，没落地继续下落
            if (body.IsGrounded)
            {
                body.MainStateMachine.ChangeState(new GroundedState(body));
            }
            else
            {
                body.MainStateMachine.ChangeState(new AirState(body, false));
            }
        }
    }
}
