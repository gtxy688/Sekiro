# 07 动画事件 - 验收清单

> 配合 `07-anim-events.md` 使用。这个模块主要是你在 Unity 里操作。

## 前置准备

1. 攻击动画 Clip 已就位（项目里有动画文件）。
2. Hitbox 已挂武器骨骼上，CombatManager 已就位（M3）。

## 配置验收

| # | 操作 | 预期 |
|---|------|------|
| 1 | 打开动画 Clip，在挥刀起始帧加 EnableHitbox 事件 | 事件出现在时间轴上 |
| 2 | 在挥刀结束帧加 DisableHitbox 事件 | 事件出现在时间轴上 |
| 3 | Play 模式，玩家挥砍 | 命中帧才造成伤害，收刀后无伤害 |

## 触发验收

| # | 操作 | 预期 |
|---|------|------|
| 4 | 播放攻击动画，观察 Console | 动画事件调用 EnableHitbox，无 "not found" 报错 |
| 5 | 挥砍扫到敌人 | 伤害在 Enable 之后、Disable 之前才能命中 |
| 6 | 忍者杀（若已配） | ExecuteFinisher 被调用，Boss 扣命 |
| 7 | 喝葫芦动画 | OnDrinkGourdAnimEvent 被调用，回血生效 |

## 常见问题

- **动画事件报 "Function not found"**：方法名和事件 Function 不一致，或方法不在事件目标对象上。
- **伤害提前/延后**：EnableHitbox 帧位置不对，调整时间轴。
- **每次只配一个 Clip 生效**：每个攻击 Clip 都要单独配，连招的每一段都是独立 Clip。
