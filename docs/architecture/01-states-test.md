# 01 状态机核心 - 验收清单

> 配合 `01-states.md` 使用。AI 实现完后，你在 Unity 里逐条操作验证。

## 前置准备

1. 场景里放一个角色 GameObject，挂 `CharacterBody` + `Animator` + `Rigidbody`。
2. CharacterBody 的 Inspector 配好：`groundCheckPoint`（角色的脚底空物体）、`groundCheckRadius`、`groundLayer`（地面层）。
3. 需要一个 Ground 模型/胶囊体放在角色脚下作为地面。
4. Animator 里要有这些动画状态：Idle、Run、Hurt_Ground、Hurt_Air。

## M1：Hit 路由

| # | 操作 | 预期 |
|---|------|------|
| 1 | 打开 Console，无编译错误 | 0 个 Error |
| 2 | 代码里 `MainStateMachine.CurrentState.OnHitReceived(hit)` 能编译通过 | 无报错 |
| 3 | 构造一个 HitData，手动调 `body.ReceiveHit(...)` | 能进 StunnedState（观察动画切到 Hurt_Ground） |

## M4：受击/弹反/闪避

| # | 操作 | 预期 |
|---|------|------|
| 4 | 玩家在地面（Idle 状态），手动调 `ReceiveHit` | 切到 StunnedState，播 Hurt_Ground，0.5s 后回 Idle |
| 5 | 玩家在空中（跳起来）调 `ReceiveHit` | 切到 StunnedState，播 Hurt_Air，落地后回 Grounded，没落地继续下落 |
| 6 | 玩家在弹反窗口内调 `ReceiveHit` | 触发弹反逻辑（涨对方架势 + 打铁音效事件），不进 StunnedState |
| 7 | 玩家在闪避无敌帧内调 `ReceiveHit` | 伤害被吞，不进 StunnedState |
| 8 | 玩家已经在 StunnedState 里再调 `ReceiveHit` | 二次受击被拦截，硬直不刷新 |

## M10：处决

| # | 操作 | 预期 |
|---|------|------|
| 9 | Boss 架势崩解（手动把 Posture 设满） | 进 EndureState（崩解硬直），身上锁定点变红（M13 联动） |
| 10 | 玩家走进攻击范围按交互键 | 触发忍杀动画 |
| 11 | 忍杀动画命中帧 | 调用 ExecuteFinisher，Boss 扣一条命，架势归零 |
| 12 | Boss 两条命都打掉 | 游戏结束 |

## 常见问题

- **进不了 StunnedState**：检查 `ReceiveHit` 是否被调用（Hitbox 那边 A5/M3 还没接，先用脚本手动调）。
- **二次受击刷新硬直**：StunnedState.OnParentHandleHit 要返回 true。
- **闪避免疫不生效**：确认 DodgeState.OnHitReceived 在无敌帧内返回 true。
