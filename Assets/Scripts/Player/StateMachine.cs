
using UnityEngine;

// 1. 指令接口
public interface ICommand { }
public struct IdleCommand : ICommand { }
public struct MoveCommand : ICommand
{
    public Vector2 Direction; 
    public MoveCommand(Vector2 dir) { Direction = dir; }
}
public struct JumpCommand : ICommand { }


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