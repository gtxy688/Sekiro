using System;

using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States
{
    // 2. 状态机驱动器
    //
    // 状态机只负责生命周期顺序：退出旧状态 → 进入新状态。
    // 具体的「从哪里切到哪里」由 CharacterBody 的语义化入口或当前父状态决定。
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

        public StateMachine(
            Action<BaseState, BaseState, string> onStateChanged = null,
            Func<BaseState, string> debugPathResolver = null)
        {
            this.onStateChanged = onStateChanged;
            this.debugPathResolver = debugPathResolver;
        }

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
