using UnityEngine;


public class AirborneState : HierarchicalState
{
    private bool isJumping;

    public AirborneState(CharacterBody body, bool isJumping) : base(body) 
    {
        this.isJumping = isJumping;
    }

    protected override BaseState GetInitialSubState()
    {
        if (isJumping) return new JumpState(body, this);
        else return new FallState(body, this);
    }

    public override void OnUpdate()
    {
        //只要碰地,直接切回地面,不属于command
        if (body.IsGrounded)
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
            return;
        }

        base.OnUpdate();
    }
    // 没有其他要拦截的了,直接返回false
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return false;
    }
}