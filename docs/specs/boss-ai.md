# Boss AI 系统技术规格

## 设计意图

弦一郎 Boss AI 是只狼双向弹刀博弈的对手端核心。Boss 拥有丰富的招式表、动态行为权重和弹刀 AI，根据玩家行为实时决策出招，营造"越打越聪明"的战斗体验。

## 核心机制

### Boss 状态机

```
BossStateMachine
│
├── IdleState（待机）
│   └── 随机选择下一个行为
│
├── MoveState（移动/追击）
│   └── 靠近玩家到攻击距离
│
├── AttackState（攻击）
│   ├── Startup（前摇）
│   ├── Active（判定生效）
│   └── Recovery（后摇）
│
├── RangedState（远程射箭）
│
├── StaggerState（被弹刀硬直）
│   └── 短暂停顿，约 0.3 秒
│
├── CollapseState（架势崩溃）
│   └── 2 秒无法行动，等待忍杀
│
└── ExecutedState（被忍杀）
    └── 处决演出，战斗结束
```

### AI 决策流程

```
每帧 BossAI.Execute()：
│
├── 计算与玩家距离
├── 根据行为权重表随机选择招式
├── 考虑特殊触发条件：
│    ├── 玩家喝药 → 大概率突进攻击
│    ├── 玩家架势即将崩溃 → 激进攻击
│    ├── 玩家连砍 3 段以上 → 60% 概率进入弹刀姿态
│    └── 被玩家弹刀成功 → 短暂后退，下一招大概率防御
│
└── 出招 → AttackState
```

### Boss 弹刀 AI

```
Boss 弹刀由 AI 控制，不是玩家输入：

├── AI 在特定时机进入弹刀姿态
│    └── 姿态期间有弹刀窗口（9 帧，比玩家的 12 帧窄）
│
├── 玩家攻击落在窗口内 → Boss 弹刀成功
│    ├── 弹刀特效（小火花，比玩家弹刀略小）
│    ├── 帧冻结 0.03 秒
│    ├── 玩家架势大幅上升（基础值 × 1.5）
│    ├── 玩家攻击被弹回，短暂硬直（约 0.3 秒）
│    └── Boss 获得反击窗口 → 立即接反击招式
│
├── 落在窗口外 → Boss 普通格挡或未防御
│
└── 弹刀概率规则：
     ├── 玩家普攻命中时：40% 概率弹刀
     ├── 玩家连招中（2 段以上）：60% 概率弹刀
     ├── Boss 低血量（< 30%）：25% 概率（给玩家翻盘机会）
```

### 攻击表（剑 + 弓）

| 编号 | 招式名称 | 类型 | 伤害 | 破韧值 | 前摇(帧) | 判定(帧) | 后摇(帧) | 可弹刀 | 危字类型 |
|------|---------|------|------|--------|---------|---------|---------|--------|---------|
| 1 | 横斩 | 近战 | 100 | 15 | 10 | 4 | 12 | ✅ | 无 |
| 2 | 上挑 | 近战 | 110 | 15 | 12 | 4 | 14 | ✅ | 无 |
| 3 | 下劈 | 近战 | 120 | 20 | 14 | 5 | 16 | ✅ | 无 |
| 4 | 突刺 | 近战 | 150 | 25 | 16 | 6 | 18 | ❌ | 突刺↓ |
| 5 | 跳跃劈 | 近战 | 130 | 20 | 20 | 6 | 14 | ✅ | 无 |
| 6 | 扫击 | 近战 | 100 | 10 | 10 | 8 | 10 | ❌ | 扫击→ |
| 7 | 三连斩 | 近战 | 80+90+100 | 10+10+15 | 8+6+8 | 3+3+4 | 10+8+12 | ✅✅✅ | 无 |
| 8 | 射箭×3 | 远程 | 60×3 | 10×3 | - | - | - | - | 无 |
| 9 | 后撤步 | 位移 | 0 | 0 | 10 | - | - | - | 无 |

## 数据结构

```csharp
// 依赖：BossStats（见 data-layer.md）
// 依赖：BossDeflectConfig（见 data-layer.md）
// 依赖：AttackData（见 data-layer.md）

[CreateAssetMenu(fileName = "BossDeflectConfig", menuName = "Combat/BossDeflectConfig")]
public class BossDeflectConfig : ScriptableObject
{
    [Header("弹刀窗口")]
    public float deflectWindow = 0.15f;      // 9帧@60fps（比玩家12帧窄）
    public float blockWindow = 0.3f;         // 普通格挡窗口
    public float stanceDuration = 0.4f;      // 弹刀姿态总时长
    
    [Header("AI弹刀概率")]
    public float deflectChanceOnPlayerAttack = 0.4f;   // 玩家攻击时40%弹刀
    public float deflectChanceInCombo = 0.6f;          // 玩家连招中60%弹刀
    public float deflectChanceLowHealth = 0.25f;       // Boss低血量时25%
    
    [Header("惩罚参数")]
    public float posturePenaltyMultiplier = 1.5f;      // 被弹反后玩家架势升1.5倍
    public float playerHitStunDuration = 0.3f;         // 被弹反后玩家硬直时长
    public float hitStopDuration = 0.03f;              // 帧冻结时长
}

public enum BossAIState
{
    Idle,
    Move,
    Attack,
    Ranged,
    Stagger,
    Collapse,
    Executed
}
```

## 接口定义

```csharp
/// <summary>
/// Boss 弹刀系统，处理 Boss 端的弹刀判定和 AI 弹刀决策
/// </summary>
public class BossDeflectSystem
{
    public BossDeflectSystem(BossDeflectConfig config);
    
    /// <summary>
    /// AI 决定进入弹刀姿态
    /// </summary>
    public void EnterDeflectStance();
    
    /// <summary>
    /// 每帧调用，更新弹刀姿态计时器
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 检测玩家攻击是否被 Boss 弹开
    /// </summary>
    /// <param name="playerAttack">玩家攻击数据</param>
    /// <param name="attackDirection">攻击方向</param>
    /// <param name="bossForward">Boss 朝向前向量</param>
    /// <returns>弹刀结果：None / NormalBlock / PerfectDeflect</returns>
    public DeflectResult TryDeflect(AttackData playerAttack, Vector3 attackDirection, Vector3 bossForward);
    
    /// <summary>
    /// AI 弹刀概率判定：是否应该进入弹刀姿态
    /// </summary>
    /// <param name="bossHealthPercent">Boss 当前血量百分比</param>
    public bool ShouldEnterDeflectStance(float bossHealthPercent);
    
    /// <summary>
    /// 重置玩家连击计数（弹刀姿态结束时调用）
    /// </summary>
    public void ResetComboCount();
    
    public bool IsDeflectStance { get; }
}

/// <summary>
/// Boss AI 控制器，管理行为决策和状态转换
/// </summary>
public class BossAIController
{
    /// <summary>
    /// 根据当前状态选择下一个行为
    /// </summary>
    /// <param name="distanceToPlayer">与玩家的距离</param>
    /// <param name="playerState">玩家当前状态信息</param>
    /// <returns>选择的攻击数据（AttackData）</returns>
    public AttackData SelectAttack(float distanceToPlayer, PlayerStateInfo playerState);
    
    /// <summary>
    /// 切换到反击状态（弹刀成功后调用）
    /// </summary>
    public void TransitionToCounterAttackState();
    
    /// <summary>
    /// 切换到硬直状态
    /// </summary>
    /// <param name="duration">硬直时长（秒）</param>
    public void TransitionToStaggerState(float duration);
}

public enum DeflectResult
{
    None,           // 未弹刀
    NormalBlock,    // 普通格挡
    PerfectDeflect  // 完美弹刀
}
```

## 数值参数表

### Boss 属性

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| maxHealth | 1000 | 点 | 最大生命值 |
| maxPosture | 300 | 点 | 最大架势 |
| attack | 100 | 点 | 攻击力 |
| postureRecoveryRate | 10 | %/秒 | 架势恢复速度 |
| moveSpeed | 中等 | - | 移动速度 |

### Boss 弹刀参数

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| bossDeflectWindow | 0.15 | 秒 | 9帧@60fps（比玩家12帧窄） |
| blockWindow | 0.3 | 秒 | 普通格挡窗口 |
| stanceDuration | 0.4 | 秒 | 弹刀姿态总时长 |
| posturePenaltyMultiplier | 1.5 | 倍 | 被弹反后玩家架势升 1.5 倍 |
| playerHitStunDuration | 0.3 | 秒 | 被弹反后玩家硬直时长 |
| hitStopDuration | 0.03 | 秒 | 帧冻结时长 |

### AI 弹刀概率

| 参数 | 值 | 说明 |
|------|-----|------|
| deflectChanceOnAttack | 40% | 玩家普攻命中时弹刀概率 |
| deflectChanceInCombo | 60% | 玩家连招中弹刀概率 |
| deflectChanceLowHealth | 25% | Boss 低血量时弹刀概率 |

### AI 行为权重

| 行为 | 权重 | 触发条件 |
|------|------|---------|
| 横斩/上挑 | 25% | 距离 < 3 米 |
| 三连斩 | 20% | 距离 < 2.5 米 |
| 突刺 | 15% | 距离 < 3.5 米，随机触发 |
| 下劈 | 10% | 距离 < 2 米 |
| 跳跃劈 | 10% | 距离 < 4 米 |
| 扫击 | 5% | 距离 < 2 米，偶尔触发 |
| 后撤 + 射箭 | 15% | 距离 > 4 米 或 被连续攻击后 |

## 与其他系统的交互

### 输入

- `PlayerCombatController` → 玩家攻击命中事件、玩家连击计数
- `PostureSystem` → 双方架势值变化
- `HealthSystem` → Boss 血量
- `PlayerStateMachine` → 玩家当前状态（喝药/崩溃等）

### 输出

- `BossStateMachine` → 状态转换（Attack/Stagger/Collapse）
- `DeflectSystem` → Boss 弹刀成功时影响玩家架势
- `PostureSystem` → 增加/恢复架势
- `CombatEvents` → 广播 `OnBossPostureBreak` / `OnDeathblow`
- `VFXManager` → 弹刀特效
- `AudioManager` → 弹刀音效
- `UI` → Boss 血条、架势条

## 测试要点

### EditMode 单元测试

- [ ] `SelectAttack_Phase1_CloseRange_PrefersMelee` — 近距离优先选择近战招式
- [ ] `SelectAttack_Phase1_FarRange_PrefersRanged` — 远距离优先射箭
- [ ] `ShouldEnterDeflectStance_BaseProbability_40Percent` — 基础弹刀概率正确
- [ ] `ShouldEnterDeflectStance_PlayerCombo_IncreasedTo60Percent` — 连招时概率提升
- [ ] `ShouldEnterDeflectStance_LowHealth_DecreasedTo25Percent` — 低血量时概率降低
- [ ] `TryDeflect_InWindow_ReturnsPerfectDeflect` — 弹刀窗口内返回完美弹刀
- [ ] `TryDeflect_OutsideWindow_ReturnsNormalBlock` — 窗口外返回普通格挡
- [ ] `TryDeflect_WrongAngle_ReturnsNone` — 角度不对不弹刀
- [ ] `TryDeflect_NotInStance_ReturnsNone` — 未在弹刀姿态不弹刀
- [ ] `TransitionToStaggerState_SetsCorrectDuration` — 硬直时长正确
- [ ] `BossDeflect_OnSuccess_PlayerPostureIncreased` — Boss 弹刀成功后玩家架势上升

## 验收标准

1. Boss AI 根据与玩家距离和行为权重动态选择招式
2. 包含 9 种招式（含远程射箭）
3. Boss 弹刀窗口为 9 帧，比玩家的 12 帧窄
4. AI 弹刀概率根据玩家行为动态调整（40%→60%→25%）
5. 玩家连砍 3 段以上时 Boss 有 60% 概率弹刀
6. Boss 弹刀成功后立即获得反击窗口
7. Boss 架势归零时播放崩溃动画，等待忍杀
