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

## M7：弦一郎 AI

| # | 操作 | 预期 |
|---|------|------|
| 5 | 玩家在 7m 外 | Boss 追击（BT_MoveToTarget） |
| 6 | 玩家进入 3-5m | Boss 放近战连段（BT_Combo，2-3 连砍） |
| 7 | 玩家在 5-7m | Boss 接近后横砍 + 射箭 |
| 8 | 玩家持续攻击 Boss | 触发 BT_Deflect（Boss 招架） |
| 10 | 同一招式连续用 | 冷却生效，不连续放同一招 |
| 11 | 被玩家盾反 | 触发交锋变招（侧垫步 + 重砍） |

> #9（危字攻击）已移除：M17 不在本项目范围。

## 常见问题

- **Boss 不动**：检查黑板 target 是否设置、BT_MoveToTarget 是否读到 target。
- **连段乱**：BT_Combo 内部第几刀索引没维护好。
- **一直放同一招**：冷却字典没检查。
- **被弹反不变招**：交锋计划触发条件（OnHitReceived 弹反成功 → 通知 BTBrain）没接。
