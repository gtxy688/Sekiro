# 02 角色属性与战斗数据

> 现状依据：CharacterBody、CombatStats、SO 与 Boss 招式数据源码。手动验收见 02-combat-data-test.md。

## 数据归属

- CharacterConfig 是角色配置 ScriptableObject，供 CharacterBody 的角色参数读取。
- AttackConfig 是招式配置 ScriptableObject，含攻击时长、判定窗口、连招、受击等级、伤害等招式数据。
- BossMoveTable 保存 BossMoveEntry；招式可以包含多个 BossMoveWindow、HitPulse、ArrowSpawnCue 等段级数据。
- CharacterBody 是 Unity 组件入口；运行时血量、架势等数值计算已分到相应的运行时数据 / 逻辑类，CharacterBody 提供编排与转发。
- Faction 是身份标签，不是战斗数值配置。

## 数值求值

玩家招式通常直接使用 AttackConfig。Boss 攻击从招式表选取招式与段；段覆盖、脉冲覆盖和出箭点覆盖可以覆盖招式默认值。AttackCombatResolve 在未显式覆盖时还提供按 HitGrade 和箭矢类别计算的默认伤害、架势伤害。

因此，ScriptableObject 和 BossMoveTable 是主要配置载体，但不能声称所有战斗数值都只来自 SO 或完全没有代码常量。

## 弹反窗口与空按惩罚

主动弹反的全部参数取自 CharacterConfig，没有为它们新建 SO：

| 字段 | 含义 |
|---|---|
| `DeflectWindow` | 基础完美弹反窗口（秒） |
| `DeflectWindowMin` | 窗口下限（秒） |
| `DeflectEmptyGraceCount` | 前 N 次连续空按不缩窗 |
| `DeflectEmptyPenalty` | 超出宽容次数后每个空按的累乘系数，取值 (0, 1] |
| `DeflectEmptyResetInterval` | 相邻两次实际开启尝试的间隔超过该值则清零累计 |

运行时由 CharacterBody 组装成 `DeflectAttemptTracker.Settings` 轻量快照传入 Tracker。非法值（窗口 ≤ 0、下限大于基础窗口、惩罚系数不在 (0, 1]、宽容次数为负、恢复间隔 ≤ 0）由 `Editor/ArpgValidationRules.cs` 报错；运行时 CharacterBody 仍会夹紧并告警一次，避免除零或弹反窗口永久消失。

字段迁移：旧的按次数记账字段 `DeflectMashLimit` / `DeflectMashWindow` / `DeflectMashPenalty` 已删除，改为上面的按结果记账字段。`DeflectMashWindow` → `DeflectEmptyResetInterval`、`DeflectMashPenalty` → `DeflectEmptyPenalty` 用 `FormerlySerializedAs` 保留资源旧值；宽容次数由 float 改成 int，无法直接迁移，按默认值 3 与两个配置资源里的显式取值处理。

## 受击与架势

CombatResolver 把结算参数传给 CharacterBody.ReceiveHit。当前状态可以先处理命中；未被状态处理的命中进入生命值、架势与受击状态处理。格挡、弹反、闪避、架势崩解及死亡规则分布在 CharacterBody、CombatStats 与状态类中。

## 其他战斗数据路径

葫芦、复活和死亡状态由 CharacterBody 及对应状态编排；架势变化、死亡、复活、胜利等表现信息通过 CombatEventBus 通知订阅方。精确数值与状态转移需以对应配置资源和源码为准。

## 核心源码

- SO/CharacterConfig.cs
- SO/AttackConfig.cs
- SO/AttackCombatResolve.cs
- Boss/BossMoveTable.cs、Boss/BossMoveEntry.cs、Boss/BossMoveWindow.cs
- FrameWork/Body/CharacterBody.cs
- Combat/CombatResolver.cs
