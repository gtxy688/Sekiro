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
        if (!body.IsGrounded && !body.IsPostureBroken)
        {
            body.MainStateMachine.ChangeState(new AirState(body));
            return;
        }

        base.OnUpdate(); // 执行子状态 (Idle 或 Move)
    }

    //所有的地面状态，都共用这个跳跃逻辑！
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        // 崩解倒地由 StaggerBrokenState 吞命令。父层若先切跳跃/喝药，
        // RecoverFromBreak 永远不会跑，架势条会卡满且无法忍杀。
        if (body.IsPostureBroken)
        {
            return false;
        }

        // 喝药期间只有移动会下钻到 HealState；跳跃和重复喝药在父层直接吞掉。
        if (body.IsHealing && (cmd is JumpCommand || cmd is HealCommand))
        {
            return true;
        }

        // 攻击命中段不能被跳跃/喝药强切；命令保持在 0.2s 缓冲中等待后摇。
        if (body.IsAttacking &&
            !body.IsAttackRecoveryOpen &&
            (cmd is JumpCommand || cmd is HealCommand))
        {
            return false;
        }

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

        // M10：只有玩家的攻击指令才查处决。Boss 的 AttackCommand 下钻到子状态。
        // 正在出招时不抢：崩解那一刀不能直接变成处决，必须是新一次攻击。
        if (cmd is AttackCommand &&
            !body.IsAttacking &&
            CombatManager.Instance != null &&
            body == CombatManager.Instance.PlayerRef &&
            CombatManager.Instance.TryExecuteFinisher(body, FinisherKind.Ground))
        {
            return true;
        }

        return false; // 不是跳跃，抛给子状态(Idle/Move)去处理。
    }
}
