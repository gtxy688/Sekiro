using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerBrain : BrainBase
{
    private PlayerInputActions inputActions;
    
    // 专门用来暂存摇杆当前的输入值
    private Vector2 currentMoveInput;

    protected override void Awake()
    {
        base.Awake();
        inputActions = new PlayerInputActions();

        // A. 绑定连续输入 (摇杆移动)
        // performed: 摇杆被推时持续触发
        inputActions.Player.Move.performed += ctx => currentMoveInput = ctx.ReadValue<Vector2>();
        // canceled: 摇杆松开时触发归零
        inputActions.Player.Move.canceled += ctx => currentMoveInput = Vector2.zero;

        // B. 绑定离散输入 (动作按键，带有输入缓冲)
        // started: 按下按键的第一帧。我们生成对应的 Command 并丢进基类的缓冲池
        inputActions.Player.Attack.started += _ => BufferCommand(new AttackCommand());
        inputActions.Player.Jump.started += _ => BufferCommand(new JumpCommand());
        inputActions.Player.Defend.started += _ => BufferCommand(new DeflectCommand());
        // 松手时发 IdleCommand，让 DeflectState 退出回待机（防御按住不放的语义）
        inputActions.Player.Defend.canceled += _ => BufferCommand(new IdleCommand());
        inputActions.Player.Dodge.started += _ => BufferCommand(new DodgeCommand());
        inputActions.Player.Heal.started += _ => BufferCommand(new HealCommand());
        // M11 LockOnManager 接入前，Focus 命令暂时无人消费（缓冲池超时会自动丢弃，无害）
        inputActions.Player.Focus.started += _ => BufferCommand(new LockOnCommand());
    }

    protected override void Update()
    {
        // 1. 调用基类的 Update，让它去处理缓冲池里的 攻击、弹反、跳跃 指令
        base.Update();

        // 2. 独立处理移动指令 (连绵不断的意图)
        // 直接生成移动指令并尝试执行，不经过缓冲池。
        // 因为摇杆是连续的，当前是什么方向就发什么方向，不需要“预输入”未来的摇杆方向。
        body.TryExecuteCommand(new MoveCommand(currentMoveInput));
    }

    // 启用与禁用 InputSystem
    private void OnEnable()
    {
        inputActions.Enable();
    }

    private void OnDisable()
    {
        inputActions.Disable();
    }
}