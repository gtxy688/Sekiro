# 04 行为树 / Boss AI - 验收清单

> 配合 `04-behavior-tree-ai.md` 使用。

## 前置准备

1. Boss 模型挂 `CharacterBody` + `BTBrain`。
2. 场景有玩家，BTBrain 的 `PlayerTarget` 拖入玩家 Transform。
3. `BTBrain.moveTable` 指向 `Assets/SO/Boss/GenichiroMoveTable.asset`。Boss 出招走招式表，不需要 `LightAttack` / `AttackSet`。
4. Boss 右手刀刃采样点挂 `Hitbox`；未挂时 AI 可以追击和播攻击，但不会造成伤害。

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
| 8 | ≤3m | 能见 `Slash_StepTurn`、`Kick`、`Bow_Air5`；**未**连续完美弹开两次时不应出 `JumpThrust` |
| 8b | 贴身连续完美弹开 Boss 两刀（近战，不要箭），保持 ≤5m | 下一次主动抽招有较大机会 `JumpThrust`（3022）。这不是交锋 `Kengeki_Thrust`（3062） |
| 8c | 连弹一次后改成普通格挡或挨一刀 | 连弹计数清零，不应立刻跳 |
| 9 | 完美弹刀且未崩解、贴身 | 还击来自交锋表；多次 `Kengeki_Slash` 会换片 |
| 10 | 弹刀后拉开 >2.5m | 不交锋，回主动 |
| 11 | Boss HP <75% 时弹刀 | 有机会 `Boat_Full` |
| 12 | 逼出 `JumpThrust` | 起跳无危字；落地**一定是突刺**（可识破） |
| 12c | 落地突刺 | 「危」在落地弹出；无方向垫步可识破（同 9） |
| 12b | `Kengeki_Thrust`（交锋 3062） | 危字突刺，识破仍崩解 |
| 14 | 玩家喝药，Boss **正在近战/交锋** | 当前招打完，再等剩余 `HealPunishDelay`（若已到点则立刻）出 `Bow_Heavy`，中途不切重箭 |
| 14b | 玩家喝药，Boss **走位/待机** | 等 `HealPunishDelay` 后出 `Bow_Heavy`；箭在出箭点（约 1.5s）飞出。调大 Delay 应能在喝完后抬刀弹反 |
| 15 | 空中五连 | 能完整播完；有时会被重箭打断 |
| 16 | 玩家锁定 Boss 后重复远近移动 | Boss 仍面向并追踪玩家 |
| 17 | 走近后挥刀打 Boss（Boss 非攻击中） | 命中瞬间 Boss 强制进入格挡判定：普通格挡（Block/`Hurt_Guard` 姿态 + 火花 + Boss 架势涨），不再裸受击 |
| 18 | 用识破令 Boss 架势刚好崩解 | Boss 播 `Stagger_Broken_Miriki`，确认窗口不猛转到玩家；按攻击后成对忍杀才水平对视 |
| 19 | Console | 缺 Animator 状态的招不出（权重 0），无新的每帧 error |
| 20 | 连续挥刀打 Boss（未抓前摇） | 第 1、2 刀被普通格挡；**第 3 刀 Boss 强制完美弹反**（大火花 + 你被弹开硬直），随后 Boss 开始反击 |
| 20a | 贴身触发 Boss 完美弹反，确认抽到单段 `Kengeki_Slash`；玩家恢复后立即普通攻击，多次重复 | Boss 反击目标为玩家预计恢复后 0.1s，玩家第一刀判定为起手后 0.23s；检查 Boss 是否先命中、玩家攻击被打断，不把时间预测当作已验收结果 |
| 20b | 同上，玩家恢复后改为格挡或垫步 | 玩家仍能防御/回避；被弹硬直退出条件不因本改动延长 |
| 20c | 抽到长前摇或先垫步/射箭的多段交锋招 | 各段仍按招式表执行；等待反击时不提前推进下一段，不要求这些招式都抢先命中 |
| 20d | 玩家完美弹反 Boss，以及普通格挡、飞舟末刀互弹 | 保持原有硬直、连续弹反和动画流程；本次只调整被动完美弹反的反击计时 |
| 21 | Boss 出手动画中（前摇/命中段）打它 | 普通挥砍：正常受击（可抓前摇），不进入被动格挡 |
| 21b | Boss 放危字（突刺/肘击 Grab）时打它 | **打不断**：Boss 继续播招，仍扣血涨架势；架势打满仍崩 |
| 21c | Boss 放 `Boat` / `Boat_Full` 时打它 | 同 21b，飞舟打不断 |
| 21d | Boss 放 `JumpThrust` 起跳（尚未落地）时打它 | 同 21b：虽无危字，起跳仍霸体 |
| 22 | 打 Boss 两刀后停手 3s 再打 | 连续格挡计数清零（2.5s 重置窗口），下一刀重新从普通格挡开始 |
| 23 | 危字攻击（突刺/肘击 Grab 投技）打 Boss | 不触发被动格挡；正常走识破/垫步链路 |

## 常见问题

### 玩家弹反后第一刀先手的手动检查

- 开启双方 Enable State Debug Log，Console 搜索 CombatTiming。普通近战末刀被玩家完美弹反且 Boss 进入 ParriedState 后，核对 BossAttackSchedule：当前玩家标准第一刀为弹反后 0.93s，Boss 最早判定为弹反后 1.03s；倒推起手等于最早判定减首段真实攻击事件时间。
- 选中本来就够慢的招式时不额外等待；起手较早的招式应等倒推时刻再产生 AttackStart。硬直自然退出、格挡和原有冷却继续有效，不要求起手正好等于恢复时刻。
- 等待期间用玩家第一刀接触已恢复的 Boss：仍可被动格挡；普通格挡结束后再起手，不跳过选中招式首段。Boss 完美弹回玩家时改走原交锋反击，不继续旧等待。
- 抽到首段垫步/位移且无攻击事件的招式时，首段等到保护截止后再开始；确认后续段与镜头正常。复战后没有上轮时间窗口残留。
- 弹箭、飞舟互弹、不可打断/多段中间刀、识破和架势崩解不启用本窗口；玩家反击明显迟于 0.7s 准备预算时不保证先手，伤害与格挡结果仍以实际接触为准。

- **Boss 不动**：检查黑板 target 是否设置、`BT_MoveToTarget` 是否读到 target。
- **一直放同一招**：冷却字典没检查。
- **Boss 弹反后不反击**：检查完美弹反成功方是否置 `KengekiArmed`、目标是否可行动、距离是否在交锋范围，以及招式是否可播放。Boss 弹反玩家会武装交锋；玩家弹反 Boss 时 `ForceParryStun(armKengeki: false)` 不给 Boss 武装交锋。
- **从不跳 JumpThrust**：先贴身近战完美弹开两次；看 BTBrain 是否勾了 `MeleeOnly`（白名单不含 JumpThrust）；距离须 ≤5m。
- **Boss 还是裸受击**：确认 `BTBrain.Start` 已置 `body.EnablePassiveDeflect = true`（只给 Boss；玩家不开启）。
- **Boss 弹反太频繁**：调大 `passiveDeflectThreshold`（默认 2 = 第 3 刀弹反）或 `passiveDeflectResetWindow`（默认 2.5s）。
