
using UnityEngine;

using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States
{


    // 2. 状态机驱动器
    public class StateMachine
    {
        public BaseState CurrentState { get; private set; }

        public void ChangeState(BaseState newState)
        {
            CurrentState?.OnExit();
            CurrentState = newState;
            CurrentState?.OnEnter();
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
