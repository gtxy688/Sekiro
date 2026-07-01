# Harness 文档体系设计规格

> 项目：只狼 ARPG 战斗 Demo
> 日期：2026-07-01
> 目标：为 AI Agent 深度协作构建完整的 Harness 工程环境

---

## 1. 背景与目标

本项目采用 Harness Engineering 方法论，让 AI Agent（如 Claude）能够自主完成功能开发并自我验证。

**核心公式：** Agent = Model + Harness

Harness = 文档体系 + 代码结构 + 测试框架 + 工作流约束。本文档定义 Harness 的文档体系部分。

**目标：**
- Agent 每次只加载 1-2 份相关文档，上下文高效，不浪费在无关内容上
- Agent 写完代码后，通过测试自我验证，无需人工检查
- 新系统加入时只需新增一份 spec，架构可扩展

---

## 2. 文件结构总览

```
项目根目录/
├── CLAUDE.md                              ← Agent 行为手册（增强版）
├── DOCS/
│   ├── ARPG战斗Demo_项目策划案_只狼.md      ← 保留（总体参考，不删除）
│   ├── specs/                             ← 8 份系统技术规格
│   │   ├── deflect-system.md              ← 弹刀系统
│   │   ├── posture-system.md              ← 架势条系统
│   │   ├── boss-ai.md                     ← Boss AI + 招式表
│   │   ├── danger-system.md               ← 危字 + 识破
│   │   ├── lightning-system.md            ← 雷电反击
│   │   ├── input-system.md                ← 输入缓冲 + 操作优先级
│   │   ├── animation-system.md            ← 动画事件 + Root Motion
│   │   └── ui-hud.md                      ← UI 布局 + 元素清单
│   ├── architecture/                      ← 3 份架构文档
│   │   ├── code-structure.md              ← 代码目录结构 + 模块边界
│   │   ├── state-machine.md               ← HFSM 框架设计
│   │   └── data-layer.md                  ← ScriptableObject 数据层
│   └── testing/
│       └── test-plan.md                   ← 测试计划
├── SPEC/                                  ← 保留（快速恢复索引）
│   ├── ARPG战斗Demo_项目备忘.md
│   └── ARPG战斗Demo_项目策划案_只狼.md
└── Assets/Tests/                          ← 测试代码目录
    ├── EditMode/
    └── PlayMode/
```

总计新增 13 份文档 + 1 个目录结构。

---

## 3. CLAUDE.md 设计

CLAUDE.md 是整个 Harness 的入口，每次 AI 会话启动时自动加载。当前项目已有 superpowers-zh 技能框架内容，Harness 内容是**在现有内容基础上新增**的章节，不替换已有内容。

### 3.1 结构

```markdown
# CLAUDE.md

## 项目概述
- 项目名称、目标、引擎版本、开发周期
- 当前阶段（第几周）和当前任务
- 指向详细规格文档的索引表

## 架构约束（不可违反）
- 所有战斗参数必须用 ScriptableObject，禁止硬编码数值
- 状态机使用 HFSM 模式（继承 StateMachineBehaviour 或自定义基类）
- 模块间通信走 CombatEvents 事件系统，禁止直接引用
- 弹刀判定用 Physics.OverlapSphere 每帧检测，不用 OnTriggerEnter

## 代码规范
- 命名规则：类名 PascalCase，方法名 PascalCase，私有字段 _camelCase
- 每个公开方法必须有 XML 文档注释
- 单个脚本文件不超过 300 行
- 一次提交只做一件事（原子性）

## 测试要求
- 新增数值逻辑 → 必须配套 EditMode 单元测试
- 新增状态转换 → 必须配套 PlayMode 集成测试
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 测试运行方式：Window > General > Test Runner

## 文档加载指引
| 任务类型 | 必读文档 | 可选文档 |
|---------|---------|---------|
| 弹刀系统 | specs/deflect-system.md | architecture/code-structure.md |
| 架势条 | specs/posture-system.md | architecture/data-layer.md |
| Boss AI | specs/boss-ai.md | architecture/state-machine.md |
| 危字/识破 | specs/danger-system.md | specs/animation-system.md |
| 雷电反击 | specs/lightning-system.md | architecture/state-machine.md |
| 输入系统 | specs/input-system.md | architecture/code-structure.md |
| 动画集成 | specs/animation-system.md | — |
| UI/HUD | specs/ui-hud.md | — |
| HFSM 框架 | architecture/state-machine.md | — |
| 数据层 | architecture/data-layer.md | — |

## 工作流（TDD 强制）
1. 读取相关规格文档
2. 编写测试（定义"完成"的标准）
3. 编写实现代码
4. 运行测试，全部通过后提交
5. 运行验证（游戏能否启动，有无报错）
```

### 3.2 设计决策

- **为什么不在 CLAUDE.md 里写技术细节？** — CLAUDE.md 是"行为手册"，不是"技术规格"。技术细节放在 `specs/` 里，按需加载。这样避免每次会话都加载几千行无关内容。
- **为什么强制 TDD？** — 对于 AI 代理，测试是"自我验证"的唯一手段。没有测试，Agent 写完代码无法确认是否正确。

---

## 4. specs/ — 系统技术规格文档

每个核心子系统一份独立文档，Agent 按需加载。

### 4.1 文档清单

| 文件 | 对应系统 | 核心内容 |
|------|---------|---------|
| `deflect-system.md` | 弹刀系统 | 判定窗口、抖刀惩罚公式、连续加成链、碰撞检测方式 |
| `posture-system.md` | 架势条系统 | 架势变化规则、恢复逻辑、崩溃判定、数值表 |
| `boss-ai.md` | Boss AI | 状态机、两阶段招式表、行为权重、弹刀 AI、阶段转换 |
| `danger-system.md` | 危字 + 识破 | 三种危的判定条件、识破触发逻辑、方向检测 |
| `lightning-system.md` | 雷电反击 | 多阶段状态机（Falling→Charged→Reflected/Failed）、输入窗口 |
| `input-system.md` | 输入系统 | 操作优先级、输入缓冲队列、状态打断规则 |
| `animation-system.md` | 动画系统 | Root Motion 策略、Animation Event 清单、Blend Tree 设计 |
| `ui-hud.md` | UI/HUD | HUD 布局、元素清单、事件监听关系 |

### 4.2 统一文档结构

每份 spec 文档遵循以下统一结构：

```markdown
# [系统名称] 技术规格

## 设计意图（1-2 句话）
> 为什么需要这个系统？它在战斗中扮演什么角色？

## 核心机制
（该系统的运作逻辑，用流程图或伪代码描述）

## 数据结构
（涉及的 ScriptableObject / 枚举 / 配置类定义）

## 接口定义
（公开方法签名 + 参数说明，不需要实现细节）

## 数值参数表
（所有可调数值，附带建议范围和单位）

## 与其他系统的交互
（该系统的输入来自哪里，输出影响哪些系统）

## 测试要点
（必须覆盖的测试场景列表）

## 验收标准
（怎样算"完成"，可观察的行为描述）
```

### 4.3 设计决策

- **为什么包含"接口定义"？** — Agent 在实现一个系统时，需要知道其他系统的接口（如 PostureSystem 的 `AddPosture()`）。把接口集中写在各自的 spec 里，比让 Agent 去翻代码更高效。
- **为什么不包含完整代码实现？** — 代码示例在策划案里已经有了，但那是设计参考。Agent 应该根据接口定义和测试要点自行实现，确保代码是为这个项目写的，而不是从文档复制的。
- **数值参数来源** — 从现有策划案 `ARPG战斗Demo_项目策划案_只狼.md` 中提取，保持一致。

---

## 5. architecture/ — 架构文档

定义代码的物理结构和框架设计，是 Agent 写代码时的"地图"。

### 5.1 code-structure.md

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
├── ScriptableObjects/             ← 数据资产
│   ├── Player/
│   └── Boss/
│
├── Tests/
│   ├── EditMode/                  ← 单元测试
│   └── PlayMode/                  ← 集成测试
│
└── Resources/                     ← 美术资源（已有）
```

**模块边界规则：**
- `Core/` 被所有模块依赖，但自身不依赖 Player/Boss/Combat
- `Combat/` 被 Player 和 Boss 依赖，但自身不依赖它们
- `Player/` 和 `Boss/` 互相不能直接引用，只能通过 CombatEvents 通信

### 5.2 state-machine.md

定义 HFSM 框架的设计：

```
核心抽象：
├── StateMachine          ← 状态机容器，管理当前状态和状态转换
├── State                 ← 状态基类，定义 Enter/Execute/Exit 生命周期
├── PlayerStateMachine    ← 玩家状态机（继承 StateMachine）
└── BossStateMachine      ← Boss 状态机（继承 StateMachine）

状态层级：
├── GroundedState（地面）
│   ├── IdleSubState
│   └── MoveSubState
├── AirborneState（空中）
│   ├── JumpSubState
│   └── FallSubState
└── CombatLayer（战斗层，高优先级）
    ├── AttackState
    ├── DeflectState
    ├── DodgeState
    └── ...
```

### 5.3 data-layer.md

定义 ScriptableObject 数据层：

```
数据资产类型：
├── PlayerStats          ← 玩家属性（血量、架势、攻速、弹刀参数...）
├── BossStats            ← Boss 属性（两阶段分别配置）
├── AttackData           ← 攻击动作数据（伤害、破韧、前摇帧数...）
├── DeflectConfig        ← 弹刀系统全局配置
└── CombatConfig         ← 战斗系统全局参数

设计原则：
- 所有数值参数放在 ScriptableObject 里，代码中不出现魔法数字
- 运行时通过引用读取，编辑器中可直接调整
- 每个系统有自己的 Config 类，不共用一个巨型 Config
```

---

## 6. testing/ — 测试计划

测试是 Harness Engineering 的核心——它是 AI Agent 的"自动评分器"。

### 6.1 测试框架

- Unity Test Framework（EditMode + PlayMode）
- 运行方式：Window > General > Test Runner

### 6.2 EditMode 单元测试

不需要运行游戏，用于测试纯逻辑：数值计算、状态转换条件、配置读取。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| DeflectSystem | DeflectSystemTest.cs | 弹刀窗口计算、抖刀惩罚递减、成功弹刀重置、连续加成链 |
| PostureSystem | PostureSystemTest.cs | 架势增加、恢复速率、崩溃判定、脱战恢复计时器 |
| DamageCalculator | DamageCalculatorTest.cs | 伤害计算、格挡减伤、弹刀伤害加成 |
| InputBuffer | InputBufferTest.cs | 缓冲窗口、超时清空、优先级排序 |
| LightningCounterSystem | LightningSystemTest.cs | 状态流转（None→Falling→Charged→Reflected/Failed）、输入超时 |

### 6.3 PlayMode 集成测试

需要运行游戏，用于测试 Unity 组件交互：状态机转换、动画事件触发、物理检测。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| PlayerStateMachine | PlayerStateMachineTest.cs | 状态转换（Idle→Attack→Deflect→Hit）、优先级打断 |
| BossStateMachine | BossStateMachineTest.cs | AI 决策（距离判断、招式选择）、阶段转换 |
| CombatIntegration | CombatIntegrationTest.cs | 弹刀触发伤害计算+架势变化+事件广播的完整链路 |

### 6.4 测试编写规范

- 测试方法命名：`[被测方法]_[输入条件]_[期望结果]`
  - 例：`GetCurrentDeflectWindow_After3Spams_WindowReduced`
- 每个测试只验证一件事
- 使用 `Assert.AreEqual` / `Assert.IsTrue` / `Assert.Throws`
- 避免在测试中使用 `Time.deltaTime`，用固定值模拟

### 6.5 覆盖率目标

- 核心战斗逻辑（DeflectSystem、PostureSystem、DamageCalculator）：90%+
- AI 逻辑（BossStateMachine）：70%+
- UI/相机：不要求测试，手工验证即可

### 6.6 设计决策

- **为什么分 EditMode 和 PlayMode？** — EditMode 测试快（秒级），适合频繁运行；PlayMode 测试慢（需要启动游戏），只在关键集成点使用。Agent 开发时主要跑 EditMode 测试，提交前跑一次 PlayMode 测试。
- **为什么不要求 100% 覆盖率？** — UI 和相机系统的测试投入产出比低，手工验证更高效。精力集中在核心战斗逻辑上。

---

## 7. Agent 工作流

### 7.1 TDD 强制流程

```
Agent 接到任务（如"实现弹刀系统"）
    │
    ▼
① 读取 CLAUDE.md 中的"文档加载指引"表
    │  找到：弹刀系统 → specs/deflect-system.md
    ▼
② 加载对应的技术规格文档
    │  了解：设计意图、接口定义、数值参数、测试要点
    │  同时加载：architecture/code-structure.md（知道代码放哪）
    │  同时加载：architecture/state-machine.md（如果涉及状态机）
    ▼
③ 编写测试（定义"完成"标准）
    │  根据 specs 中的"测试要点"章节
    │  写在 Tests/EditMode/ 或 Tests/PlayMode/
    ▼
④ 编写实现代码
    │  遵循 specs 中的"接口定义"
    │  遵循 CLAUDE.md 中的代码规范
    ▼
⑤ 运行测试
    ├── 全部通过 → 继续
    └── 有失败 → 修正代码 → 重新运行（最多3轮）
    ▼
⑥ 验证游戏可运行
    │  无编译错误，无运行时异常
    ▼
⑦ 提交（原子性 commit，信息遵循 git 规范）
```

### 7.2 文档加载指引表（写入 CLAUDE.md）

| 任务类型 | 必读文档 | 可选文档 |
|---------|---------|---------|
| 弹刀系统 | specs/deflect-system.md | architecture/code-structure.md |
| 架势条 | specs/posture-system.md | architecture/data-layer.md |
| Boss AI | specs/boss-ai.md | architecture/state-machine.md |
| 危字/识破 | specs/danger-system.md | specs/animation-system.md |
| 雷电反击 | specs/lightning-system.md | architecture/state-machine.md |
| 输入系统 | specs/input-system.md | architecture/code-structure.md |
| 动画集成 | specs/animation-system.md | — |
| UI/HUD | specs/ui-hud.md | — |
| HFSM 框架 | architecture/state-machine.md | — |
| 数据层 | architecture/data-layer.md | — |

### 7.3 现有策划案的处理

| 文件 | 处理方式 |
|------|---------|
| `DOCS/ARPG战斗Demo_项目策划案_只狼.md` | 保留，作为总体参考（不删除） |
| `SPEC/ARPG战斗Demo_项目备忘.md` | 保留，作为会话恢复时的快速索引 |
| `SPEC/ARPG战斗Demo_项目策划案_只狼.md` | 如果存在则保留 |

---

## 8. 现有策划案内容的拆分映射

以下说明现有策划案各章节如何映射到新的 spec 文档：

| 策划案章节 | 目标 spec 文档 |
|-----------|---------------|
| 四、弹刀系统 | specs/deflect-system.md |
| 五、Boss 弹刀系统 | specs/boss-ai.md（合并到 Boss AI 中） |
| 六、架势条系统 | specs/posture-system.md |
| 七、危字与识破系统 | specs/danger-system.md |
| 八、雷电反击系统 | specs/lightning-system.md |
| 九、回血系统 | specs/input-system.md（作为操作之一） |
| 十、主角状态机 | architecture/state-machine.md |
| 十一、弦一郎 Boss 设计 | specs/boss-ai.md |
| 十二、打击感与视觉反馈 | specs/input-system.md（帧冻结属于输入反馈） |
| 十三、动画系统设计 | specs/animation-system.md |
| 十四、UI/HUD 设计 | specs/ui-hud.md |
| 十五、数据结构设计 | architecture/data-layer.md |

---

## 9. 实施优先级

鉴于项目处于第 1 周（基础框架阶段），建议按以下顺序搭建 Harness：

1. **立即** — 增强 CLAUDE.md，加入架构约束和工作流
2. **本周** — 创建 `DOCS/specs/` 和 `DOCS/architecture/` 目录结构
3. **本周** — 编写 `architecture/code-structure.md`（指导后续代码放置）
4. **第 2 周** — 编写 `specs/deflect-system.md` 和 `specs/posture-system.md`（弹刀+架势是核心）
5. **第 2 周** — 搭建 `Assets/Tests/` 目录，为 DeflectSystem 写第一批单元测试
6. **第 3 周** — 编写其余 spec 文档（danger-system、lightning-system、boss-ai）
7. **第 3 周** — 编写 `testing/test-plan.md`
8. **持续** — 每次新增系统，先写测试，再让 Agent 实现
