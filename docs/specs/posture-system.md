# 架势条系统技术规格

## 设计意图

架势条是只狼的双轨博弈核心。玩家和 Boss 各有一条独立的架势条，攻击和弹刀互相影响双方架势。架势归零 → 崩溃 → 可忍杀（Boss）或长时间硬直（玩家）。

## 核心机制

### 架势变化规则

| 行为 | 对敌方架势 | 对己方架势 |
|------|-----------|-----------|
| 普攻命中 | +基础削韧值 | 无 |
| 重击命中 | +大量削韧值 | 无 |
| 完美弹刀 | +大幅削韧（受加成链影响） | -恢复5-10% |
| 普通格挡 | 无 | +小幅上升 |
| 被攻击未弹刀 | 无 | +大幅上升 |
| 抖刀惩罚后弹刀 | +递减（最低20%） | -恢复量也递减 |
| 识破成功 | +大量削韧 | 无 |
| 跳跃踩头 | +中量削韧 | 无 |
| 停止交战2秒后 | 缓慢恢复 | 缓慢恢复 |

### 架势恢复

```
恢复逻辑：
  脱战2秒后 → 架势以每秒15%的速度恢复
  主动格挡状态 → 恢复速度减半（按住右键格挡时恢复更慢）
  受击后 → 恢复计时器重置
```

### 崩溃判定

```
当 currentPosture >= maxPosture：

  对玩家：
    → 强制进入 StunState
    → 播放大硬直动画（约1.5秒）
    → 期间无法操作
    → 弦一郎获得自由攻击窗口

  对弦一郎：
    → 播放崩溃动画（约2秒）
    → 头顶出现红色忍杀提示
    → 玩家贴近按左键 → 触发忍杀
```

## 数据结构

```csharp
// 依赖：PlayerStats / BossStats（见 data-layer.md）
// 依赖：CombatEvents（事件广播）

public class PostureSystem
{
    private float _maxPosture;
    private float _currentPosture;
    private float _recoveryRate;          // 每秒恢复百分比
    private float _recoveryDelay;         // 脱战后多久开始恢复
    private float _lastHitTime;           // 上次受击时间
    private bool _isBlocking;             // 是否处于格挡状态
}
```

## 接口定义

```csharp
/// <summary>
/// 架势条系统，管理架势值的增加、恢复和崩溃判定
/// </summary>
public class PostureSystem
{
    public PostureSystem(float maxPosture, float recoveryRate, float recoveryDelay);
    
    /// <summary>
    /// 增加架势值，可能触发崩溃
    /// </summary>
    public void AddPosture(float amount);
    
    /// <summary>
    /// 减少架势值（弹刀恢复）
    /// </summary>
    public void ReducePosture(float amount);
    
    /// <summary>
    /// 每帧调用，处理架势恢复逻辑
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 受击时调用，重置恢复计时器
    /// </summary>
    public void OnHit();
    
    /// <summary>
    /// 设置是否处于格挡状态（影响恢复速度）
    /// </summary>
    public void SetBlocking(bool isBlocking);
    
    /// <summary>
    /// 当前架势值
    /// </summary>
    public float CurrentPosture { get; }
    
    /// <summary>
    /// 最大架势值
    /// </summary>
    public float MaxPosture { get; }
    
    /// <summary>
    /// 架势百分比（0-1）
    /// </summary>
    public float PosturePercent { get; }
    
    /// <summary>
    /// 是否处于崩溃状态
    /// </summary>
    public bool IsBroken { get; }
    
    /// <summary>
    /// 架势崩溃事件
    /// </summary>
    public event System.Action OnPostureBroken;
    
    /// <summary>
    /// 架势值变化事件（当前值，最大值）
    /// </summary>
    public event System.Action<float, float> OnPostureChanged;
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| playerMaxPosture | 300 | 点 | 玩家最大架势 |
| bossPhase1MaxPosture | 300 | 点 | 弦一郎一阶段最大架势 |
| bossPhase2MaxPosture | 400 | 点 | 弦一郎二阶段最大架势 |
| postureRecoveryRate | 15 | %/秒 | 脱战后每秒恢复15% |
| postureRecoveryDelay | 2 | 秒 | 脱战2秒后开始恢复 |
| blockRecoveryMultiplier | 0.5 | 倍 | 格挡时恢复速度减半 |
| playerStunDuration | 1.5 | 秒 | 玩家崩溃硬直时长 |
| bossCollapseDuration | 2.0 | 秒 | Boss崩溃等待忍杀时长 |

## 与其他系统的交互

### 输入

- `DeflectSystem` → 弹刀恢复架势
- `DamageCalculator` → 攻击增加架势
- `PlayerCombatController` → 格挡状态
- `Time` → 脱战计时

### 输出

- `PlayerStateMachine` → 崩溃时进入 StunState
- `BossStateMachine` → 崩溃时进入 CollapseState
- `CombatEvents` → 广播 `OnPlayerPostureBreak` / `OnBossPostureBreak`
- `UI` → 更新架势条显示

## 测试要点

### EditMode 单元测试

- [ ] `AddPosture_BelowMax_DoesNotTriggerBreak` — 架势未满不崩溃
- [ ] `AddPosture_ExceedsMax_TriggersBreak` — 架势超过最大值触发崩溃
- [ ] `AddPosture_AtMax_TriggersBreak` — 架势刚好满触发崩溃
- [ ] `ReducePosture_AfterHit_DecreasesPosture` — 弹刀恢复架势
- [ ] `ReducePosture_BelowZero_ClampsToZero` — 架势不低于0
- [ ] `Recovery_AfterDelay_RecoverRate15Percent` — 脱战2秒后每秒恢复15%
- [ ] `Recovery_WhileBlocking_HalfRate` — 格挡时恢复速度减半
- [ ] `OnHit_ResetsRecoveryTimer` — 受击重置恢复计时器
- [ ] `PosturePercent_AtHalf_Returns05` — 架势百分比计算正确
- [ ] `OnPostureChanged_FiresOnAdd` — 事件正确触发

## 验收标准

1. 攻击命中时增加敌方架势
2. 完美弹刀恢复自身架势，大幅增加敌方架势
3. 普通格挡少量增加自身架势
4. 脱战 2 秒后架势以 15%/秒恢复
5. 格挡时恢复速度减半
6. 受击后恢复计时器重置
7. 架势归零时触发崩溃事件
8. 玩家崩溃进入 StunState，Boss 崩溃等待忍杀
