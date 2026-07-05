# HFSM 框架

## 基类

`StateMachine` 管理当前状态和转换，`State` 定义生命周期方法。

| 方法 | 用途 |
|------|------|
| `AddState<T>() where T : State, new()` | 注册状态，自动 Initialize |
| `TransitionTo<T>() where T : State` | Exit 当前 → 切换 → Enter 目标 |
| `Update()` | 每帧 `_currentState?.Execute()` |

State 方法：`Initialize(StateMachine)` → `virtual Enter()` → `virtual Execute()` → `virtual Exit()`

## 玩家状态层级

```
PlayerStateMachine
├── GroundedState (Idle / Move / LockOnMove)
├── AirborneState (Jump / Fall)
└── CombatLayer (Attack[Startup→Active→Recovery] / Deflect / Dodge / Mikiri / Hit / Stun / Deathblow / Heal / LightningCharge)
```

优先级：Deathblow > Stun > Hit > LightningCharge > Heal > Deflect > Dodge > Mikiri > Attack > Grounded/Airborne

## Boss 状态层级

```
BossStateMachine
Idle → Move → Attack (Startup→Active→Recovery) / Ranged → Stagger → Collapse → Executed
```

## 转换规则

| 当前 | 触发 | 目标 |
|------|------|------|
| 任意 | 右键 | DeflectState |
| 任意 | Shift (非突刺危) | DodgeState |
| 任意 | Shift (突刺危) | MikiriState |
| Attack.Recovery | 连按左键 | Attack.Startup (连招) |
| 任意 | 受击未弹刀 | HitState |
| StunState | 硬直结束 | Idle |
| 任意 | 崩溃敌人+左键 | DeathblowState |

## 实现指南

- 每个状态一个文件，放在对应 `States/` 目录
- 状态内部 < 100 行，复杂逻辑拆分到独立系统类
- `Animator.SetBool()` 在 Enter/Exit 中同步动画参数
