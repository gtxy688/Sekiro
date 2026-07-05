# 代码目录结构

```
Assets/Scripts/
├── Core/              ← 框架层（Agent 不应修改）
│   ├── StateMachine/  ← HFSM 基类
│   ├── Events/        ← CombatEvents 事件总线
│   └── Input/         ← IInputProvider 抽象层
├── Player/            ← 玩家模块
│   ├── States/        ← 玩家状态
│   ├── Combat/        ← PlayerCombatController
│   └── Movement/      ← PlayerController
├── Boss/              ← Boss 模块
│   ├── AI/            ← BossAIController
│   ├── States/        ← Boss 状态
│   └── Attacks/       ← 招式实现
├── Combat/            ← 共享战斗系统（Player/Boss 共用）
│   ├── DeflectSystem.cs / PostureSystem.cs / DamageCalculator.cs
│   └── DangerSystem.cs / HitStopManager.cs
├── UI/ / Camera/ / Audio/
├── ScriptableObjects/ ← 数据资产（Player/ Boss/ Combat）
└── Tests/EditMode/ + Tests/PlayMode/
```

## 依赖方向

Core/ ←所有模块依赖，自身不依赖业务 ← Combat/ ← Player/ 和 Boss/ 依赖，不依赖它们
Player/ ←依赖 Combat/ + Core/，**禁止**引用 Boss/
Boss/ ←依赖 Combat/ + Core/，**禁止**引用 Player/

## 模块职责

| 模块 | 职责 |
|------|------|
| Core/StateMachine | HFSM 基类 + 状态转换 |
| Core/Events | 事件总线，模块间解耦 |
| Core/Input | 输入抽象 |
| Combat/Deflect | 弹刀判定/抖刀惩罚/加成 |
| Combat/Posture | 架势管理/恢复/崩溃 |
| Combat/Damage | 伤害计算/减伤 |
| Player/States | 玩家各状态实现 |
| Boss/AI | Boss 决策/招式选择 |

## 新增文件

1. 判断所属模块 → 2. 检查依赖边界 → 3. 选择子目录 → 4. PascalCase 命名
