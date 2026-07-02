# 危字与识破系统技术规格

## 设计意图

危字系统是只狼攻防博弈的高风险高回报维度。Boss 释放特殊攻击时屏幕出现红色"危"字提示，玩家必须在极短时间内判断危的类型并采取正确应对——下段危跳跃踩头、突刺危识破踩刀、擒拿危闪避拉开。识破（Mikiri Counter）是其中最具操作深度的机制，成功识破带来大量架势奖励和输出窗口，失败则承受全额伤害。

## 核心机制

### 三种危的应对

| 危类型 | UI 提示 | 攻击特征 | 正确应对 | 错误应对后果 |
|--------|---------|---------|---------|-------------|
| 下段危 | 红色↓符号 | 敌人身体下沉，武器低位横扫 | 空格跳跃 → 可踩头输出 | 被扫中 + 架势上升 |
| 突刺危 | 红色↓符号 | 敌人身体前倾，武器直线前刺 | Shift 识破 → 踩刀，敌方架势大幅↑ | 被刺中 + 架势上升 |
| 擒拿危 | 红色↑符号 | 敌人伸手/武器准备抓取 | Shift + 方向键闪避拉开距离 | 被抓住，高伤害 |

### 下段危 — 跳跃踩头

```
敌方发动低位横扫攻击（AttackType == Sweep）：

├── 屏幕显示红色危字 + ↓ 符号
├── 玩家按空格跳跃
│    ├── 跳跃成功：跳过攻击判定范围
│    │    ├── 玩家在空中可追加攻击
│    │    └── 可踩头：对敌方造成中量架势伤害
│    └── 跳跃失败（时机不对/未跳）：被扫中，受伤 + 架势上升
│
└── 踩头判定：
     ├── 玩家处于下落阶段
     ├── 玩家位置在敌人头顶区域（Y 轴偏移 < 0.5m）
     ├── 水平距离 < 1.5m
     └── 满足 → 触发踩头，敌方架势 +中量
```

### 突刺危 — 识破踩刀

```
敌方发动突刺型攻击（AttackType == Thrust）：

├── 屏幕显示红色危字 + ↓ 符号
├── 玩家按 Shift（不按方向键）
│    ├── 满足识破条件 → 【识破成功】
│    │    ├── 播放识破动画（前踏一步，踩住敌人的刀）
│    │    ├── "咔嚓"踩断音效
│    │    ├── 帧冻结 4 帧（0.067 秒）
│    │    ├── 屏幕震动（振幅 0.1，0.2 秒）
│    │    ├── 敌方架势大幅上升（比弹刀更多）
│    │    ├── 敌人短暂硬直（约 0.5 秒）
│    │    └── 玩家可立即接攻击输出
│    │
│    └── 不满足识破条件 → 普通闪避
│         └── 距离太远 / 角度偏差 → 闪避但不踩刀
│
└── 识破失败：
     ├── 突刺型攻击没按 Shift → 被刺中，受伤 + 架势上升
     └── 非突刺型攻击按 Shift → 普通侧闪/后闪（无惩罚）
```

### 擒拿危 — 闪避拉开

```
敌方发动投技攻击（AttackType == Grab）：

├── 屏幕显示红色危字 + ↑ 符号
├── 玩家按 Shift + 方向键
│    ├── 侧向/后向闪避 → 拉开距离，躲开投技
│    └── 未闪避/方向不对 → 被抓住，高伤害
│
└── 特点：
     ├── 动作较慢但抓住后伤害极高
     ├── 无额外架势奖励（纯规避机制）
     └── 闪避无敌帧约 6-8 帧
```

### 识破判定条件

```
识破触发必须同时满足以下条件：

├── 条件 1：攻击类型
│    └── incomingAttack.attackType == AttackType.Thrust
│
├── 条件 2：距离
│    └── Vector3.Distance(player, enemy) < mikiriDistance (3m)
│
├── 条件 3：角度
│    └── Vector3.Angle(enemyAttackDirection, playerForward) < mikiriAngle (60°)
│
├── 条件 4：时机
│    └── 在攻击判定期（Active 帧）内按下 Shift
│
├── 条件 5：输入
│    └── 按下 Shift 且未按方向键（默认前踏识破）
│
└── 全部满足 → MikiriState → 识破成功
     任一不满足 → DodgeState → 普通闪避
```

### 危字提示时机

```
Boss 进入攻击前摇（Startup）时：
  1. 检查 AttackData.attackType
  2. 如果是 Thrust / Sweep / Grab → 触发危字提示
  3. UI 显示红色危字 + 方向符号
  4. 提示持续整个前摇 + 判定帧
  5. 后摇开始时隐藏危字
```

## 数据结构

```csharp
// 依赖：AttackData（见 data-layer.md）
// 依赖：CombatEvents（事件广播）
// 依赖：PostureSystem（见 posture-system.md）

/// <summary>
/// 攻击类型枚举，决定危字种类和应对方式
/// </summary>
public enum AttackType
{
    Normal,       // 普通（可弹刀，无危字）
    Thrust,       // 突刺（需识破，红色↓危字）
    Sweep,        // 扫击（需跳跃，红色→危字）
    Grab,         // 投技（需闪避，红色↑危字）
    Lightning     // 雷电（需雷电反击，红色↓危字带雷电特效）
}

/// <summary>
/// 危字攻击配置数据
/// </summary>
[System.Serializable]
public class DangerData
{
    /// <summary>攻击类型</summary>
    public AttackType attackType;
    
    /// <summary>危字 UI 显示名称</summary>
    public string dangerLabel;
    
    /// <summary>识破成功对敌方架势伤害</summary>
    public float mikiriPostureDamage;
    
    /// <summary>跳跃踩头对敌方架势伤害</summary>
    public float stompPostureDamage;
    
    /// <summary>危字提示提前量（秒），在前摇开始前显示</summary>
    public float dangerUILeadTime;
}

public class DangerSystem
{
    // 从 DangerConfig ScriptableObject 读取
    private DangerConfig _config;
    
    // 运行时状态
    private bool _isDangerActive = false;
    private AttackType _currentDangerType;
    private float _dangerTimer = 0f;
    private bool _mikiriTriggered = false;
}
```

## 接口定义

```csharp
/// <summary>
/// 危字与识破系统，管理危字提示、识破判定、踩头判定
/// </summary>
public class DangerSystem
{
    public DangerSystem(DangerConfig config);
    
    /// <summary>
    /// Boss 发动危字攻击时调用，激活危字提示
    /// </summary>
    /// <param name="attackData">攻击数据（含 attackType）</param>
    /// <param name="dangerData">危字配置数据</param>
    public void OnDangerAttackStart(AttackData attackData, DangerData dangerData);
    
    /// <summary>
    /// Boss 攻击结束时调用，清除危字提示
    /// </summary>
    public void OnDangerAttackEnd();
    
    /// <summary>
    /// 每帧调用，更新危字状态和判定窗口
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 玩家按下 Shift 时调用，尝试识破或闪避
    /// </summary>
    /// <param name="playerPosition">玩家位置</param>
    /// <param name="playerForward">玩家前向量</param>
    /// <param name="enemyPosition">敌人位置</param>
    /// <param name="attackDirection">攻击方向</param>
    /// <param name="hasDirectionInput">是否有方向键输入</param>
    /// <returns>识破结果</returns>
    public MikiriResult TryMikiri(Vector3 playerPosition, Vector3 playerForward,
        Vector3 enemyPosition, Vector3 attackDirection, bool hasDirectionInput);
    
    /// <summary>
    /// 检测跳跃踩头是否成功
    /// </summary>
    /// <param name="playerPosition">玩家位置</param>
    /// <param name="playerVelocityY">玩家 Y 轴速度</param>
    /// <param name="enemyPosition">敌人位置</param>
    /// <returns>踩头是否成功</returns>
    public bool TryStomp(Vector3 playerPosition, float playerVelocityY, Vector3 enemyPosition);
    
    /// <summary>
    /// 当前是否有危字攻击进行中
    /// </summary>
    public bool IsDangerActive { get; }
    
    /// <summary>
    /// 当前危字类型
    /// </summary>
    public AttackType CurrentDangerType { get; }
    
    /// <summary>
    /// 识破成功事件
    /// </summary>
    public event System.Action OnMikiriSuccess;
    
    /// <summary>
    /// 踩头成功事件
    /// </summary>
    public event System.Action OnStompSuccess;
    
    /// <summary>
    /// 危字提示激活事件（攻击类型）
    /// </summary>
    public event System.Action<AttackType> OnDangerWarningShown;
    
    /// <summary>
    /// 危字提示隐藏事件
    /// </summary>
    public event System.Action OnDangerWarningHidden;
}

/// <summary>
/// 识破结果
/// </summary>
public enum MikiriResult
{
    None,          // 未触发
    Success,       // 识破成功
    DodgeForward,  // 前向闪避（非突刺型 / 条件不满足）
    DodgeSide,     // 侧向闪避
    DodgeBack      // 后向闪避
}
```

## 数值参数表

### 识破参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| mikiriDistance | 3.0 | 米 | 识破最大触发距离 |
| mikiriAngle | 60 | 度 | 攻击方向与玩家朝向最大夹角 |
| mikiriPostureDamage | 50 | 点 | 识破成功对敌方架势伤害 |
| mikiriEnemyStunDuration | 0.5 | 秒 | 识破成功后敌人硬直时长 |
| mikiriHitStopDuration | 0.067 | 秒 | 4 帧@60fps 帧冻结 |
| mikiriScreenShakeAmplitude | 0.1 | - | 屏幕震动振幅 |
| mikiriScreenShakeDuration | 0.2 | 秒 | 屏幕震动时长 |

### 踩头参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| stompPostureDamage | 30 | 点 | 踩头对敌方架势伤害 |
| stompHorizontalRange | 1.5 | 米 | 踩头水平判定范围 |
| stompVerticalOffset | 0.5 | 米 | 踩头 Y 轴偏移容忍值 |
| stompHitStopDuration | 0.05 | 秒 | 3 帧@60fps 帧冻结 |
| stompScreenShakeAmplitude | 0.05 | - | 屏幕震动振幅 |

### 闪避参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| dodgeInvincibilityFrames | 6-8 | 帧 | 闪避无敌帧 |
| dodgeSpeed | 10 | 米/秒 | 闪避移动速度 |
| dodgeCooldown | 0.3 | 秒 | 闪避冷却 |

### 危字提示参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| dangerUILeadTime | 0.1 | 秒 | 危字在前摇开始前显示的提前量 |
| dangerUIDisplayDuration | - | - | 持续整个前摇 + 判定帧 |

## 与其他系统的交互

### 输入

- `BossAIController` → Boss 发动危字攻击时通知 DangerSystem
- `AttackData` → 攻击类型（attackType）决定危字种类
- `PlayerStateMachine` → 玩家当前状态（是否可执行识破/跳跃/闪避）
- `InputReader` → Shift 按下事件、方向键输入、空格跳跃

### 输出

- `PlayerStateMachine` → 识破成功进入 MikiriState / 闪避进入 DodgeState / 跳跃进入 JumpState
- `PostureSystem` → 识破成功增加敌方架势、踩头增加敌方架势
- `HitStopManager` → 识破触发帧冻结（4 帧）、踩头触发帧冻结（3 帧）
- `CombatEvents` → 广播 `OnMikiriCounter`
- `VFXManager` → 识破踩刀特效、踩头特效、危字 UI 特效
- `AudioManager` → "咔嚓"踩断音效、踩头沉闷打击声
- `UI` → 红色危字提示显示/隐藏

## 测试要点

### EditMode 单元测试

- [ ] `TryMikiri_ThrustAttack_InRange_InAngle_ReturnsSuccess` — 突刺型 + 距离角度满足 → 识破成功
- [ ] `TryMikiri_ThrustAttack_OutOfRange_ReturnsDodgeForward` — 距离过远 → 普通闪避
- [ ] `TryMikiri_ThrustAttack_WrongAngle_ReturnsDodgeForward` — 角度偏差 → 普通闪避
- [ ] `TryMikiri_NormalAttack_ReturnsDodgeForward` — 非突刺型攻击 → 普通闪避
- [ ] `TryMikiri_WithDirectionInput_ReturnsDodgeSide` — 有方向键输入 → 侧闪
- [ ] `TryMikiri_BackwardInput_ReturnsDodgeBack` — 后向输入 → 后闪
- [ ] `TryStomp_FallingPhase_InRange_ReturnsTrue` — 下落阶段 + 范围内 → 踩头成功
- [ ] `TryStomp_RisingPhase_ReturnsFalse` — 上升阶段 → 踩头失败
- [ ] `TryStomp_OutOfHorizontalRange_ReturnsFalse` — 水平距离过远 → 踩头失败
- [ ] `OnDangerAttackStart_SetsDangerActive` — 危字攻击开始时激活状态
- [ ] `OnDangerAttackEnd_ClearsDangerActive` — 危字攻击结束时清除状态
- [ ] `OnDangerAttackStart_SweepType_SetsCorrectDangerType` — 扫击型正确设置危险类型
- [ ] `MikiriPostureDamage_AppliesCorrectValue` — 识破架势伤害数值正确

### PlayMode 集成测试

- [ ] `DangerWarning_UI_DisplayedDuringStartup` — 前摇期间 UI 显示危字
- [ ] `DangerWarning_UI_HiddenAfterAttack` — 攻击结束后 UI 隐藏危字
- [ ] `Mikiri_FullFlow_StunEnemyAndAllowFollowUp` — 完整识破流程：踩刀 → 敌人硬直 → 可追击

## 验收标准

1. Boss 发动危字攻击时，屏幕正确显示红色危字 + 方向符号
2. 下段危（Sweep）：跳跃可规避，空中踩头造成架势伤害
3. 突刺危（Thrust）：满足距离 < 3m、角度 < 60° 时按 Shift 触发识破
4. 识破成功播放踩刀动画 + "咔嚓"音效 + 帧冻结 + 屏幕震动
5. 识破成功敌方架势大幅上升（50 点），敌人硬直 0.5 秒
6. 不满足识破条件时按 Shift 执行普通闪避（无惩罚）
7. 擒拿危（Grab）：闪避可规避，无架势奖励
8. 危字提示在前摇开始时显示，攻击结束后隐藏
9. 非危字攻击按 Shift 正常执行闪避
