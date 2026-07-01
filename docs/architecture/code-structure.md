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
