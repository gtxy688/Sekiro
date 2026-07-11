using System;
using System.Collections.Generic;

/// <summary>
/// 状态机容器，管理当前状态和状态转换。
/// 玩家和 Boss 的行为均通过此框架管理，禁止在 MonoBehaviour.Update 中写 switch-case。
/// </summary>
public class StateMachine
{
    /// <summary>
    /// 当前激活的状态
    /// </summary>
    protected State _currentState;

    /// <summary>
    /// 已注册的状态字典，以状态类型为键
    /// </summary>
    protected readonly Dictionary<Type, State> _states = new Dictionary<Type, State>();

    /// <summary>
    /// 添加一个状态到状态机。状态创建后自动初始化，绑定状态机引用。
    /// </summary>
    /// <typeparam name="T">状态类型，必须继承 State 且有无参构造函数</typeparam>
    /// <returns>创建的状态实例</returns>
    /// <exception cref="ArgumentException">当相同类型的状态已存在时抛出</exception>
    public T AddState<T>() where T : State, new()
    {
        var state = new T();
        state.Initialize(this);
        _states.Add(typeof(T), state);
        return state;
    }

    /// <summary>
    /// 转换到指定状态。转换流程：旧状态 Exit → 新状态 Enter。
    /// 首次转换时旧状态为空，不会调用 Exit。
    /// </summary>
    /// <typeparam name="T">目标状态类型</typeparam>
    /// <exception cref="KeyNotFoundException">当目标状态未注册时抛出</exception>
    public void TransitionTo<T>() where T : State
    {
        _currentState?.Exit();
        _currentState = _states[typeof(T)];
        _currentState.Enter();
    }

    /// <summary>
    /// 执行当前状态的逻辑。由外部驱动（如 MonoBehaviour.Update）每帧调用。
    /// 当前无激活状态时不执行任何操作。
    /// </summary>
    public void Update()
    {
        _currentState?.Execute();
    }

    /// <summary>
    /// 获取当前激活的状态。
    /// </summary>
    /// <returns>当前状态实例，无激活状态时返回 null</returns>
    public State GetCurrentState()
    {
        return _currentState;
    }
}
