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

    // Attack：按下计时；阈值前松开 = 普攻，达到 0.3s = 自动突刺
    inputActions.Player.Attack.started += _ => BeginAttackHold();
    inputActions.Player.Attack.canceled += _ => ReleaseAttack();
    inputActions.Player.Jump.started += _ => BufferCommand(new JumpCommand());
    inputActions.Player.Defend.started += _ => BufferCommand(new DeflectCommand());
    // 松手发 IdleCommand，让 DeflectState 退出（防御按住不放的语义）
    inputActions.Player.Defend.canceled += _ => BufferCommand(new IdleCommand());
    inputActions.Player.Heal.started += _ => BufferCommand(new HealCommand());
    inputActions.Player.Focus.started += _ => BufferCommand(new LockOnCommand());
}
```

### 攻击短按 / 长按

- 不需要给 InputAction 添加 Hold Interaction。
- 按下攻击时开始计时，阈值前松开才发送普通 `AttackCommand`。
- 按住达到 `CharacterConfig.AttackHoldDuration`（默认 0.3s）时，设置 `CharacterBody.ThrustAttack` 为本次主动攻击并立即发送一次 `AttackCommand`。
- 达到阈值后继续按住不会重复出招。
- `ThrustAttack` 使用独立 `AttackConfig`，Animator 状态名为 `Thrust`。
- 连招预输入继续使用 `BrainBase` 的 0.2s 单槽缓冲：窗口开启前不超过 0.2s 的攻击会在 `RecoveryWindowStart` 自动落地，过早输入会超时。

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
2. **DodgeState**：锁定中按相对 Boss 的输入切四向一次性垫步（`Dodge_Forward` / `Dodge_Back` / `Dodge_Left` / `Dodge_Right`），无输入默认前垫；未锁定仍用 `Dodge`。不要融合树。识破只认无方向垫步。
3. **相机**：`OnLockOnChanged` 驱动 M12 `CameraController` 切 VCam（FreeLook ↔ 锁定第三人称跟随），见 `06-presentation.md`。
4. **AttackState**：每招在 `AttackConfig.RotationWindowEnd` 前允许转向；锁定时持续追踪 Boss，未锁定时按移动输入方向调整。

### 锁定点 UI

- 世界空间钉在 Boss `Spine1`，Overlay 相机画在最前，不被模型挡住
- 架势崩解 → 切到 `Finsher`

## 涉及文件

- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（新 Command）
- 修改：`Assets/Scripts/Player/Brain/PlayerBrain.cs`（按键绑定）
- 新建：`Assets/Scripts/Player/Control/LockOnManager.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/MoveState.cs`（锁定面向）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`（锁定四向垫步）
- 新建：`Assets/Scripts/Mgr/GamePause.cs`
- 新建：`Assets/Scripts/Player/Input/InputRebindService.cs`
- 新建：`Assets/Scripts/UI/PauseMenuController.cs`

## 三、暂停与自定义键位

Esc / 手柄 Start 打开暂停（`PauseMenuController`）。`Time.timeScale = 0` 冻战斗；暂停时关掉 Player Map，避免摇杆抢 UI。打开菜单会选中「继续战斗」，左摇杆 / 十字键上下选，A 确认。点「设置」进入设置页：上面两条音量滑条（音乐 / 音效，0–100），下面「键位设置」。Esc / B 返回上一级：改键 → 设置 → 根页 → 继续战斗。Start 任意页直接关暂停。当前选中项用亮金底 + 深色字。拖滑条立刻改 `BGMManager` / `AudioManager` 的 Source 音量，存 `PlayerPrefs`（`audio.bgm` / `audio.sfx`）。不上 AudioMixer。音乐实际音量 = 用户音乐音量 × 淡入淡出权重。

手柄锁定只绑 `rightStickPress`（按下右摇杆），推右摇杆不再索敌。

设置页两个 Tab（`KeyboardMouse` / `Gamepad`），只改 Attack / Deflect / Dodge / Jump / Heal / LockOn。改键走 `PerformInteractiveRebinding`，同一 Scheme 撞键自动对调，override 存 `PlayerPrefs`。Move / Look 不开放。`PlayerBrain.Start` 必须 `InputRebindService.Load`，否则没开过暂停时战斗和回生提示都还是默认键。

顿帧结束时若仍在暂停，保持 `timeScale = 0`，不拨回 1。
