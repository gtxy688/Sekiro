public class GroundedState : HierarchicalState
{
    // 强制切入时指定的初始子状态（被弹反硬直/崩解倒地/喝药等物理覆写场景）
    // null 则走默认 Idle
    private readonly BaseState forcedInitialSubState;

    public GroundedState(CharacterBody body, BaseState forcedInitialSubState = null) : base(body)
    {
        this.forcedInitialSubState = forcedInitialSubState;
    }

    protected override BaseState GetInitialSubState()
    {
        return forcedInitialSubState ?? new IdleState(body, this);
    }

    // 负责处理父层级的状态切换,内部层级切换交由子状态去处理
    public override void OnUpdate()
    {
        // 踩空掉落（这属于物理环境变化,不需要去判断能否执行,不属于Command，所以保留在Update里）
        if (!body.IsGrounded)
        {
            body.MainStateMachine.ChangeState(new AirState(body));
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
            // 跳跃位移由 Jump 动画 Root 曲线驱动（全权根运动）
            body.MainStateMachine.ChangeState(new AirState(body));
            return true; // 报告大脑：跳跃指令已执行！
        }

        // M16：拦截葫芦指令 → 切喝药状态（播动画 + 可被打断硬直）
        if (cmd is HealCommand)
        {
            SubStateMachine.ChangeState(new HealState(body, this));
            return true;
        }

        // M10：拦截攻击指令时先查处决机会（Boss 崩解 + 距离近 → 处决优先于普通攻击）
        if (cmd is AttackCommand)
        {
            if (CombatManager.Instance != null && CombatManager.Instance.TryExecuteFinisher(body))
            {
                return true; // 触发处决，消耗指令
            }
        }

        return false; // 不是跳跃，抛给子状态(Idle/Move)去处理。
    }
}
