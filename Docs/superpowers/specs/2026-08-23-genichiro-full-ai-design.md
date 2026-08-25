# 弦一郎完整 AI（M7）设计规格

日期：2026-08-23  
状态：已批准（用户确认后进入实现计划）  
依据：`Docs/architecture/04-behavior-tree-ai.md`、`Docs/references/sekiro-genichiro-ai.md`、`Docs/architecture/04-boss-ai-move-assets.md`  
关系：覆盖 2026-08-05 规格中的 **Boss 选招/交锋**；战斗结算、HFSM、危字应对仍走现有 CombatManager / 状态机。简单树（追击 + 定时一刀 + 招架）验收已通过，本规格在其上替换选招，不重写受击/崩解/忍杀。

## 1. 目标与红线

用加权招式表驱动行为树，让一阶段弦一郎按距离、弹刀、喝药作出接近原作的选择。

红线：

- 只做 1 阶段、2 条命。不做脱衣、巴流、雷、雷回。
- 不做普攻 1-2-3-4 连段机。近战是具名招。
- 交锋不区分左弹/右弹。
- `AttackCommand` 仍不携带配置；出招前写入当前招数据，`AttackState` 读取。
- 伤害/判定窗跟动画走；选招（距离、权重、冷却、层）跟表走。
- 玩家连招继续用现有 `AttackConfig` 链，不改成表。
- 缺 Animator 状态的招：权重视为 0，不进抽取。

成功标准：玩家在远/中/近都能遇到不同招；弹刀后会还击而不是立刻再打同一套主动近战；危字突刺可识破、横扫可跳踩；飞舟、弓、空中五连、重箭都会出现；招架仍能弹开玩家。验收为 Unity 手测清单，无自动化测试。

## 2. 行为树（薄树 + 表）

每帧 Selector，上到下：

1. **崩解/忍杀中** → 不选招（现有状态机已接管）。
2. **交锋层**：本帧处于「刚被完美弹刀」窗口 → 从交锋表加权抽一招，Running 直到该招动画结束。
3. **招架层**：玩家正在攻击 且 距离 ≤ 招架距离 且 招架冷却好 → 现有 `BT_Parry`（短按招架）。
4. **打断：喝药** → 蓄力重箭 `Bow_Heavy`（3023），冷却独立。
5. **主动层**：按与玩家距离选档，从该档加权抽一招（已在冷却的权重当 0）。
6. **移动**：没有可出的招 → `BT_MoveToTarget`（Walk / Walk_Strafe）。

Running 记忆：正在播的招不因距离微变而取消。招结束才重新选。

交锋窗口：玩家完美弹刀且未把 Boss 弹到崩解时，Boss 进现有被弹硬直结束前/结束瞬间进入交锋选招。距离 > 2.5m 则交锋抽空（NoAction），回到主动层。

## 3. 数据：一份 `BossMoveTable`

一个 ScriptableObject，List 一行一招。建议字段：

| 字段 | 含义 |
| --- | --- |
| id | 与资源表招式 ID 一致，如 `Slash_Double` |
| animStates | Animator 状态名，按顺序播（一段就一项；Kick=`Attack_Slash` 然后 `Kick`） |
| variantAnimStates | 可选。非空则每次出招从若干套 `animStates` 里均匀随机（`Kengeki_Slash` 五条） |
| baseDamage / postureDamage / knockback | 判定数值 |
| perilous | None / Thrust（识破）/ Sweep（跳踩） |
| hitStartTime / recoverStart / stateDuration / rotateEnd | 与现有 AttackConfig 窗口同义；多段时每段可各写一套，或只写在段上 |
| minRange / maxRange | 主动层距离带（米） |
| layer | Active / Kengeki / Interrupt |
| weight | 该层/该档相对权重 |
| cooldown | 秒 |
| extraConditions | 可选：`hpBelow75`、`postureBelow360`、`afterAir5` 等 |

出招：按段把当前行拷进内存中的 `AttackConfig`（`CreateInstance` 或等价只读结构）赋给 `ActiveAttack`，再 `AttackCommand`。多段：第一段结束（StateDuration）后立刻切下一段，整招 Running 到最后一段结束再写冷却。

弓：第一版判定走路过 Hitbox（与刀相同），不强制做投射物。重箭同理。以后加箭矢不改选招表。

## 4. 主动层距离档（权重来自原作数量级，可调）

距离用根位置，与现有 `attackRange` 同一套。

| 距离 | 可抽（权重） |
| --- | --- |
| > 7m | Bow_ThenSlash(600)、Slash_Rush2(300)；无 3014 则 Bow_RollSlash 顶 Bow_ThenSlash |
| 5–7m | Slash_Rush2(300)、Slash_RushThenBow(100)、Boat(300，架势≤360 时权重大) |
| 3–5m | Slash_Double(10)、Slash_Heavy(30)、Slash_Spin+Elbow(15，两段当一招)、Boat(架势低时 300) |
| ≤ 3m | Slash_StepTurn(15)、Slash_Spin+Elbow(15)、Kick(30)、Dodge_Back 后 Bow_Air5(30) |
| ≤ 5m 额外 | JumpThrust 作为危字识破，单独条件/权重，不跟普近战混成 1-2-3-4 |
| 危字横扫 | Perilous_Sweep 进主动表，权重单独可调 |

`Slash_Spin` 与 `Elbow` 按原作 3037→3020 做成 **一条招两段**，不要拆成两次选招。

`Boat` = 3040 然后 3041，只出现在主动层。

## 5. 交锋层

一张表，不按左右弹分。被弹且距离 ≤ 2.5m 时抽：

| 招式 ID | 原作 | 权重意向 |
| --- | --- | --- |
| Kengeki_Slash | 3050/3055/3065/3071/3076 均匀随机 | 高 |
| Kengeki_Double | 3063 或 3068 | 中 |
| Kengeki_Thrust | 3062 危字识破 | 中 |
| Kengeki_Heavy | Step_L 或 Step_R 然后 3007 | 中 |
| Kengeki_Bow | 先 3031，再随机：3019→3029 或只 3036 | 中 |
| Kengeki_Air5 | Dodge_Back 然后 Bow_Air5 | 中（对应 200210/200211） |
| Boat_Full | 3028；仅 HP&lt;75% 且飞舟冷却好 | 中 |

`Kengeki_Bow2Slash`、`Kengeki_JumpBow`、`Bow_AirHeavy` 有状态就进表，没有权重 0。

## 6. 招架与打断

- 招架：保持简单树已验收行为（距离 + 玩家攻击中 + 冷却）。
- 喝药：Interrupt 出 `Bow_Heavy`。
- 空中五连播到一半时，可按原作用 `Bow_Heavy` 打断（50% 或可调）；没有打断也必须能完整播完五连。

## 7. 与现有代码的衔接

- 替换 `BTBrain` 里「冷却好了就 HitOnce」为「按层抽表」。
- `BT_Combo` 不再按 `AttackSet` 下标当普攻连；具名招的多段用表的 `animStates`。
- `BT_Bow` 占位 CrossFade 名字改为表里的状态名。
- 崩解、忍杀、受击、IgnoreCollision 刀身判定：不在本规格改。
- Animator 状态名以 `04-boss-ai-move-assets.md` 已填为准；表里 `animStates` 与之一致。

## 8. 实现阶段（仍是一份规格）

1. `BossMoveTable` + 抽招 + 主动四档 + 现有近战/危字状态能播。
2. 交锋窗口 + Slash/Double/Thrust/Heavy。
3. 弓、空中五连、重箭（含喝药打断）。
4. 飞舟主动两段 + 交锋 Boat_Full。
5. 招架层与简单树对齐回归；手测清单全过。

## 9. 手测清单（摘要）

- 远：会射箭或冲近两刀，不会站桩空挥。
- 中：能出重砍/二连/旋转肘/飞舟。
- 近：能出转身砍、踢、后跳五连射。
- 弹刀：还击来自交锋表；贴身砍一刀会在五条 Slash 片间变化。
- 残血弹刀：可能 Boat_Full。
- 突刺识破、横扫跳踩仍成立。
- 喝药会被重箭。
- 简单树已过的追击朝向、招架弹开、崩解忍杀不回归。

## 10. 规格自检

- 无 TODO/待定挡实现的项。权重写「意向」，实装用表调。
- 与资源表一致：Boat≠Boat_Full；Kengeki_Slash 五随机；不拆左右弹。
- 范围：一份规格、五个实现阶段，不把二阶段雷写进来。
- 多段动画：顺序 CrossFade，不以玩家 NextCombo 语义给 Boss 乱用。
