using System;

using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States
{
    // 普通 C# 状态机：管理状态的进入、退出、更新和命令转发。
    // 本类不判断切换条件，调用方决定何时 ChangeState；顶层与子状态机共用这套机制。
    public class StateMachine
    {
        private readonly Action<BaseState, BaseState, string> onStateChanged;
        private readonly Func<BaseState, string> debugPathResolver;

        public BaseState CurrentState { get; private set; }
        public BaseState PreviousState { get; private set; }
        public string PreviousStatePath { get; private set; } = "<none>";
        public string CurrentStatePath { get; private set; } = "<none>";
        public string LastChangeReason { get; private set; }
        public int TransitionCount { get; private set; }

        // 构造只保存调试回调，不进入状态；首次 ChangeState 后才有 CurrentState。
        public StateMachine(
            Action<BaseState, BaseState, string> onStateChanged = null,
            Func<BaseState, string> debugPathResolver = null)
        {
            this.onStateChanged = onStateChanged;
            this.debugPathResolver = debugPathResolver;
        }

        // null 表示清空当前状态；切换始终先退出旧状态，再进入新状态。
        public void ChangeState(BaseState newState, string reason = null)
        {
            BaseState previous = CurrentState;
            // 先保存旧路径，再执行 OnExit。父状态的 OnExit 会清空子状态机，
            // 不能等退出后再通过对象反推完整路径。
            PreviousStatePath = ResolveDebugPath(previous);
            previous?.OnExit();
            PreviousState = previous;
            CurrentState = newState;
            LastChangeReason = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
            // 父状态会在 OnEnter 中建立初始子状态，之后再记录完整调试路径。
            CurrentState?.OnEnter();
            CurrentStatePath = ResolveDebugPath(CurrentState);
            TransitionCount++;
            onStateChanged?.Invoke(previous, CurrentState, LastChangeReason);
        }

        private string ResolveDebugPath(BaseState state)
        {
            if (state == null) 
                return "<none>";
            return debugPathResolver != null ? debugPathResolver(state) : state.GetType().Name;
        }

        public void Update()
        {
            CurrentState?.OnUpdate();
        }

        public bool HandleCommand(ICommand command)
        {
            return CurrentState != null && CurrentState.HandleCommand(command);
        }
    }
}
