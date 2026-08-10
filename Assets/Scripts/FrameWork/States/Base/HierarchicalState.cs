// 复合状态/父状态 
public abstract class HierarchicalState : BaseState
{
    // 父状态内部自己养的“子状态机”
    public StateMachine SubStateMachine { get; private set; } = new StateMachine();

    public HierarchicalState(CharacterBody body) : base(body) { }

    // 要求子类必须提供一个默认进入的子状态
    protected abstract BaseState GetInitialSubState();

    public override void OnEnter()
    {
        // 自动切入默认子状态
        BaseState initial = GetInitialSubState();
        if (initial != null)
        {
            SubStateMachine.ChangeState(initial);
        }
    }

    public override void OnUpdate()
    {
        // 如果父状态自己没被切走，就驱动当前的子状态更新
        SubStateMachine.Update();
    }

    public override void OnExit()
    {
        // 强制退出当前正在运行的子状态
        SubStateMachine.ChangeState(null);
    }

    // 命令的路由转发 (父类 -> 子类)
    public override bool HandleCommand(ICommand cmd)
    {
        // 第一步：父状态自己先看看要不要拦截这个命令？
        // (交由具体的父状态子类去实现 OnParentHandleCommand)
        if (OnParentHandleCommand(cmd))
        {
            return true; // 父类拦截了！停止传递。
        }

        // 第二步：如果父类不关心，往下抛给当前正在运行的子状态
        if (SubStateMachine.CurrentState != null)
        {
            return SubStateMachine.CurrentState.HandleCommand(cmd);
        }

        return false;
    }

    // 留给具体父状态去实现拦截逻辑
    protected virtual bool OnParentHandleCommand(ICommand cmd) 
    { 
        return false; 
    }
}