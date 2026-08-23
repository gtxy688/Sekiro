using UnityEngine;

// 空中父状态：跳跃/下落共用一个子状态（AirIdleState）
// 全权根运动：跳跃上升由 Jump 动画 Root 曲线驱动，代码不给初速度
public class AirState : HierarchicalState
{
    public AirState(CharacterBody body) : base(body) { }

    protected override BaseState GetInitialSubState()
    {
        return new AirIdleState(body, this);
    }

    public override void OnUpdate()
    {
        // 只要碰地，直接切回地面，不属于 command。
        // 崩解中若被跳走，落地必须回到倒地，不能进 Idle 却仍 IsPostureBroken。
        if (body.IsGrounded)
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
            return;
        }

        base.OnUpdate();
    }

    // 没有其他要拦截的了，直接返回 false
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return false;
    }
}
