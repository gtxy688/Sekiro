using UnityEngine;

// 空中父状态：子状态按 Jump → Jumping → Fall 切动画。
// 根运动保持开启（XZ 跟贴图）；高度走 JumpSpeed + 重力。
public class AirState : HierarchicalState
{
    public AirState(CharacterBody body) : base(body) { }

    public override void OnEnter()
    {
        body.IsAirborne = true;
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

        AirIdleState air = SubStateMachine.CurrentState as AirIdleState;
        if (air == null || !air.CanLeaveAir) return;

        // Fall 播完（或崩解落地跳过 Fall）再回地面。
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
