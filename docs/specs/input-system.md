# 输入系统技术规格

## 设计意图

输入系统负责管理玩家的操作响应和优先级调度。在只狼式快节奏战斗中，玩家可能在极短时间内连续按下多个按键，输入系统需要精确判断哪个操作应该被执行、哪个应该被缓冲、哪个应该被丢弃，确保操作手感流畅且符合战斗逻辑。

## 核心机制

### 操作优先级

```
优先级从高到低：

  忍杀 > 弹刀/格挡 > 攻击 > 识破/闪避 > 跳跃 > 回血 > 移动

规则：
  高优先级操作可以打断低优先级操作的动画
  同优先级操作不可互相打断（需等当前动作结束）
  忍杀优先级最高 — 架势归零时贴近按左键，优先触发忍杀而不是攻击
  弹刀优先于攻击 — 同时按下左右键时，执行弹刀
```

### 输入缓冲队列

```
玩家在动画播放期间按下的操作不会丢失，而是进入缓冲队列：

  ┌──────────────────────────────────────┐
  │          InputBuffer (Queue)         │
  │  窗口：150ms                         │
  │  超出窗口的旧输入被丢弃              │
  └──────────────────────────────────────┘

流程：
  1. 玩家按下按键 → AddInput(CombatInput)
  2. 检查距上次输入是否超过 150ms
     ├─ 超过 → 清空队列，重新入队
     └─ 未超过 → 直接入队
  3. 当前动画结束（可取消点）→ GetNextInput()
  4. 按优先级取出最高优先级的输入执行
  5. 丢弃队列中剩余的输入
```

### 状态打断规则

```
打断规则矩阵：

  当前状态         可被打断的操作（高优先级）
  ──────────────────────────────────────
  攻击前摇         弹刀、闪避
  攻击判定         （不可打断）
  攻击后摇         弹刀、闪避、连招
  闪避             （不可打断）
  跳跃             （不可打断）
  喝药             任意攻击、弹刀、闪避
  受击硬直         （不可打断，锁定所有输入）
  架势崩溃         （不可打断，锁定所有输入）
  忍杀演出         （不可打断，锁定所有输入）
  识破             （不可打断）

打断实现：
  高优先级输入 → 检查 CanInterrupt(currentState, newInput)
  ├─ 允许 → 立即退出当前状态，进入新状态
  └─ 不允许 → 进入输入缓冲队列，等待可取消点
```

### 回血系统

```
按 E 键使用药葫芦：

  检查条件：
  ├─ 剩余次数 > 0？
  ├─ 当前不在硬直/被控状态？（HitState / StunState / DeathblowState）
  ├─ 当前不在其他不可打断动画中？（忍杀演出）
  │
  ├─ 全部满足 → 进入 HealingState
  │    ├─ 立即扣除 1 次使用次数
  │    ├─ 播放喝药动画（0.8 秒）
  │    ├─ 动画播完 → 回复 30% 最大生命值
  │    └─ 动画中途受击 → 打断回血
  │         ├─ 次数已扣除，不退还
  │         └─ 不回复生命值
  │
  └─ 不满足 → 不响应按键
```

### 帧冻结（HitStop）

```
打击感的核心组件，在攻击/弹刀命中的瞬间暂停游戏逻辑：

  HitStopManager 工作流程：
  1. 命中事件触发 → HitStopManager.Trigger(秒数)
  2. 下一帧 Update 开始：
     ├─ remaining > 0 → 跳过所有游戏逻辑更新
     │    ├─ 不更新物理
     │    ├─ 不更新状态机
     │    ├─ 不更新动画
     │    └─ 渲染继续（画面定格在当前帧）
     └─ remaining <= 0 → 恢复正常更新
  3. 使用 Time.unscaledDeltaTime 计时（不受 Time.timeScale 影响）

注意：
  帧冻结期间输入缓冲仍然接受输入（按键不受暂停影响）
  帧冻结结束后，缓冲队列中的输入按优先级执行
```

## 数据结构

```csharp
// 依赖：CombatEvents（事件广播）
// 依赖：PlayerStateMachine（状态切换）
// 依赖：HitStopManager（帧冻结）

/// <summary>
/// 战斗输入枚举，按优先级从高到低排列
/// </summary>
public enum CombatInput
{
    Deathblow,       // 忍杀（最高优先级）
    Deflect,         // 弹刀/格挡
    DeflectRelease,  // 松开格挡
    Attack,          // 攻击
    Dodge,           // 闪避
    Mikiri,          // 识破
    Jump,            // 跳跃
    Heal,            // 回血
    LockOn,          // 锁定切换
    Move             // 移动（最低优先级）
}

/// <summary>
/// 输入缓冲队列，管理战斗期间的输入缓存
/// </summary>
public class InputBuffer
{
    private Queue<BufferedInput> _buffer = new Queue<BufferedInput>();
    private float _bufferWindow;
}

/// <summary>
/// 缓冲输入条目，包含输入类型和时间戳
/// </summary>
[System.Serializable]
public struct BufferedInput
{
    public CombatInput input;
    public float timestamp;
}

/// <summary>
/// 回血配置 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "HealingConfig", menuName = "Combat/HealingConfig")]
public class HealingConfig : ScriptableObject
{
    [Header("药葫芦参数")]
    public int maxCharges = 10;
    public float healPercent = 0.3f;
    public float healDuration = 0.8f;
    public bool canBeInterrupted = true;
}

/// <summary>
/// 输入优先级配置 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "InputConfig", menuName = "Combat/InputConfig")]
public class InputConfig : ScriptableObject
{
    [Header("输入缓冲")]
    public float bufferWindow = 0.15f;
    
    [Header("操作优先级（从高到低）")]
    public CombatInput[] priorityOrder = {
        CombatInput.Deathblow,
        CombatInput.Deflect,
        CombatInput.DeflectRelease,
        CombatInput.Attack,
        CombatInput.Dodge,
        CombatInput.Mikiri,
        CombatInput.Jump,
        CombatInput.Heal,
        CombatInput.LockOn,
        CombatInput.Move
    };
}
```

## 接口定义

```csharp
/// <summary>
/// 输入缓冲队列，在动画播放期间缓存玩家输入
/// </summary>
public class InputBuffer
{
    /// <summary>
    /// 构造函数，设置缓冲窗口时长
    /// </summary>
    public InputBuffer(float bufferWindow);
    
    /// <summary>
    /// 添加一个输入到缓冲队列
    /// </summary>
    public void AddInput(CombatInput input);
    
    /// <summary>
    /// 获取队列中优先级最高的输入并移除它，如果队列为空返回 null
    /// </summary>
    public CombatInput? GetNextInput();
    
    /// <summary>
    /// 获取队列中优先级最高的输入但不移除（预览）
    /// </summary>
    public CombatInput? PeekNextInput();
    
    /// <summary>
    /// 清空缓冲队列
    /// </summary>
    public void Clear();
    
    /// <summary>
    /// 缓冲队列中是否有输入
    /// </summary>
    public bool HasInput { get; }
    
    /// <summary>
    /// 缓冲队列中的输入数量
    /// </summary>
    public int Count { get; }
}

/// <summary>
/// 回血系统，管理药葫芦的使用和回复逻辑
/// </summary>
public class HealingSystem
{
    public HealingSystem(HealingConfig config);
    
    /// <summary>
    /// 玩家按下回血键时调用
    /// </summary>
    public void OnHealPressed();
    
    /// <summary>
    /// 每帧调用，更新回血状态和计时
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 回血过程被打断（受击）时调用
    /// </summary>
    public void OnHealInterrupted();
    
    /// <summary>
    /// 回血动画播放完毕，触发实际回复
    /// </summary>
    public void OnHealCompleted();
    
    /// <summary>
    /// 剩余使用次数
    /// </summary>
    public int RemainingCharges { get; }
    
    /// <summary>
    /// 是否正在回血
    /// </summary>
    public bool IsHealing { get; }
    
    /// <summary>
    /// 是否可以使用药葫芦（有剩余次数且不在禁止状态）
    /// </summary>
    public bool CanHeal { get; }
    
    /// <summary>
    /// 回血次数变化事件
    /// </summary>
    public event System.Action<int> OnChargesChanged;
}

/// <summary>
/// 帧冻结管理器（全局静态）
/// </summary>
public static class HitStopManager
{
    /// <summary>
    /// 触发帧冻结，暂停游戏逻辑指定秒数
    /// </summary>
    public static void Trigger(float seconds);
    
    /// <summary>
    /// 是否处于帧冻结状态
    /// </summary>
    public static bool IsActive { get; }
    
    /// <summary>
    /// 每帧在 Update 最开始调用，处理冻结计时
    /// </summary>
    public static void Update();
    
    /// <summary>
    /// 剩余冻结时间
    /// </summary>
    public static float RemainingTime { get; }
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| bufferWindow | 150 | 毫秒 | 输入缓冲窗口，超时清空队列 |
| maxHealingCharges | 10 | 次 | 药葫芦最大使用次数 |
| healPercent | 30 | % | 每次回复最大生命值的百分比 |
| healDuration | 0.8 | 秒 | 喝药动画时长（可打断窗口） |
| hitStop_attack | 2 | 帧 | 普攻命中帧冻结（约 0.033 秒） |
| hitStop_attack3 | 3 | 帧 | 普攻第 3 段命中帧冻结（约 0.05 秒） |
| hitStop_deflect | 3 | 帧 | 弹刀命中帧冻结（约 0.05 秒） |
| hitStop_mikiri | 4 | 帧 | 识破帧冻结（约 0.067 秒） |
| hitStop_jumpStomp | 3 | 帧 | 跳跃踩头帧冻结（约 0.05 秒） |
| hitStop_deathblow | 5 | 帧 | 忍杀帧冻结（约 0.083 秒） |
| hitStop_postureBreak | 4 | 帧 | 架势崩溃帧冻结（约 0.067 秒） |

## 与其他系统的交互

### 输入

- `InputReader` → 捕获原始按键输入，转换为 `CombatInput` 枚举
- `PlayerStateMachine` → 查询当前状态，判断是否可被打断
- `PostureSystem` → 查询 Boss 架势是否归零（忍杀条件）
- `DangerSystem` → 查询当前危字类型（决定 Shift 是闪避还是识破）
- `HealingConfig` → 读取回血参数

### 输出

- `PlayerStateMachine` → 切换玩家状态（AttackState、DeflectState、DodgeState 等）
- `InputBuffer` → 在动画可取消点消费缓冲输入
- `HealingSystem` → 扣除次数、触发回复
- `CombatEvents` → 广播 `OnHealingChargeChanged`
- `HealthSystem` → 执行实际生命值回复
- `UI` → 更新药葫芦计数器显示

## 测试要点

### EditMode 单元测试

- [ ] `AddInput_WithinWindow_Buffered` — 150ms 内输入正确入队
- [ ] `AddInput_ExceedsWindow_ClearsAndEnqueues` — 超过 150ms 窗口清空后重新入队
- [ ] `GetNextInput_EmptyQueue_ReturnsNull` — 空队列返回 null
- [ ] `GetNextInput_SingleInput_ReturnsInput` — 单个输入正确出队
- [ ] `GetNextInput_MultipleInputs_ReturnsHighestPriority` — 多个输入按优先级返回最高的
- [ ] `GetNextInput_DeflectAndAttack_ReturnsDeflect` — 弹刀优先于攻击
- [ ] `GetNextInput_DeathblowAndDeflect_ReturnsDeathblow` — 忍杀优先于弹刀
- [ ] `Clear_AfterAdd_Empty` — 清空后队列为空
- [ ] `HealPressed_HasCharges_EntersHealingState` — 有次数时进入回血状态
- [ ] `HealPressed_NoCharges_NoResponse` — 无次数时不响应
- [ ] `HealPressed_InHitState_NoResponse` — 硬直中不响应
- [ ] `HealCompleted_Restores30Percent` — 回血完毕恢复 30% 生命值
- [ ] `HealInterrupted_ChargeConsumed_NoHeal` — 回血被打断，次数已扣但不回复
- [ ] `HitStop_Trigger_IsActiveTrue` — 触发后 IsActive 为 true
- [ ] `HitStop_TimeElapsed_IsActiveFalse` — 时间耗尽后 IsActive 为 false

## 验收标准

1. 玩家在动画播放期间按下的操作被缓冲，动画结束后按优先级执行
2. 缓冲窗口 150ms，超时后旧输入被丢弃
3. 忍杀优先级最高，架势归零时贴近按左键触发忍杀而非攻击
4. 弹刀输入优先于攻击输入
5. 高优先级操作可打断低优先级动画（如弹刀打断攻击后摇）
6. 药葫芦 10 次使用，每次回复 30% 生命值，0.8 秒喝药动画可被打断
7. 回血被打断后次数已扣除，不退还
8. 帧冻结在命中瞬间触发，暂停游戏逻辑但渲染继续
9. 帧冻结期间输入缓冲仍正常接受输入
