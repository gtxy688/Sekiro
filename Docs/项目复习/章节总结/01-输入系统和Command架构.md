## 1. 输入层的结构：BrainBase 与 PlayerBrain

### 1. 两个类的分工
输入层把玩家的操作变成角色能处理的指令，再交给 `CharacterBody.TryExecuteCommand`。它分成两个类：

- `BrainBase`：抽象基类，保存一条预输入（单槽缓冲池），每帧尝试把它交给状态机，被消费或超时后清空；
- `PlayerBrain`：继承 `BrainBase`，从 Unity Input System 读取按键和摇杆，决定每个操作变成什么指令、什么时候进入缓冲池。

指令类型都定义在 `Command.cs`，是实现 `ICommand` 接口的 `struct`：`MoveCommand`、`JumpCommand`、`AttackCommand`、`FinisherCommand`、`DeflectCommand`、`HealCommand`、`DodgeCommand`。缓冲池的字段类型是 `ICommand`，所以存入时会装箱。

输入的两条路径：

```text
Unity Input System（Player 动作表：Move / Attack / Jump / Deflect / Dodge / Heal）
  │
  ├─ 离散动作（攻击、弹反、闪避、跳跃、喝药）
  │    按键回调 → BufferCommand(cmd) → 存入缓冲池
  │    PlayerBrain.Update → base.Update() → ProcessCommandBuffer → body.TryExecuteCommand
  │
  └─ 连续移动（摇杆）
       PlayerBrain.Update 每帧读取 → body.MoveDirection
                                    → body.TryExecuteCommand(MoveCommand)   不进缓冲池
```

---

### 2. BrainBase：单槽缓冲池
```csharp
// BrainBase.cs
// 保存一条离散动作预输入，在有效期内持续尝试交给角色状态机。
[RequireComponent(typeof(CharacterBody))]
public abstract class BrainBase : MonoBehaviour
{
    protected CharacterBody body;

    // 单槽缓冲：新的动作会覆盖尚未执行的旧动作。
    private ICommand bufferedCommand;
    private float bufferTimer;

    protected virtual void Awake()
    {
        body = GetComponent<CharacterBody>();
    }

    protected virtual void Update()
    {
        ProcessCommandBuffer();
    }

    // 记录最新动作；duration 是状态机暂时拒收时允许等待的时长。
    protected void BufferCommand(ICommand cmd, float duration = 0.2f)
    {
        bufferedCommand = cmd;
        bufferTimer = duration;
    }

    // 自动回防前检查缓冲，避免覆盖玩家主动按下的防御命令。
    protected bool HasBufferedCommand<T>() where T : ICommand
    {
        return bufferedCommand is T;
    }

    // 中断操作时，只丢弃指定类型的待执行动作。
    protected void DropBufferedCommand<T>() where T : ICommand
    {
        if (bufferedCommand is not T) return;

        bufferedCommand = null;
    }

    private void ProcessCommandBuffer()
    {
        if (bufferedCommand == null) return;

        // 状态机决定当前能否接受动作；接受后立即清空缓冲。
        bool isConsumed = body.TryExecuteCommand(bufferedCommand);

        if (isConsumed)
        {
            bufferedCommand = null;
        }
        else
        {
            // 暂时拒收时继续等待，超时则丢弃本次预输入。
            bufferTimer -= Time.deltaTime;

            if (bufferTimer <= 0)
            {
                bufferedCommand = null;
            }
        }
    }
}
```

缓冲池里只有一条指令和一个倒计时：

- `BufferCommand` 写入新指令并重置倒计时（默认 0.2s），旧指令被直接覆盖；
- 每帧 `ProcessCommandBuffer` 把指令交给状态机：返回 `true` 就清空；返回 `false` 就扣减倒计时，到 0 则丢弃。

返回值的含义由状态机一侧规定（章节 3）：`true` 表示指令已被消费，`false` 表示当前状态没有接受，可以在有效期内重试。

---

### 3. PlayerBrain：绑定输入事件
`PlayerBrain` 在 `Start` 里取得 `PlayerInput` 的动作表，找到各个动作并订阅回调：

```csharp
// PlayerBrain.cs（节选）
private void Start()
{
    actions = playerInput.actions;
    // 恢复玩家保存的改键设置。
    InputRebindService.Load(actions);

    var map = actions.FindActionMap("Player");
    moveAction = map.FindAction("Move");
    attackAction = map.FindAction("Attack");
    jumpAction = map.FindAction("Jump");
    deflectAction = map.FindAction("Deflect");
    dodgeAction = map.FindAction("Dodge");
    healAction = map.FindAction("Heal");
    lockOnAction = map.FindAction("LockOn");

    // 将攻击、跳跃等离散动作接到对应回调；移动在 Update 中轮询。
    BindActions();
    callbacksBound = true; // 销毁时据此判断是否需要解绑。
    // 暂停时清除残留操作，避免恢复后沿用暂停前的输入。
    GamePause.OnChanged += HandlePauseChanged;
    // ……
}

private void BindActions()
{
    attackAction.started += OnAttackStarted;
    attackAction.canceled += OnAttackCanceled;
    jumpAction.started += OnJumpStarted;
    deflectAction.started += OnDeflectStarted;
    deflectAction.canceled += OnDeflectCanceled;
    dodgeAction.started += OnDodgeStarted;
    healAction.started += OnHealStarted;
    lockOnAction.started += OnLockOnStarted;
}
```

移动不订阅事件，而是在 `Update` 里轮询摇杆。`OnDestroy` 里会解除订阅，避免事件调用已销毁的对象。

---

## 2. 各个动作什么时候进入缓冲池

不同动作进入缓冲池的时机不一样，下表先给结论，后面逐个看代码：

| 指令 | 来源 | 进入时机 | 说明 |
| :--- | :--- | :--- | :--- |
| `AttackCommand`（轻击） | `OnAttackCanceled` | 松开攻击键 | 为了和长按突刺区分 |
| `AttackCommand`（突刺） | `ProcessHeldAttack` | 按住超过阈值的那一帧（`Update` 里） | 同时把 `body.ActiveAttack` 设为 `ThrustAttack` |
| `FinisherCommand` | `OnAttackStarted` | 按下攻击键时，且目标已有忍杀机会 | 优先于轻击和突刺 |
| `DeflectCommand` | `OnDeflectStarted` | 按下防御键 | 带 `PressId`；松键不产生指令 |
| `DodgeCommand` | `OnDodgeStarted` | 按下闪避键 | |
| `JumpCommand` | `OnJumpStarted` | 按下跳跃键 | 由地面父状态拦截（章节 3） |
| `HealCommand` | `OnHealStarted` | 按下喝药键 | 由地面父状态处理（章节 3） |
| `MoveCommand` | `PlayerBrain.Update` | 每帧 | 不进缓冲池 |

---

### 1. 攻击键：按下、按住、松开
攻击键同时承担轻击、突刺、忍杀三种功能，所以按下时先不出招，只做判断和计时：

```csharp
// PlayerBrain.cs
private void OnAttackStarted(InputAction.CallbackContext _)
{
    if (!CanAcceptPlayInput()) return;

    // 按下时决定是否忍杀，避免普通攻击在松键或缓冲期间被改成忍杀。
    if (CombatManager.Instance.HasAvailableFinisher(body))
    {
        attackPressed = false;
        holdAttackTriggered = false;
        holdThresholdChecked = true;
        BufferCommand(new FinisherCommand());
        return;
    }

    // 从按下时开始计时，后续按持续时长区分普通攻击与突刺。
    attackPressed = true;
    holdAttackTriggered = false;
    holdThresholdChecked = false;
    attackPressedTime = Time.time;
}

private void OnAttackCanceled(InputAction.CallbackContext _)
{
    // 未触发长按突刺时，松键提交普通攻击。
    if (attackPressed && !holdAttackTriggered && CanAcceptPlayInput())
    {
        body.ActiveAttack = null;
        BufferCommand(new AttackCommand());
    }
    attackPressed = false;
}

// 按住达到配置时长后请求突刺，每次按下只请求一次。
private void ProcessHeldAttack()
{
    // 没有按住攻击键、已经触发过长按，或判定已经处理过，就直接返回。
    if (!attackPressed || holdAttackTriggered || holdThresholdChecked) return;

    float threshold = body.Config.AttackHoldDuration;
    if (Time.time - attackPressedTime < threshold) return;

    holdThresholdChecked = true;
    holdAttackTriggered = true;
    body.ActiveAttack = body.ThrustAttack;
    BufferCommand(new AttackCommand());
}
```

三个方法配合起来的效果：

- **按下时**：如果目标当前有忍杀机会，直接写入 `FinisherCommand`，并把 `holdThresholdChecked` 置为 `true`，这次按键不再进入长按判断；否则记录按下状态和时间；
- **按住时**：`Update` 里的 `ProcessHeldAttack` 每帧检查，按住时间达到 `Config.AttackHoldDuration` 的那一帧，把 `body.ActiveAttack` 设为 `ThrustAttack` 并写入 `AttackCommand`；
- **松开时**：只有这次按键没有触发突刺（`holdAttackTriggered` 为 `false`），才写入轻击的 `AttackCommand`。已经触发过突刺的，松开时不会再多出一刀。

`AttackCommand` 本身不带招式信息。具体是哪一招，由状态进入时通过 `body.GetAttackConfig()` 决定：有 `ActiveAttack` 就用它，否则用默认的 `LightAttack`。

---

### 2. 防御键：PressId 与松键
```csharp
// PlayerBrain.cs
private void OnDeflectStarted(InputAction.CallbackContext _)
{
    if (!CanAcceptPlayInput()) return;

    // 同一次按下共用 PressId，避免缓冲重试或状态重入重复开启弹反窗口。
    int pressId = body.BeginDeflectPress();
    BufferCommand(DeflectCommand.Physical(pressId));
}

private void OnDeflectCanceled(InputAction.CallbackContext _)
{
    // 只记录松键，收刀由防御状态决定，避免短按提前关闭弹反窗口。
    body.NotifyDeflectReleased();
}
```

- 每次按下都会得到一个新的 `PressId`，放进 `DeflectCommand`。这条指令在缓冲池里可能被重试多次，状态也可能被重复进入，但它们带的是同一个 `PressId`，所以角色一侧能认出这是同一次按下，同一次按下只开一次弹反窗口；
- 松开防御键不生成指令，只调用 `body.NotifyDeflectReleased()` 记录松键。什么时候收刀由防御状态自己决定；
- 还有一种 `DeflectCommand.System()`（`PressId` 为 0，来源是系统请求），用于崩解恢复后自动回防，只恢复普通格挡，不开弹反窗口（见下一节 `Update` 里的步骤 4）。

---

### 3. 闪避、跳跃、喝药
这三个动作按下就直接写入缓冲池：

```csharp
// PlayerBrain.cs
private void OnDodgeStarted(InputAction.CallbackContext ctx)
{
    // 只接受按钮处于按下状态的回调。
    if (!ctx.ReadValueAsButton()) return;
    if (!CanAcceptPlayInput()) return;
    BufferCommand(new DodgeCommand());
}

private void OnJumpStarted(InputAction.CallbackContext _)
{
    if (CanAcceptPlayInput()) BufferCommand(new JumpCommand());
}

private void OnHealStarted(InputAction.CallbackContext _)
{
    if (CanAcceptPlayInput()) BufferCommand(new HealCommand());
}
```

---

## 3. PlayerBrain.Update：每帧的执行顺序

```csharp
// PlayerBrain.cs
protected override void Update()
{
    if (GamePause.IsPaused) return;

    // 结算期间清除操作并发送零移动，让角色停止移动。
    if (CombatInputGate.Blocked)
    {
        ClearActionInput();
        body.TryExecuteCommand(new MoveCommand(Vector2.zero));
        return;
    }

    ProcessHeldAttack();

    Vector2 rawInput = moveAction.ReadValue<Vector2>();
    // 忽略微小输入，避免摇杆漂移造成角色移动。
    Vector2 currentMoveInput = rawInput.sqrMagnitude < 0.01f ? Vector2.zero : rawInput;
    // 先更新方向，再消费缓冲，让垫步进入状态时能读取本帧方向。
    body.MoveDirection = currentMoveInput;

    // 由 BrainBase 尝试执行缓冲中的攻击、防御、跳跃等离散动作。
    base.Update();

    // 破防后按住防御可请求恢复普通格挡，不开启完美弹反窗口。
    // 玩家按下的防御命令优先，系统请求不能覆盖它。
    if (body.IsPostureBroken && deflectAction.IsPressed() && !HasBufferedCommand<DeflectCommand>())
        BufferCommand(DeflectCommand.System(), 0.15f);

    // 移动是连续输入，每帧直接提交，不占用离散动作缓冲。
    body.TryExecuteCommand(new MoveCommand(currentMoveInput));
}
```

按顺序是：

1. 游戏暂停时整个 `Update` 不执行；
2. 战斗输入被阻断（`CombatInputGate.Blocked`，比如结算期间）时，清除攻击和防御的按键状态，发一次零移动，然后返回；
3. `ProcessHeldAttack()`：检查长按是否到了突刺阈值；
4. 读取摇杆，过滤掉很小的输入，写入 `body.MoveDirection`；
5. `base.Update()`：调用 `BrainBase.Update`，也就是 `ProcessCommandBuffer`，尝试把缓冲池里的指令交给状态机；
6. 角色处于崩解状态且玩家按住防御、缓冲里又没有玩家自己的防御指令时，写入一条系统防御请求（有效期 0.15s）；
7. 把当前摇杆方向作为 `MoveCommand` 直接提交。

**为什么 `MoveDirection` 要在 `base.Update()` 之前写入？** `DodgeState` 进入时要读取 `body.MoveDirection` 决定往哪个方向垫步。如果先消费缓冲再更新方向，玩家推着摇杆按下闪避的那一帧，垫步读到的是上一帧的方向。先写方向再消费缓冲，进入状态时读到的就是本帧的输入。

---

## 4. 缓冲池怎样被消费

### 1. 一条指令的生命周期
以 `AttackCommand` 为例：

1. 松开攻击键，`BufferCommand` 写入指令，倒计时设为 0.2s；
2. 在 `Update` 里 `ProcessCommandBuffer` 把它交给 `body.TryExecuteCommand`：
   - 角色在待机，状态机消费指令并切到 `AttackState`，返回 `true`，缓冲清空；
   - 角色正处于不能接受的状态（例如上一刀的命中段），返回 `false`，缓冲保留，倒计时扣减；
3. 之后每一帧重复第 2 步：
   - 在倒计时结束前状态机放行（例如上一刀进入后摇），指令被消费，缓冲清空；
   - 倒计时到 0 仍没被接受，指令被丢弃，避免过早按下的操作很久之后才突然执行。

---

### 2. 离散动作与连续移动的对比

| | 离散动作（攻击、弹反、闪避、跳跃、喝药） | 连续移动（`MoveCommand`） |
| :--- | :--- | :--- |
| 存储 | 单槽缓冲池，新指令覆盖旧指令 | 不缓冲，每帧直接提交 |
| 有效期 | 默认 0.2s，被拒收时每帧重试 | 只对当前帧有效，下一帧由新的摇杆读数取代 |
| 消费 | 返回 `true` 清空，返回 `false` 保留重试 | 每帧都通过 `TryExecuteCommand` 提交，同时 `body.MoveDirection` 每帧被更新 |

两种设计带来的效果：

- **单槽覆盖**：玩家在硬直里先后按了轻击和闪避，缓冲池里只剩最后按下的闪避，硬直结束后执行的就是最近一次的操作；如果用队列，两个动作会先后都被执行；
- **移动不缓冲**：摇杆每帧都有新值，每帧直接提交最新读数即可，缓冲旧的方向没有意义（这是对设计原因的推测，源码没有说明）。

---

## 5. 暂停、禁用与输入阻断时的清理

### 1. 统一的输入门限
所有按键回调开头都先调用 `CanAcceptPlayInput`：

```csharp
// PlayerBrain.cs
// 统一限制动作回调：组件需启用，且不在暂停或结算期间。
private bool CanAcceptPlayInput()
{
    // Unity 对象销毁后，不能再读取 isActiveAndEnabled。
    if (this == null) return false;

    return isActiveAndEnabled && !GamePause.IsPaused && !CombatInputGate.Blocked;
}
```

### 2. 清除按键状态和防御缓冲
```csharp
// PlayerBrain.cs
private void HandlePauseChanged(bool paused)
{
    if (!paused) return;
    ClearActionInput();
}

// 取消尚未结束的攻击输入与防御输入，其它已缓冲动作仍按原策略保留。
private void ClearActionInput()
{
    attackPressed = false;
    holdAttackTriggered = false;
    holdThresholdChecked = false;
    ClearDeflectInput();
}

// 中断操作时清除防御状态与缓冲命令，恢复后需重新按下防御。
private void ClearDeflectInput()
{
    if (body == null) return;
    body.ClearDeflectInput();
    DropBufferedCommand<DeflectCommand>();
}

private void OnDisable()
{
    ClearActionInput();
}
```

`ClearActionInput` 在三种情况下被调用：游戏暂停、`CombatInputGate.Blocked` 的每一帧、组件被禁用（比如换场景、销毁）。它会重置攻击键的按下和长按标记，并清除防御：既重置 `body` 里的防御输入状态，也丢掉缓冲池里的 `DeflectCommand`。缓冲池里其他类型的指令（比如已经按下的闪避）不受影响。

### 3. 受击时：不清缓冲池，由状态消费指令
受击时 `PlayerBrain` 不会收到通知，也不会清缓冲池。处理分两处，都在角色一侧。

**第一处：`ReceiveHit` 结束当前弹反尝试。** 命中没有被防御状态挡住、真的结算了伤害之后：

```csharp
// CharacterBody.cs ReceiveHit（节选）
TakeDamage(healthDmg, postureDmg, attacker);

// 实际受伤后结束未成功的防御尝试；致死已按生命周期取消，避免重复记空按。
if (CurrentHP > 0)
    NotifyDeflectHitFailed();
```

`NotifyDeflectHitFailed` 以 `HitFailed` 为原因结束当前弹反尝试，记一次空按（用于缩小下次弹反窗口），之后这次尝试就不能再弹反。

**第二处：受击状态把指令消费掉。** `ReceiveHit` 之后角色进入 `StunnedState`，此后 `PlayerBrain` 缓冲池里的指令会被交给它处理：

```csharp
// StunnedState.cs OnParentHandleCommand（节选）
// 父层先接收输入命令：通常消费掉，只有需要让输入缓冲稍后重试的垫步返回 false。
if (useHitGrade && cmd is DodgeCommand)
{
    if (ground != null && ground.CanDodgeCancel)
    {
        ...
        return true;
    }
    // 受击计时未到开放点：返回 false，输入缓冲仍可在有效期内再次尝试。
    return false;
}
if (useHitGrade && cmd is DeflectCommand deflectCmd)
{
    if (ground != null && ground.CanMidToGuard) { ...进入 MidToGuardState...; return true; }
    // 轻受击且已落地时可直接抬刀，不需要经过 Mid 的起身进防动画。
    if (ground != null && ground.CanLightGuardCancel) { ...进入 DeflectState...; return true; }
}
return true;
```

受击期间，大部分指令（包括没到取消点的 `DeflectCommand`）走到最后的 `return true`，被当作已消费，`PlayerBrain` 随即清空缓冲池，不会等硬直结束后再执行；只有垫步在取消点还没开放时返回 `false`，继续留在缓冲池里重试，倒计时（0.2s）到了仍未接受才丢弃。弹反在达到取消条件时才会真正进入防御。

这条路径的效果是：受击硬直期间按下的防御，不会在硬直结束后突然出现一次格挡。
