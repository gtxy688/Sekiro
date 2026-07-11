/// <summary>
/// 状态基类，定义生命周期方法。
/// 所有具体状态必须继承此类并重写生命周期方法。
/// </summary>
public abstract class State
{
    /// <summary>
    /// 所属状态机引用，由 Initialize 方法设置。
    /// 子类可通过此字段访问状态机并触发状态转换。
    /// </summary>
    protected StateMachine _stateMachine;

    /// <summary>
    /// 初始化状态，设置所属状态机引用。
    /// 由 StateMachine.AddState 自动调用，不应手动调用。
    /// </summary>
    /// <param name="stateMachine">所属的状态机实例</param>
    public void Initialize(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
    }

    /// <summary>
    /// 进入状态时调用。用于初始化状态相关资源、播放动画等。
    /// </summary>
    public virtual void Enter() { }

    /// <summary>
    /// 每帧执行状态逻辑。由 StateMachine.Update 调用。
    /// </summary>
    public virtual void Execute() { }

    /// <summary>
    /// 退出状态时调用。用于清理资源、重置状态等。
    /// </summary>
    public virtual void Exit() { }
}
