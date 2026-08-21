# 攻击后摇、转向与长按突刺实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 用现有 0.2 秒输入缓冲实现连招预输入，开放后摇取消，攻击期间正确追踪方向，并让长按攻击自动触发 `Thrust`。

**架构：** `AttackConfig` 保存每招的后摇和转向参数；`AttackState` 是窗口门控唯一权威；`BrainBase` 继续提供滚动缓冲；`PlayerBrain` 只负责将短按/长按翻译为同一个 `AttackCommand` 和一次性攻击配置。

**技术栈：** Unity Input System、C# HFSM、ScriptableObject、Animator Root Motion。

**提交约束：** 用户未要求 Git commit，执行期间不得创建 commit。

---

## 文件职责

- 修改 `Assets/Scripts/SO/AttackConfig.cs`：后摇与转向参数。
- 修改 `Assets/Scripts/SO/CharacterConfig.cs`：长按阈值。
- 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs`：突刺配置与攻击窗口运行时标志。
- 修改 `Assets/Scripts/FrameWork/States/Ground/AttackState.cs`：缓冲落地、取消和转向。
- 修改 `Assets/Scripts/FrameWork/States/Ground/GroundedState.cs`：跳跃/喝药服从攻击窗口。
- 修改 `Assets/Scripts/Player/Brain/PlayerBrain.cs`：短按/长按输入。
- 修改玩家攻击 `AttackConfig` 资源：配置后摇和转向数值。
- 修改 `Docs/architecture/01-states.md`、`Docs/architecture/05-input-lockon.md`。

### 任务 1：重定义 AttackConfig 时间窗口

- [ ] **步骤 1：修改 `AttackConfig.cs`**

```csharp
using UnityEngine.Serialization;

[FormerlySerializedAs("ComboWindowStart")]
public float RecoveryWindowStart = 0f;

public float ComboWindowEnd = 0.33f;

[Header("攻击转向")]
public bool AllowRotation = true;
public float RotationSpeed = 720f;
public float RotationWindowEnd = 0.3f;
```

删除旧 `ComboWindowStart` 字段。`FormerlySerializedAs` 必须保留已有 SO 的数值。

- [ ] **步骤 2：刷新 Unity 并抽查 `atk1.asset`**

预期：原 `ComboWindowStart: 0.6` 被迁移为 `RecoveryWindowStart: 0.6`，`NextCombo` 引用不丢失。

- [ ] **步骤 3：为每段玩家攻击设置顺序约束**

每个 SO 必须满足：

```text
0 <= HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration
0 <= RotationWindowEnd <= StateDuration
```

违反时在 `AttackState.OnEnter` 输出包含 SO 名称的 Error，并立刻回待机，禁止带错误时间窗继续运行。

### 任务 2：重写 AttackState 窗口门控

- [ ] **步骤 1：删除 `hasBufferedNextHit`**

预输入只依赖 `BrainBase` 的 0.2 秒滚动缓冲。`AttackState` 不保存额外攻击队列。

- [ ] **步骤 2：增加运行时窗口属性**

`CharacterBody`：

```csharp
public bool IsAttackRecoveryOpen { get; set; }
```

`AttackState.OnEnter` 设为 false，`OnExit` 也清为 false。`OnUpdate`：

```csharp
body.IsAttackRecoveryOpen =
    stateTimer >= config.RecoveryWindowStart;
```

- [ ] **步骤 3：实现攻击命令门控**

```csharp
if (cmd is AttackCommand)
{
    if (config.NextCombo != null &&
        stateTimer >= config.RecoveryWindowStart &&
        stateTimer <= config.ComboWindowEnd)
    {
        parent.SubStateMachine.ChangeState(
            new AttackState(body, parent, config.NextCombo));
        return true;
    }
    return false;
}
```

窗口前返回 false，使命令留在通用缓冲中；过早输入会在 0.2 秒后自然过期。

- [ ] **步骤 4：实现格挡和闪避取消**

允许条件：

```csharp
bool canEarlyCancel = stateTimer < config.HitStartTime;
bool canRecoveryCancel = stateTimer >= config.RecoveryWindowStart;
bool canCancel = canEarlyCancel || canRecoveryCancel;
```

`DeflectCommand` / `DodgeCommand` 在 `canCancel` 时立即切状态，否则返回 false。

- [ ] **步骤 5：实现移动取消**

`MoveCommand` 始终更新 `body.MoveDirection`。只有同时满足：

```csharp
stateTimer >= config.RecoveryWindowStart &&
moveCmd.Direction.sqrMagnitude >= 0.01f
```

才切 `MoveState`。零输入不会取消攻击。

- [ ] **步骤 6：让父层跳跃和喝药服从后摇**

在 `GroundedState.OnParentHandleCommand` 中，处理 `JumpCommand` / `HealCommand` 前增加：

```csharp
if (body.IsAttacking && !body.IsAttackRecoveryOpen)
    return false;
```

返回 false 后，`AttackState` 也返回 false，离散命令继续留在 0.2 秒缓冲；进入后摇后父层会在下一次重试时执行。

- [ ] **步骤 7：刷新 Unity 并检查 Console**

预期：编译通过；攻击窗口非法的 SO 会输出明确 Error。

### 任务 3：实现攻击转向

- [ ] **步骤 1：在 `AttackState.OnUpdate` 增加转向**

```csharp
if (config.AllowRotation &&
    stateTimer <= config.RotationWindowEnd)
{
    RotateDuringAttack();
}
```

`RotateDuringAttack()`：

```csharp
private void RotateDuringAttack()
{
    Vector3 direction;
    if (LockOnManager.Instance != null &&
        LockOnManager.Instance.IsLockedOn &&
        LockOnManager.Instance.Target != null)
    {
        direction = LockOnManager.Instance.Target.position -
                    body.transform.position;
    }
    else
    {
        direction = body.InputToWorldDir(body.MoveDirection);
    }

    direction.y = 0f;
    body.RotateYaw(direction, config.RotationSpeed);
}
```

- [ ] **步骤 2：检查玩家五段攻击和 `Thrust` 的导入设置**

每个攻击 Clip：

- Root Transform Rotation：Bake Into Pose。
- Root Transform Position (XZ)：保留动画根位移，不勾 Bake Into Pose。
- Root Transform Position (Y)：按现有接地表现保持一致。

预期：代码转向不会被动画根旋转拉回，攻击前移仍由 Root Motion 驱动。

- [ ] **步骤 3：Play Mode 手动验证**

1. 未锁定时按左右方向出刀，角色在转向窗口内改变攻击方向。
2. 锁定后无论 WASD 方向如何，攻击始终追踪 Boss。
3. 超过 `RotationWindowEnd` 后不再突然转身。

### 任务 4：实现短按普通攻击与长按突刺

- [ ] **步骤 1：增加配置**

`CharacterConfig.cs`：

```csharp
[Header("攻击输入")]
public float AttackHoldDuration = 0.3f;
```

`CharacterBody.cs`：

```csharp
public AttackConfig ThrustAttack;
```

- [ ] **步骤 2：修改 `PlayerBrain` 字段**

```csharp
private InputAction attackAction;
private bool attackPressed;
private bool holdAttackTriggered;
private bool holdThresholdChecked;
private float attackPressedTime;
```

- [ ] **步骤 3：替换 Attack 的 started-only 监听**

`Start()`：

```csharp
attackAction = map.FindAction("Attack");
attackAction.started += _ =>
{
    attackPressed = true;
    holdAttackTriggered = false;
    holdThresholdChecked = false;
    attackPressedTime = Time.time;
};
attackAction.canceled += _ =>
{
    if (attackPressed && !holdAttackTriggered)
    {
        body.ActiveAttack = null;
        BufferCommand(new AttackCommand());
    }
    attackPressed = false;
};
```

`Update()` 在 `base.Update()` 前检查长按，保证新命令当帧可进入缓冲：

```csharp
if (attackPressed && !holdAttackTriggered && !holdThresholdChecked)
{
    float threshold = body.Config != null
        ? body.Config.AttackHoldDuration
        : 0.3f;
    if (Time.time - attackPressedTime >= threshold)
    {
        holdThresholdChecked = true;
        if (body.ThrustAttack == null)
        {
            Debug.LogError($"{body.name} 未配置 ThrustAttack，松开后回退普通攻击。");
        }
        else
        {
            holdAttackTriggered = true;
            body.ActiveAttack = body.ThrustAttack;
            BufferCommand(new AttackCommand());
        }
    }
}
```

`holdThresholdChecked` 保证缺配置时只输出一次 Error；因为 `holdAttackTriggered` 仍为 false，松开时会发送普通攻击。

- [ ] **步骤 4：让一次性攻击选择只消费一次**

`AttackState.OnEnter` 在保存当前 `config` 后：

```csharp
if (body.ActiveAttack == config)
    body.ActiveAttack = null;
```

Boss BT 使用同一机制，仍能在命令落地时消费配置；不得在命令尚处于缓冲时提前清空。

- [ ] **步骤 5：创建并绑定玩家突刺 AttackConfig**

创建 `ThrustAttack.asset`：

- `AnimName = "Thrust"`
- `NextCombo = null`
- `Perilous = None`，除非后续明确要求 Boss 可识破玩家突刺
- 伤害、架势、时间窗与转向参数由用户在 Inspector 按动画实际时长设置

将资源拖到玩家 `CharacterBody.ThrustAttack`。

- [ ] **步骤 6：刷新 Unity 并检查 Console**

预期：无需修改 `.inputactions`；短按松开触发普通攻击，按住 0.3 秒自动触发一次 `Thrust`。

### 任务 5：同步文档与手动验收

- [ ] **步骤 1：更新 `01-states.md`**

替换旧“过前摇整刀锁死”规则，写入前摇取消、锁定段、后摇取消和 0.2 秒预输入。

- [ ] **步骤 2：更新 `05-input-lockon.md`**

写入短按/长按 Attack 事件语义，以及锁定攻击持续朝 Boss 转向。

- [ ] **步骤 3：执行手动验收**

1. 在 `RecoveryWindowStart` 前 0.2 秒内按攻击，当前动画不立即中断，到后摇起点衔接下一段。
2. 更早按攻击不会衔接。
3. 后摇开始后移动、格挡、闪避、跳跃、喝药立即响应。
4. `ComboWindowEnd` 后不再接 `NextCombo`。
5. 攻击前摇格挡取消后，松手正常播放 `Deflect_Cancel`。
6. 锁定时攻击追踪 Boss；未锁定时按输入转向。
7. 短按普通攻击，长按自动突刺，长按不重复出招。

预期：Console 无 Error；所有玩家 AttackConfig 时间顺序合法。
