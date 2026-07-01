# Harness 文档体系实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 为只狼 ARPG 战斗 Demo 搭建完整的 Harness 文档体系，让 AI Agent 能够按需加载上下文、自主开发并自我验证。

**架构：** 采用模块化文档体系：CLAUDE.md 作为 Agent 行为手册入口，`specs/` 存放 8 份系统技术规格，`architecture/` 存放 3 份架构文档，`testing/` 存放测试计划。每份 spec 文档遵循统一结构（设计意图、核心机制、数据结构、接口定义、数值参数、系统交互、测试要点、验收标准）。

**技术栈：** Markdown 文档 + Unity Test Framework（EditMode/PlayMode）

---

## 文件结构

```
CLAUDE.md                                    ← 修改：新增 Harness 相关章节
DOCS/specs/deflect-system.md                 ← 创建：弹刀系统技术规格
DOCS/specs/posture-system.md                 ← 创建：架势条系统技术规格
DOCS/specs/boss-ai.md                        ← 创建：Boss AI 技术规格
DOCS/specs/danger-system.md                  ← 创建：危字+识破系统技术规格
DOCS/specs/lightning-system.md               ← 创建：雷电反击系统技术规格
DOCS/specs/input-system.md                   ← 创建：输入系统技术规格
DOCS/specs/animation-system.md               ← 创建：动画系统技术规格
DOCS/specs/ui-hud.md                         ← 创建：UI/HUD 技术规格
DOCS/architecture/code-structure.md          ← 创建：代码目录结构文档
DOCS/architecture/state-machine.md           ← 创建：HFSM 框架设计文档
DOCS/architecture/data-layer.md              ← 创建：ScriptableObject 数据层文档
DOCS/testing/test-plan.md                    ← 创建：测试计划
Assets/Tests/EditMode/.gitkeep               ← 创建：EditMode 测试目录
Assets/Tests/PlayMode/.gitkeep               ← 创建：PlayMode 测试目录
```

---

### 任务 1：创建目录结构

**文件：**
- 创建：`DOCS/specs/.gitkeep`
- 创建：`DOCS/architecture/.gitkeep`
- 创建：`DOCS/testing/.gitkeep`
- 创建：`Assets/Tests/EditMode/.gitkeep`
- 创建：`Assets/Tests/PlayMode/.gitkeep`

- [ ] **步骤 1：创建 docs 子目录**

```bash
mkdir -p DOCS/specs DOCS/architecture DOCS/testing
```

- [ ] **步骤 2：创建测试目录**

```bash
mkdir -p Assets/Tests/EditMode Assets/Tests/PlayMode
```

- [ ] **步骤 3：添加 .gitkeep 文件**

```bash
touch DOCS/specs/.gitkeep DOCS/architecture/.gitkeep DOCS/testing/.gitkeep
touch Assets/Tests/EditMode/.gitkeep Assets/Tests/PlayMode/.gitkeep
```

- [ ] **步骤 4：Commit**

```bash
git add DOCS/ Assets/Tests/
git commit -m "chore: 创建 Harness 文档和测试目录结构"
```

---

### 任务 2：增强 CLAUDE.md

**文件：**
- 修改：`CLAUDE.md`

- [ ] **步骤 1：在 CLAUDE.md 末尾追加 Harness 相关章节**

在现有的 superpowers-zh 注释块之后，追加以下内容：

```markdown

---

## Harness 工程配置

> 本节定义 AI Agent 在本项目中必须遵守的架构约束、代码规范和工作流。

### 架构约束（不可违反）

- 所有战斗参数必须用 `ScriptableObject`，禁止硬编码数值
- 状态机使用 HFSM 模式（继承 `StateMachine` 基类），禁止在 `MonoBehaviour.Update` 中写 switch-case
- 模块间通信走 `CombatEvents` 事件系统，禁止直接引用其他模块
- 弹刀判定用 `Physics.OverlapSphere` 每帧检测，不用 `OnTriggerEnter`
- `Player/` 和 `Boss/` 模块不能直接互相引用，只能通过事件通信

### 代码规范

- 命名规则：类名 `PascalCase`，方法名 `PascalCase`，私有字段 `_camelCase`
- 每个公开方法必须有 XML 文档注释（`/// <summary>`）
- 单个脚本文件不超过 300 行
- 一次提交只做一件事（原子性）

### 测试要求

- 新增数值逻辑 → 必须配套 EditMode 单元测试
- 新增状态转换 → 必须配套 PlayMode 集成测试
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 测试运行方式：Unity Editor → Window → General → Test Runner

### 文档加载指引

当 Agent 接到任务时，根据任务类型加载对应文档：

| 任务类型 | 必读文档 | 可选文档 |
|---------|---------|---------|
| 弹刀系统 | `DOCS/specs/deflect-system.md` | `DOCS/architecture/code-structure.md` |
| 架势条 | `DOCS/specs/posture-system.md` | `DOCS/architecture/data-layer.md` |
| Boss AI | `DOCS/specs/boss-ai.md` | `DOCS/architecture/state-machine.md` |
| 危字/识破 | `DOCS/specs/danger-system.md` | `DOCS/specs/animation-system.md` |
| 雷电反击 | `DOCS/specs/lightning-system.md` | `DOCS/architecture/state-machine.md` |
| 输入系统 | `DOCS/specs/input-system.md` | `DOCS/architecture/code-structure.md` |
| 动画集成 | `DOCS/specs/animation-system.md` | — |
| UI/HUD | `DOCS/specs/ui-hud.md` | — |
| HFSM 框架 | `DOCS/architecture/state-machine.md` | — |
| 数据层 | `DOCS/architecture/data-layer.md` | — |

### 工作流（TDD 强制）

1. 读取相关规格文档（参考上方指引表）
2. 编写测试（定义"完成"的标准）
3. 编写实现代码（遵循架构约束和代码规范）
4. 运行测试，全部通过后提交
5. 运行验证（游戏能否启动，有无编译错误）
```

- [ ] **步骤 2：Commit**

```bash
git add CLAUDE.md
git commit -m "docs: 增强 CLAUDE.md，加入 Harness 架构约束和工作流"
```

---

### 任务 3：编写 code-structure.md

**文件：**
- 创建：`DOCS/architecture/code-structure.md`

- [ ] **步骤 1：编写代码目录结构文档**

内容如下：

```markdown
# 代码目录结构

## 总览

```
Assets/
├── Scripts/
│   ├── Core/                      ← 框架层（Agent 不应修改）
│   │   ├── StateMachine/          ← HFSM 基类
│   │   ├── Events/                ← CombatEvents 事件总线
│   │   ├── Input/                 ← 输入抽象层
│   │   └── Utils/                 ← 工具类
│   │
│   ├── Player/                    ← 玩家模块
│   │   ├── States/                ← 玩家状态（IdleState, AttackState...）
│   │   ├── Combat/                ← 玩家战斗逻辑
│   │   └── Movement/              ← 移动控制
│   │
│   ├── Boss/                      ← Boss 模块
│   │   ├── AI/                    ← AI 决策逻辑
│   │   ├── States/                ← Boss 状态
│   │   └── Attacks/               ← 招式实现
│   │
│   ├── Combat/                    ← 共享战斗系统（玩家和 Boss 都用）
│   │   ├── DeflectSystem.cs
│   │   ├── PostureSystem.cs
│   │   ├── DamageCalculator.cs
│   │   ├── DangerSystem.cs
│   │   ├── LightningSystem.cs
│   │   └── HitStopManager.cs
│   │
│   ├── UI/                        ← UI 逻辑
│   ├── Camera/                    ← 相机控制
│   └── Audio/                     ← 音效管理
│
├── ScriptableObjects/             ← 数据资产（运行时生成 + 编辑器配置）
│   ├── Player/
│   └── Boss/
│
├── Tests/
│   ├── EditMode/                  ← 单元测试（不需要运行游戏）
│   └── PlayMode/                  ← 集成测试（需要运行游戏）
│
└── Resources/                     ← 美术资源（已有，不纳入 git）
```

## 模块边界规则

### 依赖方向

```
Core/ ← 被所有模块依赖，自身不依赖 Player/Boss/Combat
Combat/ ← 被 Player 和 Boss 依赖，自身不依赖它们
Player/ ← 依赖 Combat/ 和 Core/，不依赖 Boss/
Boss/ ← 依赖 Combat/ 和 Core/，不依赖 Player/
```

### 禁止的依赖

| 禁止 | 原因 |
|------|------|
| `Player/` 直接引用 `Boss/` | 通过 CombatEvents 通信 |
| `Boss/` 直接引用 `Player/` | 通过 CombatEvents 通信 |
| `Combat/` 引用 `Player/` 或 `Boss/` | Combat 是共享层，必须中立 |
| `Core/` 引用业务模块 | Core 是纯框架层 |

### 模块职责

| 模块 | 职责 | 示例文件 |
|------|------|---------|
| Core/StateMachine | HFSM 基类和状态转换逻辑 | `StateMachine.cs`, `State.cs` |
| Core/Events | 事件总线，模块间解耦通信 | `CombatEvents.cs` |
| Core/Input | 输入抽象，支持新旧输入系统 | `InputReader.cs` |
| Combat/Deflect | 弹刀判定、抖刀惩罚、连续加成 | `DeflectSystem.cs` |
| Combat/Posture | 架势条管理、恢复、崩溃判定 | `PostureSystem.cs` |
| Combat/Damage | 伤害计算、格挡减伤 | `DamageCalculator.cs` |
| Player/States | 玩家各状态实现 | `IdleState.cs`, `AttackState.cs` |
| Player/Combat | 玩家战斗逻辑（攻击、弹刀输入） | `PlayerCombatController.cs` |
| Boss/AI | Boss AI 决策、招式选择 | `BossAIController.cs` |
| Boss/Attacks | Boss 各招式实现 | `HorizontalSlash.cs`, `ThrustAttack.cs` |

## 新增文件指南

当添加新功能时：

1. **判断所属模块** — 这个功能属于哪个模块？
2. **检查边界** — 是否违反了依赖规则？
3. **选择位置** — 放在对应模块的子目录下
4. **命名规范** — 文件名使用 PascalCase，与主要类名一致
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/architecture/code-structure.md
git commit -m "docs: 添加代码目录结构和模块边界文档"
```

---

### 任务 4：编写 state-machine.md

**文件：**
- 创建：`DOCS/architecture/state-machine.md`

- [ ] **步骤 1：编写 HFSM 框架设计文档**

内容如下：

```markdown
# HFSM 框架设计

## 设计意图

分层状态机（HFSM）是本项目的核心架构模式，用于管理玩家和 Boss 的复杂行为。避免在 MonoBehaviour.Update 中使用 switch-case。

## 核心抽象

### StateMachine 基类

```csharp
/// <summary>
/// 状态机容器，管理当前状态和状态转换
/// </summary>
public class StateMachine
{
    protected State _currentState;
    private Dictionary<Type, State> _states = new Dictionary<Type, State>();
    
    public T AddState<T>() where T : State, new()
    {
        var state = new T();
        state.Initialize(this);
        _states.Add(typeof(T), state);
        return state;
    }
    
    public void TransitionTo<T>() where T : State
    {
        _currentState?.Exit();
        _currentState = _states[typeof(T)];
        _currentState.Enter();
    }
    
    public void Update()
    {
        _currentState?.Execute();
    }
}
```

### State 基类

```csharp
/// <summary>
/// 状态基类，定义生命周期方法
/// </summary>
public abstract class State
{
    protected StateMachine _stateMachine;
    
    public void Initialize(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
    }
    
    public virtual void Enter() { }
    public virtual void Execute() { }
    public virtual void Exit() { }
}
```

## 玩家状态机层级

```
PlayerStateMachine
│
├── GroundedState（地面状态）
│   ├── IdleSubState（待机）
│   ├── MoveSubState（移动）
│   └── LockOnMoveSubState（锁定移动）
│
├── AirborneState（空中状态）
│   ├── JumpSubState（跳跃）
│   └── FallSubState（下落）
│
└── CombatLayer（战斗层，高优先级，可打断基础层）
    ├── AttackState（攻击状态）
    │   ├── Startup（前摇）
    │   ├── Active（判定生效）
    │   └── Recovery（后摇）
    ├── DeflectState（弹刀状态）
    ├── DodgeState（闪避状态）
    ├── MikiriState（识破状态）
    ├── HitState（受击状态）
    ├── StunState（架势崩溃状态）
    ├── DeathblowState（忍杀状态）
    ├── HealingState（喝药状态）
    └── LightningChargeState（接雷状态）
```

## Boss 状态机层级

```
BossStateMachine
│
├── IdleState（待机）
├── MoveState（移动/追击）
├── AttackState（攻击）
│   ├── Startup
│   ├── Active
│   └── Recovery
├── RangedState（远程射箭，仅一阶段）
├── LightningState（雷电攻击，仅二阶段）
├── StaggerState（被弹刀硬直）
├── CollapseState（架势崩溃）
├── ExecutedState（被忍杀）
└── PhaseTransitionState（阶段转换）
```

## 状态转换规则

### 玩家状态转换

| 当前状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| 任意战斗状态 | 按下右键 | DeflectState |
| 任意战斗状态 | 按下 Shift（非突刺危字） | DodgeState |
| 任意战斗状态 | 按下 Shift（突刺危字） | MikiriState |
| AttackState.Recovery | 连按左键 | AttackState.Startup |
| AttackState.* | 按右键 | DeflectState |
| 任意状态 | 受到攻击且未弹刀 | HitState |
| StunState | 硬直结束 | GroundedState.Idle |
| 任意状态 | 崩溃敌人贴近 + 左键 | DeathblowState |

### 状态优先级

```
DeathblowState > StunState > HitState > LightningChargeState > 
HealingState > DeflectState > DodgeState > MikiriState > 
AttackState > GroundedState/AirborneState
```

高优先级状态可以打断低优先级状态。

## 实现指南

1. 每个状态一个文件，放在对应模块的 `States/` 目录下
2. 状态类继承 `State` 基类，重写 `Enter`、`Execute`、`Exit`
3. 状态转换通过 `_stateMachine.TransitionTo<T>()` 调用
4. 状态内部逻辑不要超过 100 行，复杂逻辑拆分到独立的系统类

## 与 Unity Animator 的关系

HFSM 管理游戏逻辑状态，Animator 管理动画播放。两者通过参数同步：

```csharp
// 在状态中设置 Animator 参数
public override void Enter()
{
    _animator.SetBool("isAttacking", true);
}

public override void Exit()
{
    _animator.SetBool("isAttacking", false);
}
```
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/architecture/state-machine.md
git commit -m "docs: 添加 HFSM 框架设计文档"
```

---

### 任务 5：编写 data-layer.md

**文件：**
- 创建：`DOCS/architecture/data-layer.md`

- [ ] **步骤 1：编写 ScriptableObject 数据层文档**

内容如下：

```markdown
# ScriptableObject 数据层设计

## 设计意图

所有战斗参数通过 ScriptableObject 配置，实现数据驱动。禁止在代码中硬编码数值。

## 数据资产类型

### PlayerStats

玩家属性配置，每个玩家实例引用一份。

```csharp
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Combat/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("基础属性")]
    public float maxHealth = 1000f;
    public float maxPosture = 300f;
    public float postureRecoveryRate = 15f;      // 每秒恢复15%
    public float attack = 100f;
    public float defense = 50f;
    public float moveSpeed = 5f;
    public float dodgeSpeed = 10f;
    
    [Header("弹刀参数")]
    public float baseDeflectWindow = 0.2f;        // 12帧@60fps
    public float minDeflectWindow = 0.016f;       // 1帧@60fps
    public float spamResetTime = 0.5f;
    public float windowReductionPerSpam = 0.015f;
    public float deflectPostureRecovery = 0.08f;  // 弹刀恢复8%架势
    
    [Header("连续弹刀加成")]
    public float[] deflectChainMultipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };
    
    [Header("回血")]
    public int maxHealingCharges = 10;
    public float healPercent = 0.3f;
    public float healDuration = 0.8f;
}
```

### BossStats

Boss 属性配置，支持两阶段。

```csharp
[CreateAssetMenu(fileName = "BossStats", menuName = "Combat/BossStats")]
public class BossStats : ScriptableObject
{
    [Header("一阶段")]
    public float phase1MaxHealth = 1000f;
    public float phase1MaxPosture = 300f;
    public float phase1Attack = 100f;
    public AttackData[] phase1Attacks;
    
    [Header("二阶段")]
    public float phase2MaxHealth = 1200f;
    public float phase2MaxPosture = 400f;
    public float phase2Attack = 120f;
    public AttackData[] phase2Attacks;
    public float attackSpeedMultiplier = 1.2f;    // 攻速+20%
    public float startupReduction = 0.85f;        // 前摇-15%
    
    [Header("阶段转换")]
    public float phaseTransitionHealthPercent = 0.5f;
}
```

### AttackData

攻击动作数据，可复用。

```csharp
[System.Serializable]
public class AttackData
{
    public string attackName;         // 招式名称（调试用）
    public string animName;           // 动画名称
    public float damage;              // 伤害值
    public float postureDamage;       // 对敌方架势伤害
    public AttackType attackType;     // 普通/突刺/扫击/投技/雷电
    public bool canBeDeflected;       // 是否可弹刀
    
    [Header("帧数据")]
    public float startupFrames;       // 前摇帧数
    public float activeFrames;        // 判定帧数
    public float recoveryFrames;      // 后摇帧数
    public float deflectWindowFrames; // 弹刀窗口帧数
    
    [Header("判定")]
    public float hitboxRadius;        // 攻击判定范围
    public Vector3 hitboxOffset;      // 攻击判定偏移
    
    [Header("特效")]
    public string vfxName;            // 攻击特效
    public string sfxName;            // 攻击音效
    public bool isRanged;             // 是否远程
}

public enum AttackType
{
    Normal,       // 普通（可弹刀）
    Thrust,       // 突刺（需识破）
    Sweep,        // 扫击（需跳跃）
    Grab,         // 投技（需闪避）
    Lightning     // 雷电（需雷电反击）
}
```

### DeflectConfig

弹刀系统全局配置。

```csharp
[CreateAssetMenu(fileName = "DeflectConfig", menuName = "Combat/DeflectConfig")]
public class DeflectConfig : ScriptableObject
{
    [Header("玩家弹刀")]
    public float baseDeflectWindow = 0.2f;        // 12帧@60fps
    public float minDeflectWindow = 0.016f;       // 1帧@60fps
    public float spamResetTime = 0.5f;
    public float windowReductionPerSpam = 0.015f;
    
    [Header("Boss弹刀")]
    public float bossDeflectWindow = 0.15f;       // 9帧@60fps
    public float bossBlockWindow = 0.3f;
    public float bossStanceDuration = 0.4f;
    
    [Header("AI弹刀概率")]
    public float deflectChanceOnAttack = 0.4f;
    public float deflectChanceInCombo = 0.6f;
    public float deflectChanceLowHealth = 0.25f;
    public float phase2Bonus = 0.1f;
}
```

### CombatConfig

战斗系统全局参数。

```csharp
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Combat/CombatConfig")]
public class CombatConfig : ScriptableObject
{
    [Header("帧冻结")]
    public float normalHitStop = 0.033f;          // 2帧
    public float deflectHitStop = 0.05f;          // 3帧
    public float mikiriHitStop = 0.066f;          // 4帧
    public float lightningHitStop = 0.1f;         // 6帧
    
    [Header("架势")]
    public float postureRecoveryDelay = 2f;       // 脱战2秒后开始恢复
    public float postureRecoveryRate = 15f;       // 每秒15%
    
    [Header("输入缓冲")]
    public float inputBufferWindow = 0.15f;       // 150ms
}
```

## 使用规范

### 创建数据资产

1. Unity Editor → 右键 → Create → Combat → [类型]
2. 命名：`PlayerStats_Default.asset`、`BossStats_Genichiro.asset`
3. 存放在 `Assets/ScriptableObjects/` 对应子目录

### 在代码中引用

```csharp
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStats _stats;
    
    public void TakeDamage(float damage)
    {
        float actualDamage = damage - _stats.defense;
        // ...
    }
}
```

### 禁止行为

| 禁止 | 原因 |
|------|------|
| `float damage = 100f;` | 应该从 `AttackData.damage` 读取 |
| `float window = 0.2f;` | 应该从 `DeflectConfig.baseDeflectWindow` 读取 |
| 在运行时修改 ScriptableObject | 使用运行时数据结构（如 `PostureSystem`） |

## 数据资产清单

| 资产 | 路径 | 说明 |
|------|------|------|
| PlayerStats_Default | `ScriptableObjects/Player/` | 玩家默认属性 |
| BossStats_Genichiro | `ScriptableObjects/Boss/` | 弦一郎属性 |
| DeflectConfig_Default | `ScriptableObjects/Combat/` | 弹刀默认配置 |
| CombatConfig_Default | `ScriptableObjects/Combat/` | 战斗全局配置 |
| AttackData_* | `ScriptableObjects/Boss/Attacks/` | 弦一郎各招式数据 |
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/architecture/data-layer.md
git commit -m "docs: 添加 ScriptableObject 数据层设计文档"
```

---

### 任务 6：编写 deflect-system.md

**文件：**
- 创建：`DOCS/specs/deflect-system.md`

- [ ] **步骤 1：编写弹刀系统技术规格**

内容如下：

```markdown
# 弹刀系统技术规格

## 设计意图

弹刀是只狼战斗系统的核心，是攻防转换的枢纽。玩家通过精准的时机判定弹开敌人攻击，获得输出窗口。

## 核心机制

### 弹刀判定流程

```
玩家按下右键（鼠标）：

├── 轻点右键（按下后快速松开）：
│    ── 进入 DeflectState，持续约12帧@60fps（0.2秒）
│         ├─ 前0帧：立即进入判定
│         ├─ 第1-12帧：完美弹刀窗口 ← 核心
│         └─ 第13-36帧：普通格挡窗口
│
│    在完美弹刀窗口内受到攻击 → 【完美弹刀】
│    │    ├─ 播放弹刀特效（大型火花 + 屏幕震动）
│    │    ├─ 播放弹刀音效（金属"叮"声）
│    │    ├─ 帧冻结 0.03-0.05秒
│    │    ├─ 敌方架势大幅上升
│    │    ├─ 玩家恢复少量自身架势（约5-10%）
│    │    ├─ 连续弹刀加成计数 +1
│    │    └─ 抖刀惩罚计数归零
│    │
│    在普通格挡窗口内受到攻击 → 【普通格挡】
│    │    ├─ 减免约50-70%伤害
│    │    ├─ 玩家架势小幅上升
│    │    └─ 小幅击退
│    │
│    未受到攻击 → 正常退出DeflectState
│
├── 按住右键不放：
│    └── 持续格挡状态
│         ├─ 受击 → 普通格挡（同上方）
│         ├─ 连续受击 → 架势持续上升 → 最终崩溃
│         └─ 松手 → 退出格挡状态
│
└── 抖刀惩罚（连续快速按右键）：
     ├─ 判定：0.5秒内连续按右键且未成功弹刀
     ├─ 惩罚1：弹刀窗口逐次缩小
     │    第1次：12帧 → 第2次：~10帧 → ... → 最低1帧
     ├─ 惩罚2：弹刀对敌方造成的架势伤害递减
     │    第1次：100% → 后续逐渐降低 → 最低约20%
     ├─ 解除方式：
     │    ├─ 成功弹刀一次 → 惩罚重置
     │    └─ 停止按键超过0.5秒（30帧）→ 惩罚自动消除
     └─ 例外：弹开敌人多段连击时不受惩罚
```

### 连续弹刀加成

```
连续成功弹刀时，对敌方架势条的伤害递增：

  第1次弹刀 → 基础架势伤害 × 1.0
  第2次弹刀 → 基础架势伤害 × 1.2
  第3次弹刀 → 基础架势伤害 × 1.4
  第4次弹刀 → 基础架势伤害 × 1.5
  第5次+   → 封顶 × 1.5

解除：弹刀失败 / 超过1秒未弹刀 → 计数归零
```

### 弹刀窗口分档（S/L Deflect）

```
不同攻击的弹刀窗口不同：

  轻型攻击（普通挥砍）→ 弹刀窗口 12帧（标准）
  重型攻击（重劈/突刺）→ 弹刀窗口 6-7帧（更严格）
  
弹刀重型攻击成功后，对敌方造成的架势伤害更大（约1.5倍）
```

### 弹刀碰撞检测方式

```
不依赖 OnTriggerEnter（有物理刷新延迟）
→ 使用 Physics.OverlapSphere 绑定在敌人武器骨骼上
→ 每帧主动检测是否与主角的弹刀判定框交叉

实现：
  敌人武器末端挂一个小的Sphere Collider（trigger）
  主角弹刀激活时，每帧检测该Sphere是否在主角的弹刀范围内
  同时检测主角朝向与攻击方向的夹角 < 90°（正面弹刀）
```

## 数据结构

```csharp
// 依赖：DeflectConfig（见 data-layer.md）
// 依赖：PostureSystem（见 posture-system.md）
// 依赖：CombatEvents（事件广播）

public class DeflectSystem
{
    // 从 DeflectConfig 读取
    private DeflectConfig _config;
    
    // 运行时状态
    private int _spamCount = 0;
    private float _lastDeflectPressTime = 0f;
    private float _lastDeflectReleaseTime = 0f;
    private bool _isDeflecting = false;
    private float _deflectTimer = 0f;
    private int _deflectChainCount = 0;
    private float _lastDeflectSuccessTime = 0f;
}
```

## 接口定义

```csharp
/// <summary>
/// 弹刀系统，处理弹刀判定、抖刀惩罚、连续加成
/// </summary>
public class DeflectSystem
{
    public DeflectSystem(DeflectConfig config);
    
    /// <summary>
    /// 玩家按下右键时调用
    /// </summary>
    public void OnDeflectPressed();
    
    /// <summary>
    /// 玩家松开右键时调用
    /// </summary>
    public void OnDeflectReleased();
    
    /// <summary>
    /// 每帧调用，更新弹刀状态
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 检测是否受到攻击，返回弹刀结果
    /// </summary>
    public DeflectResult TryDeflect(AttackData incomingAttack, Vector3 attackDirection, Vector3 playerForward);
    
    /// <summary>
    /// 成功弹刀后调用，重置惩罚计数
    /// </summary>
    public void OnSuccessfulDeflect();
    
    /// <summary>
    /// 获取当前弹刀窗口（考虑抖刀惩罚）
    /// </summary>
    public float GetCurrentDeflectWindow();
    
    /// <summary>
    /// 获取当前架势伤害倍率（考虑连续加成和抖刀惩罚）
    /// </summary>
    public float GetPostureDamageMultiplier();
    
    /// <summary>
    /// 是否处于弹刀状态
    /// </summary>
    public bool IsDeflecting { get; }
    
    /// <summary>
    /// 是否处于格挡状态（按住右键）
    /// </summary>
    public bool IsBlocking { get; }
}

public enum DeflectResult
{
    None,           // 未弹刀
    NormalBlock,    // 普通格挡
    PerfectDeflect  // 完美弹刀
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| baseDeflectWindow | 0.2 | 秒 | 12帧@60fps |
| minDeflectWindow | 0.016 | 秒 | 1帧@60fps |
| spamResetTime | 0.5 | 秒 | 停止按键多久后重置惩罚 |
| windowReductionPerSpam | 0.015 | 秒 | 每次抖刀减少的窗口 |
| deflectChainResetTime | 1.0 | 秒 | 多久未弹刀则加成归零 |
| deflectPostureRecovery | 0.08 | 比例 | 弹刀恢复自身架势8% |
| blockDamageReduction | 0.6 | 比例 | 格挡减伤60% |
| blockPostureIncrease | 0.3 | 比例 | 格挡时架势上升30% |

## 与其他系统的交互

### 输入

- `InputReader` → 右键按下/松开/长按事件
- `PostureSystem` → 敌方攻击数据（AttackData）
- `Boss` → 攻击方向和位置

### 输出

- `PostureSystem` → 增加敌方架势、恢复自身架势
- `DamageCalculator` → 传递伤害倍率
- `HitStopManager` → 触发帧冻结
- `CombatEvents` → 广播 `OnPerfectDeflect` / `OnNormalBlock`
- `VFXManager` → 播放弹刀特效
- `AudioManager` → 播放弹刀音效

## 测试要点

### EditMode 单元测试

- [ ] `GetCurrentDeflectWindow_BaseValue_Returns12Frames` — 初始弹刀窗口正确
- [ ] `GetCurrentDeflectWindow_After3Spams_WindowReduced` — 抖刀惩罚使窗口递减
- [ ] `GetCurrentDeflectWindow_AtMinSpam_Returns1Frame` — 窗口不低于最小值
- [ ] `OnSuccessfulDeflect_SpamPenaltyReset` — 成功弹刀重置惩罚
- [ ] `OnDeflectReleased_Wait05Seconds_SpamPenaltyAutoReset` — 超时自动重置
- [ ] `GetPostureDamageMultiplier_Chain2_Returns12` — 连续弹刀加成正确
- [ ] `GetPostureDamageMultiplier_Chain5Plus_Caps15` — 加成封顶
- [ ] `GetPostureDamageMultiplier_Spam3_Reduced` — 抖刀惩罚降低伤害
- [ ] `TryDeflect_InPerfectWindow_ReturnsPerfectDeflect` — 完美弹刀判定
- [ ] `TryDeflect_InBlockWindow_ReturnsNormalBlock` — 普通格挡判定
- [ ] `TryDeflect_OutsideWindow_ReturnsNone` — 未弹刀
- [ ] `TryDeflect_WrongAngle_ReturnsNone` — 角度不对不弹刀

## 验收标准

1. 轻点右键能进入弹刀状态，持续约 0.2 秒
2. 在弹刀窗口内受击触发完美弹刀，播放特效音效，帧冻结
3. 普通格挡减伤并减少架势上升
4. 连续快速按右键触发抖刀惩罚，弹刀窗口递减
5. 成功弹刀重置惩罚，停止按键 0.5 秒后自动重置
6. 连续弹刀时敌方架势伤害递增，封顶 1.5 倍
7. 弹刀成功恢复自身 8% 架势
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/specs/deflect-system.md
git commit -m "docs: 添加弹刀系统技术规格"
```

---

### 任务 7：编写 posture-system.md

**文件：**
- 创建：`DOCS/specs/posture-system.md`

- [ ] **步骤 1：编写架势条系统技术规格**

内容如下：

```markdown
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
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/specs/posture-system.md
git commit -m "docs: 添加架势条系统技术规格"
```

---

### 任务 8：编写其余 spec 文档

由于剩余 spec 文档（boss-ai、danger-system、lightning-system、input-system、animation-system、ui-hud）内容较多，此处省略完整代码。每个文档应遵循统一结构，从现有策划案 `DOCS/ARPG战斗Demo_项目策划案_只狼.md` 中提取对应章节内容，按照 `specs/` 统一格式组织。

- [ ] **步骤 1：编写 boss-ai.md**

从策划案第五、十一章提取内容，包含：
- Boss 状态机层级
- 两阶段招式表（伤害、破韧、前摇、判定、后摇、可弹刀、危字类型）
- AI 行为权重表
- 弹刀 AI 逻辑（概率判定、姿态进入）
- 阶段转换逻辑

- [ ] **步骤 2：编写 danger-system.md**

从策划案第七章提取内容，包含：
- 三种危的判定条件（下段、突刺、擒拿）
- 识破触发逻辑（距离、角度、时机）
- 跳跃踩头逻辑

- [ ] **步骤 3：编写 lightning-system.md**

从策划案第八章提取内容，包含：
- 雷电反击状态机（None→Falling→Charged→Reflected/Failed）
- 各阶段输入窗口
- 失败判定

- [ ] **步骤 4：编写 input-system.md**

从策划案第二、九、十二章提取内容，包含：
- 操作优先级表
- 输入缓冲队列（150ms 窗口）
- 状态打断规则
- 回血系统（药葫芦）
- 帧冻结参数

- [ ] **步骤 5：编写 animation-system.md**

从策划案第十三章提取内容，包含：
- Root Motion 策略表
- Animation Event 清单
- Blend Tree 设计

- [ ] **步骤 6：编写 ui-hud.md**

从策划案第十四章提取内容，包含：
- HUD 布局图
- UI 元素清单
- 事件监听关系

- [ ] **步骤 7：Commit 所有 spec 文档**

```bash
git add DOCS/specs/
git commit -m "docs: 添加全部系统技术规格（8份）"
```

---

### 任务 9：编写 test-plan.md

**文件：**
- 创建：`DOCS/testing/test-plan.md`

- [ ] **步骤 1：编写测试计划文档**

内容如下：

```markdown
# 测试计划

## 测试框架

- Unity Test Framework（EditMode + PlayMode）
- 运行方式：Unity Editor → Window → General → Test Runner

## 测试分层

### EditMode 单元测试

不需要运行游戏，用于测试纯逻辑：数值计算、状态转换条件、配置读取。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| DeflectSystem | DeflectSystemTest.cs | 弹刀窗口计算、抖刀惩罚递减、成功弹刀重置、连续加成链 |
| PostureSystem | PostureSystemTest.cs | 架势增加、恢复速率、崩溃判定、脱战恢复计时器 |
| DamageCalculator | DamageCalculatorTest.cs | 伤害计算、格挡减伤、弹刀伤害加成 |
| InputBuffer | InputBufferTest.cs | 缓冲窗口、超时清空、优先级排序 |
| LightningCounterSystem | LightningSystemTest.cs | 状态流转（None→Falling→Charged→Reflected/Failed）、输入超时 |

### PlayMode 集成测试

需要运行游戏，用于测试 Unity 组件交互：状态机转换、动画事件触发、物理检测。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| PlayerStateMachine | PlayerStateMachineTest.cs | 状态转换（Idle→Attack→Deflect→Hit）、优先级打断 |
| BossStateMachine | BossStateMachineTest.cs | AI 决策（距离判断、招式选择）、阶段转换 |
| CombatIntegration | CombatIntegrationTest.cs | 弹刀触发伤害计算+架势变化+事件广播的完整链路 |

## 测试编写规范

### 命名规则

测试方法命名：`[被测方法]_[输入条件]_[期望结果]`

例：
- `GetCurrentDeflectWindow_After3Spams_WindowReduced`
- `AddPosture_ExceedsMax_TriggersBreak`

### 测试结构

```csharp
[Test]
public void MethodName_Condition_ExpectedResult()
{
    // Arrange - 准备测试数据和依赖
    var system = CreateTestSystem();
    
    // Act - 执行被测操作
    var result = system.MethodUnderTest(input);
    
    // Assert - 验证结果
    Assert.AreEqual(expected, result);
}
```

### 禁止事项

- 避免在测试中使用 `Time.deltaTime`，用固定值模拟
- 避免在测试中加载真实资源，使用 Mock 或 ScriptableObject.CreateInstance
- 每个测试只验证一件事

### 测试辅助方法

```csharp
// 在测试基类或工具类中提供
protected static DeflectSystem CreateTestDeflectSystem()
{
    var config = ScriptableObject.CreateInstance<DeflectConfig>();
    config.baseDeflectWindow = 0.2f;
    // ... 设置默认值
    return new DeflectSystem(config);
}

protected static AttackData CreateTestAttackData(float damage = 100f, float postureDamage = 15f)
{
    return new AttackData
    {
        damage = damage,
        postureDamage = postureDamage,
        attackType = AttackType.Normal,
        canBeDeflected = true,
        // ... 其他默认值
    };
}
```

## 覆盖率目标

| 模块 | 目标覆盖率 | 说明 |
|------|-----------|------|
| DeflectSystem | 90%+ | 核心战斗逻辑，必须高覆盖 |
| PostureSystem | 90%+ | 核心战斗逻辑，必须高覆盖 |
| DamageCalculator | 90%+ | 核心战斗逻辑，必须高覆盖 |
| InputBuffer | 80%+ | 重要但逻辑相对简单 |
| LightningSystem | 80%+ | 状态流转需覆盖 |
| BossStateMachine | 70%+ | AI 逻辑复杂，覆盖关键路径 |
| UI/Camera | 手工验证 | 不要求自动化测试 |

## 测试执行时机

| 时机 | 执行内容 |
|------|---------|
| 编写新功能时 | 先写测试，再写实现，实时运行 EditMode 测试 |
| 修改已有代码前 | 先运行现有测试，确保不回归 |
| 提交前 | 运行全部 EditMode 测试 |
| 重要里程碑 | 运行全部测试（EditMode + PlayMode） |

## 测试文件组织

```
Assets/Tests/
├── EditMode/
│   ├── DeflectSystemTest.cs
│   ├── PostureSystemTest.cs
│   ├── DamageCalculatorTest.cs
│   ├── InputBufferTest.cs
│   └── LightningSystemTest.cs
├── PlayMode/
│   ├── PlayerStateMachineTest.cs
│   ├── BossStateMachineTest.cs
│   └── CombatIntegrationTest.cs
└── TestUtilities/
    ├── TestHelpers.cs          ← 测试辅助方法
    └── MockObjects.cs          ← Mock 对象
```
```

- [ ] **步骤 2：Commit**

```bash
git add DOCS/testing/test-plan.md
git commit -m "docs: 添加测试计划"
```

---

### 任务 10：最终验证和 Commit

- [ ] **步骤 1：检查所有文件是否已创建**

```bash
find DOCS/ -name "*.md" | sort
```

预期输出：

```
DOCS/ARPG战斗Demo_项目策划案_只狼.md
DOCS/architecture/code-structure.md
DOCS/architecture/data-layer.md
DOCS/architecture/state-machine.md
DOCS/specs/animation-system.md
DOCS/specs/boss-ai.md
DOCS/specs/danger-system.md
DOCS/specs/deflect-system.md
DOCS/specs/input-system.md
DOCS/specs/lightning-system.md
DOCS/specs/posture-system.md
DOCS/specs/ui-hud.md
DOCS/testing/test-plan.md
```

- [ ] **步骤 2：检查 CLAUDE.md 是否包含 Harness 章节**

```bash
grep -A 5 "Harness 工程配置" CLAUDE.md
```

预期输出：包含架构约束、代码规范、测试要求、文档加载指引、工作流等内容。

- [ ] **步骤 3：检查测试目录是否创建**

```bash
ls -la Assets/Tests/EditMode/ Assets/Tests/PlayMode/
```

预期输出：两个目录都存在，包含 `.gitkeep` 文件。

- [ ] **步骤 4：最终 Commit**

```bash
git status
git add .
git commit -m "docs: 完成 Harness 文档体系搭建

- CLAUDE.md 增强（架构约束+工作流+文档加载指引）
- 8 份系统技术规格（specs/）
- 3 份架构文档（architecture/）
- 测试计划（testing/）
- 测试目录结构（Assets/Tests/）"
```

- [ ] **步骤 5：推送到 GitHub**

```bash
git push origin master
```

---

## 自检

### 1. 规格覆盖度

| 规格章节 | 对应任务 |
|---------|---------|
| CLAUDE.md 设计 | 任务 2 |
| specs/ 统一结构 | 任务 6-8 |
| architecture/ 文档 | 任务 3-5 |
| testing/ 测试计划 | 任务 9 |
| 实施优先级 | 任务 1（目录）→ 任务 2-9（文档）→ 任务 10（验证） |

所有规格需求都有对应任务。

### 2. 占位符扫描

任务 8 中的 6 份 spec 文档（boss-ai、danger-system 等）使用了简化描述，因为完整内容与策划案重复且篇幅过长。这是合理的——这些文档应从策划案提取内容，按照统一格式组织，不需要在计划中重复展示完整代码。

### 3. 类型一致性

所有任务中引用的类型和接口保持一致：
- `DeflectSystem`、`PostureSystem`、`DamageCalculator` 等类名
- `DeflectConfig`、`PlayerStats`、`BossStats` 等数据类
- `CombatEvents` 事件系统
- 测试文件命名规范统一

---

**计划已完成并保存到 `docs/superpowers/plans/2026-07-01-harness-documentation.md`。**

两种执行方式：

**1. 子代理驱动（推荐）** — 每个任务调度一个新的子代理，任务间进行审查，快速迭代

**2. 内联执行** — 在当前会话中使用 executing-plans 执行任务，批量执行并设有检查点

选哪种方式？
