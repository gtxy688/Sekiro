public class GroundedState : HierarchicalState
{
    public GroundedState(CharacterBody body) : base(body) { }

    protected override BaseState GetInitialSubState()
    {
        return new IdleState(body, this); // 默认待机
    }

    // 负责处理父层级的状态切换,内部层级切换交由子状态去处理
    public override void OnUpdate()
    {
        // 踩空掉落（这属于物理环境变化,不需要去判断能否执行,不属于Command，所以保留在Update里）
        if (!body.IsGrounded)
        {
            body.MainStateMachine.ChangeState(new AirborneState(body,false));
            return;
        }

        base.OnUpdate(); // 执行子状态 (Idle 或 Move)
    }

    //所有的地面状态，都共用这个跳跃逻辑！
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        // 拦截跳跃指令
        if (cmd is JumpCommand)
        {
            // 无论子状态是 Idle 还是 Move，父类直接掐断，强切大状态！
            body.MainStateMachine.ChangeState(new AirborneState(body, true));
            return true; // 报告大脑：跳跃指令已执行！
        }

        return false; // 不是跳跃，抛给子状态(Idle/Move)去处理。
    }
}