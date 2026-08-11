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

public struct AttackCommand : ICommand{ }

public struct DeflectCommand : ICommand{ }