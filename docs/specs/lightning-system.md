# 雷电反击系统技术规格

> **优先级：低（仅二阶段）** — 当前一阶段优先实现弹刀、架势、危字系统，雷电反击在二阶段开发时实现。

## 设计意图

雷电反击是弦一郎二阶段的核心机制，也是整场 Boss 战的高潮体验。弦一郎跳起蓄雷后释放雷电攻击，玩家需要在空中接雷并弹回，成功弹雷使 Boss 进入长时间硬直，获得大量输出窗口。这是只狼中最具视觉冲击力和操作满足感的机制之一。

## 核心机制

### 雷电反击完整流程

```
弦一郎跳起蓄雷（红色危字提示 + 雷电特效）：

├── 第一阶段：雷电下落（Falling）
│    ├── 弦一郎跳起蓄雷，红色危字提示
│    ├── 雷电下落击中玩家
│    ├── 玩家进入"带电"状态
│    │    ├── 角色全身带电特效
│    │    └── 进入短暂的输入等待窗口
│    └── 如果玩家不做任何操作 → 受到雷电伤害 + 架势大幅上升
│
├── 第二阶段：接雷（Charged）
│    ├── 玩家在空中按左键 → 角色双手举起
│    ├── 雷电停在手中，短暂定格感（约 0.3 秒）
│    ├── 进入输入等待窗口（约 1 秒）
│    └── 如果超时未按左键 → 受到雷电伤害
│
├── 第三阶段：弹雷（Reflected）
│    ├── 在等待窗口内再次按左键
│    ├── 雷电被掷回弦一郎
│    ├── 弦一郎被电麻（约 1.5 秒硬直）
│    ├── 弦一郎架势大幅上升
│    ├── 屏幕震动 + 闪电特效
│    ├── 帧冻结 6 帧（0.1 秒）
│    └── 大量输出窗口
│
└── 失败情况（Failed）
     ├── 未在空中按左键 → 被电，大伤害
     ├── 按了但没及时弹 → 被电，大伤害
     └── 接地状态接雷 → 不触发，直接受伤害
```

### 雷电反击状态机

```
LightningCounterSystem 状态流转：

None ──(被雷电击中)──→ Falling
                          │
                     (空中按左键)
                          │
                          ▼
                       Charged ──(超时1秒)──→ Failed
                          │
                     (再次按左键)
                          │
                          ▼
                      Reflected ──(弹雷命中Boss)──→ None
                                                    │
                                              Boss 进入硬直
                                              （1.5秒）

任何状态 ──(玩家落地)──→ Failed（必须在空中完成）
```

### 各阶段输入窗口

```
┌──────────────┬──────────────┬───────────────────────┐
│ 阶段         │ 玩家输入      │ 超时/错误后果          │
├──────────────┼──────────────┼───────────────────────┤
│ Falling      │ 无（等待受击） │ 被雷击中 → 受伤+架势↑ │
│ Charged      │ 按左键接雷    │ 超时1秒 → 受伤         │
│ Reflected    │ 按左键弹雷    │ 超时1秒 → 受伤         │
│ （弹雷后）   │ 无需输入      │ Boss硬直1.5秒          │
└──────────────┴──────────────┴───────────────────────┘

关键约束：
  - 接雷和弹雷都必须在空中完成
  - 玩家落地时如果仍在 Charged 状态 → 自动 Failed
  - 弹雷方向自动朝向弦一郎（不需要瞄准）
```

## 数据结构

```csharp
// 依赖：AttackData（见 data-layer.md）
// 依赖：CombatEvents（事件广播）
// 依赖：PostureSystem（见 posture-system.md）

/// <summary>
/// 雷电反击状态枚举
/// </summary>
public enum LightningState
{
    None,           // 未触发
    Falling,        // 雷电下落中，等待被击中
    Charged,        // 已接雷，等待弹回
    Reflected,      // 已弹回，雷电飞向Boss
    Failed          // 失败，被电
}

/// <summary>
/// 雷电反击配置（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "LightningConfig", menuName = "Combat/LightningConfig")]
public class LightningConfig : ScriptableObject
{
    [Header("输入窗口")]
    public float chargedInputWindow = 1.0f;      // 接雷后等待弹雷的窗口（秒）
    public float reflectInputWindow = 1.0f;       // 弹雷输入窗口（秒）
    public float catchFreezeTime = 0.3f;          // 接雷定格感时长（秒）
    
    [Header("伤害参数")]
    public float lightningDamage = 200f;          // 雷电伤害（失败时）
    public float lightningPostureDamage = 40f;    // 雷电架势伤害（失败时）
    
    [Header("弹雷效果")]
    public float bossStunDuration = 1.5f;         // Boss 被电麻硬直时长（秒）
    public float bossPostureDamageOnReflect = 60f;// 弹雷对Boss架势伤害
    public float reflectHitStopDuration = 0.1f;   // 6帧@60fps 帧冻结
    
    [Header("视觉反馈")]
    public float screenShakeAmplitude = 0.15f;    // 屏幕震动振幅
    public float screenShakeDuration = 0.3f;      // 屏幕震动时长（秒）
}

public class LightningCounterSystem
{
    private LightningConfig _config;
    
    // 运行时状态
    private LightningState _state = LightningState.None;
    private float _inputTimer = 0f;
    private bool _isPlayerAirborne = false;
}
```

## 接口定义

```csharp
/// <summary>
/// 雷电反击系统，管理接雷、弹雷的状态流转和判定
/// </summary>
public class LightningCounterSystem
{
    public LightningCounterSystem(LightningConfig config);
    
    /// <summary>
    /// Boss 发动雷电攻击时调用，进入 Falling 状态
    /// </summary>
    public void OnLightningAttackStart();
    
    /// <summary>
    /// 玩家被雷电击中时调用
    /// </summary>
    /// <param name="isAirborne">玩家是否在空中</param>
    public void OnLightningHit(bool isAirborne);
    
    /// <summary>
    /// 玩家按下左键时调用，尝试接雷或弹雷
    /// </summary>
    /// <param name="isAirborne">玩家是否在空中</param>
    /// <returns>操作结果</returns>
    public LightningActionResult OnAttackPressed(bool isAirborne);
    
    /// <summary>
    /// 每帧调用，更新雷电状态和输入窗口计时器
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 玩家落地时调用，如果在 Charged 状态则判定失败
    /// </summary>
    public void OnPlayerLanded();
    
    /// <summary>
    /// 弹雷命中 Boss 时调用
    /// </summary>
    public void OnReflectHitBoss();
    
    /// <summary>
    /// 重置系统状态
    /// </summary>
    public void Reset();
    
    /// <summary>
    /// 当前雷电状态
    /// </summary>
    public LightningState CurrentState { get; }
    
    /// <summary>
    /// 是否处于雷电反击流程中
    /// </summary>
    public bool IsActive { get; }
    
    /// <summary>
    /// 当前输入窗口剩余时间
    /// </summary>
    public float InputWindowRemaining { get; }
    
    /// <summary>
    /// 雷电反击成功事件
    /// </summary>
    public event System.Action OnLightningCounterSuccess;
    
    /// <summary>
    /// 雷电反击失败事件
    /// </summary>
    public event System.Action OnLightningCounterFail;
    
    /// <summary>
    /// 状态变化事件
    /// </summary>
    public event System.Action<LightningState> OnStateChanged;
}

/// <summary>
/// 雷电反击操作结果
/// </summary>
public enum LightningActionResult
{
    None,              // 无操作
    CaughtLightning,   // 成功接雷
    ReflectedLightning,// 成功弹雷
    FailedOnGround,    // 失败：在地面接雷
    FailedTimeout      // 失败：输入超时
}
```

## 数值参数表

### 输入窗口

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| chargedInputWindow | 1.0 | 秒 | 接雷后等待弹雷的窗口 |
| reflectInputWindow | 1.0 | 秒 | 弹雷输入窗口 |
| catchFreezeTime | 0.3 | 秒 | 接雷定格感时长 |

### 伤害参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| lightningDamage | 200 | 点 | 雷电伤害（失败时） |
| lightningPostureDamage | 40 | 点 | 雷电架势伤害（失败时） |

### 弹雷效果

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| bossStunDuration | 1.5 | 秒 | Boss 被电麻硬直时长 |
| bossPostureDamageOnReflect | 60 | 点 | 弹雷对 Boss 架势伤害 |
| reflectHitStopDuration | 0.1 | 秒 | 6 帧@60fps 帧冻结 |
| screenShakeAmplitude | 0.15 | - | 屏幕震动振幅 |
| screenShakeDuration | 0.3 | 秒 | 屏幕震动时长 |

## 与其他系统的交互

### 输入

- `BossAIController` → Boss 发动雷电攻击时通知 LightningCounterSystem
- `InputReader` → 左键按下事件
- `PlayerStateMachine` → 玩家是否在空中（AirborneState）
- `DangerSystem` → 雷电攻击标记为 AttackType.Lightning 危字

### 输出

- `PlayerStateMachine` → 进入 LightningChargeState（接雷状态）
- `BossStateMachine` → 弹雷命中时 Boss 进入 StaggerState（1.5 秒硬直）
- `PostureSystem` → 弹雷成功增加 Boss 架势（60 点）、失败增加玩家架势（40 点）
- `DamageSystem` → 失败时对玩家造成雷电伤害（200 点）
- `HitStopManager` → 弹雷成功触发帧冻结（6 帧）
- `CombatEvents` → 广播 `OnLightningCounterSuccess` / `OnLightningCounterFail`
- `VFXManager` → 全身带电特效、弹雷闪电特效、全屏闪白
- `AudioManager` → 雷电蓄力音效、接雷音效、雷电爆裂声
- `UI` → 雷电危字提示

## 测试要点

### EditMode 单元测试

- [ ] `OnLightningHit_Airbone_TransitionsToCharged` — 空中被雷击中 → Charged 状态
- [ ] `OnLightningHit_OnGround_TransitionsToFailed` — 地面被雷击中 → Failed 状态
- [ ] `OnAttackPressed_InChargedState_TransitionsToReflected` — Charged 状态按左键 → Reflected
- [ ] `OnAttackPressed_InChargedState_OnGround_ReturnsFailedOnGround` — 落地后按左键 → 失败
- [ ] `OnAttackPressed_NoActiveLightning_ReturnsNone` — 无雷电时按左键 → 无操作
- [ ] `Update_ChargedTimeout_TransitionsToFailed` — 接雷后超时 1 秒 → Failed
- [ ] `Update_ReflectedTimeout_TransitionsToFailed` — 弹雷超时 → Failed
- [ ] `OnPlayerLanded_InChargedState_TransitionsToFailed` — Charged 状态落地 → Failed
- [ ] `OnPlayerLanded_InNoneState_NoEffect` — 非活动状态落地 → 无影响
- [ ] `OnReflectHitBoss_TriggersBossStun` — 弹雷命中 → Boss 硬直 1.5 秒
- [ ] `OnReflectHitBoss_PostureDamageApplied` — 弹雷架势伤害正确（60 点）
- [ ] `Reset_SetsStateToNone` — 重置后状态为 None
- [ ] `CurrentState_ReturnsCorrectState` — 状态属性返回正确值
- [ ] `InputWindowRemaining_DecreasesOverTime` — 输入窗口随时间递减

### PlayMode 集成测试

- [ ] `Lightning_FullFlow_CatchAndReflect_StunsBoss` — 完整流程：被雷击 → 接雷 → 弹雷 → Boss 硬直
- [ ] `Lightning_FullFlow_Timeout_PlayerDamaged` — 超时流程：被雷击 → 接雷 → 超时 → 受伤
- [ ] `Lightning_LandDuringCharged_Fails` — 接雷后落地 → 失败

## 验收标准

1. Boss 雷电攻击时显示红色危字 + 雷电特效提示
2. 玩家在空中被雷击中后进入 Charged 状态，全身带电特效
3. Charged 状态按左键成功接雷，短暂定格感（0.3 秒）
4. 接雷后 1 秒内再次按左键成功弹雷，雷电飞向 Boss
5. 弹雷命中 Boss → Boss 硬直 1.5 秒 + 架势 +60 + 帧冻结 + 屏幕震动
6. 接雷后超时 1 秒未按左键 → 受到雷电伤害（200 点）+ 架势 +40
7. 在地面被雷击中 → 直接失败，受到伤害
8. Charged 状态落地 → 自动失败
9. 弹雷方向自动朝向 Boss（无需瞄准）
10. 雷电反击成功后玩家可立即接攻击输出
