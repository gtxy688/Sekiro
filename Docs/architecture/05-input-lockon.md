# 05 玩家控制（M6 输入 + M11 锁定）

> 模块：M6, M11
> 前置：M1
> 验收：`05-input-lockon-test.md`

## 一、玩家输入（M6）

InputAction 已定义 8 个动作（`Assets/Scripts/Player/Input/`）。当前 PlayerBrain 只绑了 Move，其他待实现。

### 按键 → Command 映射

| 动作 | 按键 | Command | 状态机处理 |
|------|------|---------|-----------|
| Move | WASD/摇杆 | MoveCommand（连续，不走缓冲） | MoveState |
| Attack | 鼠标左键/J | AttackCommand | AttackState |
| Jump | 空格 | JumpCommand | GroundedState 拦截 |
| Defend | 鼠标右键/K | DeflectCommand | DeflectState |
| Dodge | 左 Shift | DodgeCommand | DodgeState |
| Heal | E | HealCommand（新增） | GroundedState 拦截 → 葫芦 |
| Focus | 中键 | LockOnCommand（新增） | LockOnManager |
| Crouch | Ctrl | （暂不实现） | - |

### Command.cs 新增

```csharp
public struct HealCommand : ICommand { }
public struct LockOnCommand : ICommand { }
```

> 保留：`DodgeCommand`（垫步，左 Shift）。

### PlayerBrain 实现

```csharp
protected override void Awake()
{
    base.Awake();
    inputActions = new PlayerInputActions();
    inputActions.Player.Move.performed += ctx => currentMoveInput = ctx.ReadValue<Vector2>();
    inputActions.Player.Move.canceled += ctx => currentMoveInput = Vector2.zero;

    // 离散按键 → 缓冲池（基类 BufferCommand）
    inputActions.Player.Attack.started += _ => BufferCommand(new AttackCommand());
    inputActions.Player.Jump.started += _ => BufferCommand(new JumpCommand());
    inputActions.Player.Defend.started += _ => BufferCommand(new DeflectCommand());
    // 松手发 IdleCommand，让 DeflectState 退出（防御按住不放的语义）
    inputActions.Player.Defend.canceled += _ => BufferCommand(new IdleCommand());
    inputActions.Player.Heal.started += _ => BufferCommand(new HealCommand());
    inputActions.Player.Focus.started += _ => BufferCommand(new LockOnCommand());
}
```

### 缓冲池规则（已有）

- 离散按键 → BufferCommand（0.2s 预输入窗口）
- Move 连续 → 直接 TryExecuteCommand，不走缓冲

## 二、锁定系统（M11）

### LockOnManager

```csharp
public class LockOnManager : MonoBehaviour
{
    public Transform Target { get; private set; }
    public bool IsLockedOn => Target != null;

    public void ToggleLockOn() { ... }     // 无目标→锁定，有目标→解锁
    public void UpdateTarget() { ... }     // 每帧检查目标是否存活/超范围
}
```

- 按下 Focus（LockOnCommand）→ 锁定视野内最近的敌人
- 再按 → 解锁
- 目标死亡 → 自动解锁

### 锁定对状态的影响

1. **MoveState 面向**：锁定中，面向目标（覆盖摇杆转身）：
   ```csharp
   // MoveState.OnUpdate
   if (lockOnManager.IsLockedOn)
   {
       Vector3 dir = (lockOnManager.Target.position - body.transform.position).normalized;
       // 面向 dir，位移仍按摇杆输入
   }
   ```
2. **相机**：锁定模式由 M12（Cinemachine）处理。

### 锁定点 UI

- 世界空间白点挂在 Target 身上（M13）
- 架势崩解 → 红点高亮

## 涉及文件

- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（新 Command）
- 修改：`Assets/Scripts/Player/Brain/PlayerBrain.cs`（按键绑定）
- 新建：`Assets/Scripts/Player/Control/LockOnManager.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/MoveState.cs`（锁定面向）
