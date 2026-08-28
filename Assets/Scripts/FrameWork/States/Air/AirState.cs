using UnityEngine;

// 空中父状态：子状态按 Jump → Jumping → Fall 切动画；空中刀是独立叶子。
// 根运动保持开启（XZ 跟贴图）；高度走 JumpSpeed + 重力。
public class AirState : HierarchicalState
{
    public AirState(CharacterBody body) : base(body) { }

    public override void OnEnter()
    {
        body.IsAirborne = true;
        // 每次进空重置：同一滞空只能 Jump2 一次，落地再跳重新给
        body.ResetAirJump2();
        base.OnEnter();
    }

    public override void OnExit()
    {
        body.IsAirborne = false;
        base.OnExit();
    }

    protected override BaseState GetInitialSubState()
    {
        return new AirIdleState(body, this);
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (SubStateMachine.CurrentState is AirAttackState airAtk && airAtk.WantsImmediateLand)
        {
            LeaveAir();
            return;
        }

        AirIdleState air = SubStateMachine.CurrentState as AirIdleState;
        if (air == null || !air.CanLeaveAir) return;
        LeaveAir();
    }

    private void LeaveAir()
    {
        if (body.IsPostureBroken)
        {
            body.MainStateMachine.ChangeState(
                new GroundedState(body, new StaggerBrokenState(body)));
        }
        else
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    // 没有其他要拦截的了，直接返回 false
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return false;
    }
}
