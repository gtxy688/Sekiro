# 03 命中判定 - 验收清单

> 配合 `03-hit-detection.md` 使用。

## 前置准备

1. 角色模型上有武器骨骼（空物体挂在手部），挂 `Hitbox` 组件。
2. 角色身体挂 `Hurtbox` 组件。
3. 场景里放一个 `CombatManager`（单例）。
4. 攻击动画已在 Animator 里，且攻击帧上配了动画事件（或先手动调 Enable/Disable 测试）。

## M3：BoxCast 命中

| # | 操作 | 预期 |
|---|------|------|
| 1 | Play 模式，角色挥砍（Hitbox 开启） | BoxCast 每帧执行，无报错 |
| 2 | 武器扫到敌人的 Hurtbox | CombatManager.ReportHit 被调用，敌人 ReceiveHit 被触发（敌人切 StunnedState） |
| 3 | 一次挥砍扫过敌人 | 只触发一次伤害（hitTargets 去重） |
| 4 | 挥砍扫到自己身体的 Hurtbox | 被排除，不受伤（ReportHit 里 owner 判断） |
| 5 | 关闭 Hitbox 后扫过敌人 | 无伤害 |
| 6 | 武器高速挥砍 | 能命中（BoxCast 一段扫描，不穿透） |

## M3：拼刀

| # | 操作 | 预期 |
|---|------|------|
| 7 | 玩家武器和敌人武器同时挥砍、相交 | CombatManager.ReportClash 被调用，触发拼刀逻辑 |
| 8 | 拼刀命中帧 | 双方都播打铁特效/音效（M13/M15 联动） |

## M17：危字攻击

| # | 操作 | 预期 |
|---|------|------|
| 9 | 敌人放突刺（PerilousType.Thrust），玩家举盾防御 | 防御无效，玩家受伤（危字不可防） |
| 10 | 敌人放突刺，玩家朝敌人方向按闪避 | 触发 MikiriCounterState，播踩刀动画 |
| 11 | 识破成功 | 敌人架势大幅上涨 |
| 12 | 敌人放横扫，玩家站立防御 | 防御无效，受伤 |
| 13 | 敌人放横扫，玩家跳起 | 跳过下段判定，不受伤 |

## 常见问题

- **BoxCast 没命中**：检查武器骨骼是否在正确位置、Hitbox 的 cast 范围、LayerMask 是否正确。
- **一次挥砍多次伤害**：hitTargets 去重没生效。
- **打到自己**：ReportHit 里 owner 判断漏了。
- **危字防御无效不生效**：ReceiveHit 里防御分支对 isPerilous 的判断没写。
