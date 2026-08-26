# 04 行为树 / Boss AI - 验收清单

> 配合 `04-behavior-tree-ai.md` 使用。

## 前置准备

1. Boss 模型挂 `CharacterBody` + `BTBrain`。
2. 场景有玩家，BTBrain 的 `PlayerTarget` 拖入玩家 Transform。
3. `CharacterBody.LightAttack` 使用 Boss `Attack1.asset`，不要引用玩家 atk1。
4. `BTBrain.moveTable` 指向 `Assets/SO/Boss/GenichiroMoveTable.asset`。
5. Boss 右手刀刃采样点挂 `Hitbox`；未挂时 AI 可以追击和播攻击，但不会造成伤害。

## M5：行为树框架

| # | 操作 | 预期 |
|---|------|------|
| 1 | Play 模式 | Boss 开始追击玩家（发 MoveCommand），Console 无报错 |
| 2 | 站在 Boss 攻击范围内不动 | Boss 触发攻击，进 AttackState |
| 3 | 看 Debug.Log 检查黑板 | target/attackRange 等值存在且正确 |
| 4 | 连续多次 Evaluate | Running 记忆生效：Sequence 不从头重跑（加打印验证 currentChildIndex） |

## M7：弦一郎 AI（完整薄树）

> 简单版「隔 2.5s 砍一刀」已被完整树替换。追击朝向、招架弹开、崩解忍杀仍要回归。

| # | 操作 | 预期 |
|---|------|------|
| 5 | Play，站远处（>7m） | 会 `Bow_ThenSlash` / `Bow_Shot` 或 `Slash_Rush2`，不站桩空挥 |
| 6 | 5–7m | 能见 `Slash_Rush2` / 飞舟（Boss 架势累计过半、偏高时 `Boat`） |
| 6b | `Boat` 的 `Boat1` 段贴身吃满 | 同一段动画里应能挨到 **最多 5 下**（不是整段只一下）；`Boat2` 仍一刀。时间不准就改 `hitPulses` |
| 7 | 3–5m | 能见 `Slash_Double` / `Slash_Heavy` / `Slash_SpinElbow` |
| 8 | ≤3m | 能见 `Slash_StepTurn`、`Kick`、`Bow_Air5` |
| 9 | 完美弹刀且未崩解、贴身 | 还击来自交锋表；多次 `Kengeki_Slash` 会换片 |
| 10 | 弹刀后拉开 >2.5m | 不交锋，回主动 |
| 11 | Boss HP <75% 时弹刀 | 有机会 `Boat_Full` |
| 12 | `JumpThrust` / `Kengeki_Thrust` | 危字 + 识破仍崩解 |
| 13 | `Perilous_Sweep` | 危字 + 跳踩仍成立 |
| 14 | 玩家喝药（Boss 正在近战/走位都可以） | Boss **立刻打断**当前招，改出 `Bow_Heavy`；葫芦动画期间箭应能打中 |
| 15 | 空中五连 | 能完整播完；有时会被重箭打断 |
| 16 | 玩家锁定 Boss 后重复远近移动 | Boss 仍面向并追踪玩家 |
| 17 | 走近后挥刀打 Boss（Boss 非攻击中） | 命中瞬间 Boss 强制进入格挡判定：普通格挡（Block/`Hurt_Guard` 姿态 + 火花 + Boss 架势涨），不再裸受击 |
| 18 | 用识破令 Boss 架势刚好崩解 | Boss 播 `Stagger_Broken_Miriki`，忍杀逻辑不变 |
| 19 | Console | 缺 Animator 状态的招不出（权重 0），无新的每帧 error |
| 20 | 连续挥刀打 Boss（未抓前摇） | 第 1、2 刀被普通格挡；**第 3 刀 Boss 强制完美弹反**（大火花 + 你被弹开硬直），随后 Boss 开始反击 |
| 21 | Boss 出手动画中（前摇/命中段）打它 | 正常受击（玩家可抓前摇破招），不进入被动格挡 |
| 22 | 打 Boss 两刀后停手 3s 再打 | 连续格挡计数清零（2.5s 重置窗口），下一刀重新从普通格挡开始 |
| 23 | 危字攻击（突刺/横扫）打 Boss | 不触发被动格挡；正常走识破/跳踩链路 |

## 常见问题

- **Boss 不动**：检查黑板 target 是否设置、`BT_MoveToTarget` 是否读到 target。
- **连段乱**：`BT_Combo` 内部第几刀索引没维护好（完整树已不挂该节点）。
- **一直放同一招**：冷却字典没检查。
- **被弹反不变招**：交锋依赖 `ForceParryStun` 置 `KengekiArmed`，硬直结束且距离 ≤ 2.5m 才会抽交锋表。
- **Boss 还是裸受击**：确认 `BTBrain.Start` 已置 `body.EnablePassiveDeflect = true`（只给 Boss；玩家不开启）。
- **Boss 弹反太频繁**：调大 `passiveDeflectThreshold`（默认 2 = 第 3 刀弹反）或 `passiveDeflectResetWindow`（默认 2.5s）。
