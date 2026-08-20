# 04 行为树 / Boss AI - 验收清单

> 配合 `04-behavior-tree-ai.md` 使用。

## 前置准备

1. Boss 模型挂 `CharacterBody` + `BTBrain`。
2. 场景有玩家，BTBrain 的 `PlayerTarget` 拖入玩家 Transform。
3. 确认 M3（命中）已接好，否则 AI 只会跑不会打。

## M5：行为树框架

| # | 操作 | 预期 |
|---|------|------|
| 1 | Play 模式 | Boss 开始追击玩家（发 MoveCommand），Console 无报错 |
| 2 | 站在 Boss 攻击范围内不动 | Boss 触发攻击，进 AttackState |
| 3 | 看 Debug.Log 检查黑板 | target/attackRange 等值存在且正确 |
| 4 | 连续多次 Evaluate | Running 记忆生效：Sequence 不从头重跑（加打印验证 currentChildIndex） |

## M7：弦一郎 AI（当前简单版）

| # | 操作 | 预期 |
|---|------|------|
| 5 | Play，站远处 | Boss 朝你走过来（不是跟着镜头歪跑） |
| 6 | 让他靠近 | 砍一刀，然后停约 2.5s 再砍 |
| 8 | 他靠近时你挥刀 | 短按格挡；窗口内砍中 → 你被弹开 |
| 10 | 同一刀打完 | 不会立刻连砍，要等攻击冷却 |

> 连段/射箭/飞舟/交锋变招、#9 危字：这版没有。#7、#11 暂不验。

## 常见问题

- **Boss 不动**：检查黑板 target 是否设置、BT_MoveToTarget 是否读到 target。
- **连段乱**：BT_Combo 内部第几刀索引没维护好。
- **一直放同一招**：冷却字典没检查。
- **被弹反不变招**：交锋计划触发条件（OnHitReceived 弹反成功 → 通知 BTBrain）没接。
