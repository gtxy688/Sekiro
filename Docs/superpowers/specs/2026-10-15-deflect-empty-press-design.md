# 主动弹反与空按惩罚重构设计

日期：2026-10-15  
分支：`codex/deflect-empty-press-redesign`  
状态：已实施，编译通过，并经用户在 Unity 内验收（2026-10-15，反馈无阻塞问题）。正文保留设计提案身份，实施与验证记录见第 13 节。

## 1. 目标与范围

将玩家主动防御统一为：按下开启有限弹反窗口；按住维持普通格挡；只有未成功弹反的有效防御尝试才累计空按惩罚；成功弹反立即清除惩罚。连续弹反能力由玩家自己的操作与命中结果决定，不再读取 Boss 招式 ID 来豁免抖刀限制。

本次完整处理输入、防御尝试生命周期、窗口判定、惩罚记账和旧规则迁移。不是仅在成功回调里清零一个计数。保留 HFSM、命中结算链、Boss 被动防御、交锋反击和招式打断规则，不重写整个战斗系统。

本文件描述拟实现行为；当前实现说明仍以源码和活跃架构文档为准。文档中的拟议 API 不是已有接口。

## 2. 当前实现与问题

已核对的代码入口：

- `Assets/Scripts/Player/Brain/PlayerBrain.cs`：`OnDeflectStarted` 缓冲 `DeflectCommand`；`OnDeflectCanceled` 缓冲 `IdleCommand`。崩解期间另有按住防御自动提交命令的入口。
- `Assets/Scripts/Player/Brain/BrainBase.cs`：仅保存一个 `bufferedCommand`；新命令覆盖旧命令，消费失败时重试。
- `Assets/Scripts/FrameWork/Body/DeflectMemory.cs`：`RegisterDeflectPress` 累计每次主动防御进场，不检查是否弹中；成功弹反没有清罚入口。
- `Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`：进场缓存 `enterTime/window`；命中判定使用 `elapsed <= window || inBegin`，并叠加 `raiseSpamGuard`；重新防御通过重新构造状态刷新窗口。
- `Assets/Scripts/Combat/HitReactionUtil.cs`：`IsMultiHitParryException` 按招式 ID 或 `hitPulses` 判断多段招，绕过 `raiseSpamGuard`，但不恢复已缩小的窗口。

当前 `Assets/SO/PlayerConfig.asset` 配置：基础窗口 0.5 秒，连点间隔 0.5 秒，次数阈值 3，缩窗系数 0.75，最小窗口 0.1 秒。成功近战弹反增加攻击者架势 6，普通格挡架势系数 0.5。

需要解决的问题：

1. 准确连续弹反和空按都累计相同惩罚。
2. 缩窗、频繁抬刀限制、多段招豁免同时存在，规则分散。
3. `inBegin` 能越过时间窗口，动画可能让缩窗失效。
4. 快速按下再松开，释放命令可能覆盖尚未消费的防御命令。
5. 重新进状态、缓冲重试、崩解自动回防不等于新的物理按下，不能重复记账。
6. 飞舟末刀等成功分支可能直接切换状态，清罚不能只放在普通成功路径末尾。

## 3. 方案选择

### 推荐：按有效尝试的结果记账

每次实际开启主动弹反窗口生成一个尝试。窗口结束前弹中至少一次则成功，否则为空按。成功清罚；连续空按缩短下一次窗口。该方案不依赖敌人招式名，能明确处理重试、释放和状态切换。

### 不采用：保留现有按次计数，仅在成功后清零

改动较小，但仍会把尚未得到结果的按下算作失败；独立的抬刀限制和动画窗口也仍然存在，不能完整满足“空按才惩罚”。

### 不采用：继续扩展多段招白名单

新增招式需要补特例，且无法修复输入覆盖和缩窗被动画绕过的问题。

## 4. 操作与结果定义

### 4.1 什么算一次有效尝试

玩家真实按下防御，命令最终让身体实际开启主动弹反窗口，才创建尝试。仅入缓冲、被状态忽略但返回已消费、缓冲超时、被其他命令覆盖，都不创建尝试、不计空按。

窗口从身体实际接受并开启防御的 `Time.time` 开始。预输入时间不直接作为起点，避免窗口在动作允许回防前就耗尽。输入延迟由缓冲时长约束，不追溯已经发生的命中。

每次真实按下携带递增 `PressId`；同一个 ID 被缓冲重试或状态重入时只能开启一次。主动尝试还持有独立 `AttemptId`，成功与结束操作均须幂等。

### 4.2 什么算空按

“空按”定义为一次实际开启窗口、但结束前没有成功弹反的尝试，不局限于场上完全没有攻击。

- 窗口自然到期且未弹中：空按一次。
- 新的有效按下提前替换尚未成功的尝试：旧尝试空按一次，再开启新尝试。
- 玩家主动垫步等离开防御，未成功尝试提前结束：空按一次。
- 窗口内遇到无法弹反的命中并被迫受击：按失败结束，空按一次。
- 普通格挡不清罚；窗口外格挡不会为已经结束的尝试再追加失败。
- 死亡、复战、对象停用或输入模式整体切换：取消未完成尝试，不记空按；清除本轮输入与惩罚记忆。

普通长按只开启一次尝试。即使长时间没人攻击，也最多记一次空按，不按帧或按每次普通格挡重复累计。

### 4.3 什么算成功

主动尝试窗口内，命中经现有规则确认可以完美弹反，并实际进入成功处理路径，即记为成功。近战、箭矢以及当前允许弹反的危字命中统一清罚；Boss 被动防御不参与玩家主动尝试记账。

成功立即清除连续空按次数和窗口缩减；当前尝试记为成功，后续结束不再记空按。飞舟末刀双方弹开、重箭、架势崩解转忍杀等分支也必须经过同一个成功记账入口，且在可能的状态切换之前完成。

### 4.4 一次按下能否弹多刀

保留现有的一窗多次命中能力：一次窗口可以弹到多刀。成功不刷新起点、不延长截止时间，也不扩大当前尝试缓存的窗口；恢复的正常窗口从下一次有效按下开始使用。

因此长按不会自动连续续窗。若希望五刀严格对应五次按下，需要另外引入“一刀消耗窗口”规则，本次不增加这条要求。

## 5. 唯一的时间判定

主动弹反资格只由未取消的尝试及其固定时间区间决定：`startedAt <= now && now < expiresAt`。截止边界统一使用半开区间；命中恰好等于截止时间时不算完美弹反。

`inBegin` 仅控制抬刀动画，不再作为完美弹反资格；`Deflect_Begin`、`Deflect_Repeat` 播放速度或长度不能延长窗口。删除 `raiseSpamGuard` 对主动弹反资格的干预，删除仅为此服务的取消时间记忆。`guardStable` 等字段是否删除，依据其剩余表现用途决定，不能机械连带删除。

状态 Update 和命中入口都必须先按当前时间推进尝试到期，防止两者执行顺序改变窗口外命中的结果。同帧先后发生的输入接受和命中按真实执行顺序处理，不回滚、不开事后窗口。

使用 `Time.time` 的缩放时间，和现有战斗时间一致；暂停与 HitStop 期间不因墙钟流逝额外耗尽窗口。

## 6. 空按惩罚公式与恢复

拟议配置：

- `DeflectWindow`：沿用 0.5 秒作为迁移起点。
- `DeflectEmptyGraceCount`：3，前 3 次连续空按不缩窗。
- `DeflectEmptyPenalty`：0.75，每个超出宽容次数的空按累乘。
- `DeflectWindowMin`：沿用 0.1 秒。
- `DeflectEmptyResetInterval`：0.5 秒，相邻实际开启尝试的间隔超过此值时恢复。

令连续空按次数为 `emptyCount`，则下一次窗口：

```text
penaltySteps = max(0, emptyCount - DeflectEmptyGraceCount)
nextWindow = max(DeflectWindowMin,
                 DeflectWindow * pow(DeflectEmptyPenalty, penaltySteps))
```

顺序固定为：先结束/结算旧尝试；若距上次实际开启尝试超过恢复间隔，则清罚；根据当前空按次数计算新窗口；创建新尝试。不要仅由结算发生的时间判断“最近空按”，否则缓慢、正常的点按也可能被连续惩罚。

在按键间隔始终不超过恢复间隔且一直空按的情况下：尝试 1～4 的窗口仍为 0.5 秒；第 4 次失败后，尝试 5 为 0.375 秒；第 5 次失败后，尝试 6 为 0.28125 秒。成功一次后，下一次恢复为 0.5 秒。

这是结果记账与旧按次记账的明确区别：不能在第 4 次按下时提前惩罚尚未发生的第 4 次失败。上述参数是迁移提案，不代表已经修改资源或确认手感。

配置约束：基础窗口大于 0；最小窗口大于 0 且不超过基础窗口；惩罚系数处于 `(0, 1]`；宽容次数不小于 0；恢复间隔大于 0。编辑器校验加入 `Assets/Editor/ArpgValidationRules.cs`，运行时读取异常值仍须给出安全兜底，避免除零或永久失去窗口。

## 7. 输入、松开与自动回防

### 7.1 防御释放独立于命令单槽

保留 BrainBase 的通用单槽缓冲，不把所有动作改成队列。仅为防御增加输入边沿身份与最新按住状态：真实按下更新 `PressId` 和按住状态，再缓冲带 ID 的 `DeflectCommand`；松开通过 CharacterBody 的语义入口记录释放，不再用 `IdleCommand` 覆盖防御按下。

释放信息必须关联 PressId；延迟处理旧释放不得关闭新按下。只有真实物理按下生成新 ID。状态是否实际开启窗口由身体/状态入口确认，不能用 `TryExecuteCommand == true` 代替这项确认。

输入被暂停、结算或生命周期阻断时，清除防御按住状态和待处理的防御命令；恢复输入后以新的物理按下开始主动尝试。不要在按键仍保持按住时自动合成多个新 ID。

### 7.2 短点按保留窗口

本方案选择：松开不立即取消尚未到期的弹反窗口，也不立即结算为空按。身体保留主动窗口至截止时间，允许短点按后在窗口内弹中；到期后若已松开则收刀，若仍按住则进入普通格挡。

这会改变当前松开立即 `StartCancel` 的行为，是本次明确的操作设计调整。提前切垫步等动作仍可以主动取消窗口；松开后到期，不继续普通格挡。窗口已到期时释放可直接进入现有收刀流程。

如果按下命令延迟消费时该 ID 已释放，只要命令仍在有效缓冲期且状态允许，就开启一次短点按窗口并标记已释放；不能凭未按住直接丢掉这次有效点按。

### 7.3 崩解期间按住自动回防

区分物理按下与系统请求。崩解恢复处自动提交的防御请求只恢复普通格挡，不凭按住自动开启新的完美弹反尝试，不累计空按。若真实的新按下仍在有效缓冲内，则实际接受它时开启一次窗口。

拟议 `DeflectCommand` 增加 `PressId` 与输入来源；既有无参数构造兼容为系统来源。系统命令可以请求防御姿态，但不能伪造物理按键身份。梳理 UI 和 AI 的现有构造调用，使其语义显式，避免默认构造误开主动窗口。

## 8. 模块职责与拟议接口

新增 `Assets/Scripts/FrameWork/Body/DeflectAttemptTracker.cs`，命名空间 `ARPG.FrameWork.Body`。普通 C# 类，仅持有主动尝试和惩罚数据，不直接查询 Input System、Animator、Boss 招式或操作状态机。时间与配置由调用方传入，方便确定边界。

建议公开操作：

- `BeginAttempt(pressId, now, settings)`：结算旧尝试、处理间隔恢复、缓存窗口并返回 AttemptId；拒绝重复 ID。
- `Advance(now)`：到期结算，一次尝试只结算一次。
- `CanParry(attemptId, now)`：统一窗口资格查询。
- `RecordSuccess(attemptId, now)`：仅当前有效尝试成功清罚，不扩展截止时间。
- `EndAttempt(attemptId, reason, now)`：区分主动取消/受击失败和生命周期取消，幂等处理。
- `Reset()`：复战等清除尝试、计数和输入身份。

拟议设置是轻量数据快照，来源仍为 CharacterConfig，不新建独立 SO。成功/未完成/失败是内部生命周期，状态机通过明确结束原因通知 Tracker，不能靠 OnExit 猜测离开原因。

`DeflectMemory` 保留 Boss 被动连挡升级和连续被弹计数；玩家主动惩罚迁往 Tracker。`CharacterBody` 作为 Façade 提供输入状态、开始尝试、命中资格与结果通知封装，状态不直接访问 Tracker 内部字段。所有地面切换继续走现有封装/父状态子状态机，顶层只装父状态。

## 9. 删除与保留边界

删除主动判定中的 `IsMultiHitParryException` 调用，确认没有其他用途后删除该方法；删除 `raiseSpamGuard` 及专为它服务的 `LastDeflectCancelTime/NotifyDeflectCancel`；以新 Tracker 替换 `RegisterDeflectPress/GetDeflectWindow` 的旧主动计数实现。

保留以下机制：

- `IsLastHit` 与 `AttackUninterruptible`：决定 Boss 是否被弹断，不是玩家是否有资格弹反。
- `IsBoatFinalPulseParry`：飞舟末刀双方特殊表现与硬直，不是抖刀豁免。
- Boss `TryPassiveDeflect`、`KengekiArmed` 与反击时间安排。
- 箭 Heavy 的动画锁定和取消开放时刻；锁定期间未实际开启窗口的防御请求不记为空按。
- 普通格挡架势结算、连续被弹计数、忍杀入口、命中事件和效果。
- Hitbox/ArrowProjectile → CombatManager/CombatResolver → ReceiveHit 链，不在攻击框内结算目标伤害。

主动作成功记账必须先于特殊成功分支切状态；被动入口不能误调用它。清罚不重置 Boss 连续被弹计数，这两种计数分别表达玩家操作失败和 Boss 被连续弹开。

## 10. 文件改动地图与实施顺序

1. 输入与生命周期：修改 `Assets/Scripts/FrameWork/States/Command.cs`、`Assets/Scripts/Player/Brain/PlayerBrain.cs`、`Assets/Scripts/Player/Brain/BrainBase.cs` 和 `Assets/Scripts/FrameWork/Body/CharacterBody.cs`，加入防御身份、独立释放、防御缓冲清理和明确输入来源。通用缓冲仅增加必要的定向清理能力。
2. 结果记账：新增 `DeflectAttemptTracker.cs`；修改 `DeflectMemory.cs`，迁出主动惩罚；通过 CharacterBody 封装开始/到期/成功/取消/重置。所有退出原因必须显式映射，重入不重复失败。
3. 防御判定统一：修改 `DeflectState.cs`，使用唯一时间窗口，实施短点按保留窗口；所有成功分支共用记账；删除动画放行和 spam 限制，保留表现与被动流程。
4. 清理特例与参数迁移：修改 `HitReactionUtil.cs`、`Assets/Scripts/SO/CharacterConfig.cs`、`Assets/Editor/ArpgValidationRules.cs` 及相关 Player/Boss 配置资源。使用字段迁移属性或显式序列化迁移保留旧数值；同步字段注释和 Inspector 说明。资产修改遵循 SerializedObject/SerializedProperty 与 Undo，不批量覆盖无关参数。
5. 审查所有 `DeflectCommand` 构造点与复战/停用入口；核对系统回防、UI 输入和强制状态切换不会伪造新尝试。核对死亡、复战和暂停清理不会遗留缓冲或按住状态。
6. 实现完成后更新 `Docs/architecture/01-states.md`、`Docs/architecture/05-input-lockon.md`、`Docs/architecture/02-combat-data.md` 中受影响的实现说明；本设计继续保留提案/实施记录身份，不直接充当当前规范。

实施前以 CodeGraph 重新核对当前文件和调用方。当前工作区已有多处未提交修改，包括 DeflectState；分支保留这些修改，实施不能用旧版本覆盖或把无关改动混入提交。

## 11. 验证策略与风险

本次交付仅为文档；未执行编译、测试或场景验证。后续实现默认不新增自动化测试，遵循项目约定。可运行已有 `Assets/Tests/Editor/CharacterBodyHfsmFlagTests.cs` 检查状态标志回归，但它不能证明空按记账正确。

实现验证应同时覆盖纯记账逻辑和真实输入/命中链：确定时间与 PressId 的诊断序列确认一次结算、成功恢复、截止边界、重入与缓冲重试；Unity 中确认长按、快速点按、准确连续弹反、连续空按、飞舟末刀、箭 Heavy、危字、架势崩解自动回防和复战行为。结果必须分别标明实际执行、仅源码检查或未验证，验证记录写入实施报告，不在活跃实现文档维护手动验收清单。

关键风险及控制：

- 去掉 `inBegin` 后有效窗口可能变短：先沿用 0.5 秒，不同时调整动画速度、Boss 攻击时间或架势数值，运行观察后再单独讨论调参。
- 一次窗口仍可弹多刀：这是明确保留的行为；成功清罚也可能让有命中的乱按恢复，需要靠实际手感判断是否需要额外规则，不在本次偷偷加冷却。
- 短点按窗口保留改变收刀时机：要检查收刀延迟和表现衔接，不允许动画继续而判定已过期却仍能弹反。
- 快速按下/释放、缓冲覆盖和旧 ID 重试：由边沿身份与身体接受确认消除重复记账，不全局重写命令系统。
- 强制切换与特殊成功路径：结果通知先于切换；OnExit 只做幂等兜底，不一律把离开视为空按。
- 参数改名可能丢序列化值：显式迁移并核对资源，不只改 C# 字段名。

## 12. 完成定义

代码中只有一个主动弹反资格入口和一个空按结算入口；成功弹反清罚，空按才加罚；长按不自动续窗；物理按下与系统回防可区分；没有 Boss 招式白名单参与玩家弹反资格；动画不能越过截止时间；特殊招式结算与 Boss 被动防御保留现有职责。

只有完成实际编译与可用运行验证后，才能将方案标为已实现并说明验证范围。本次新分支和设计文档不代表运行行为已经改变。

---

## 13. 实施记录（2026-10-15）

本节是实施与验证记录，不属于当前规范；当前实现说明以 `Docs/architecture/01-states.md`、`02-combat-data.md`、`05-input-lockon.md` 与源码为准。

### 13.1 实际改动

按第 10 节的顺序落地：

| 步骤 | 文件 | 改动 |
|---|---|---|
| 1 输入与生命周期 | `FrameWork/States/Command.cs` | `DeflectCommand` 增加 `PressId` 与 `DeflectCommandSource`；`Physical(pressId)` / `System()` 显式构造；默认构造 = 系统来源 |
| | `Player/Brain/PlayerBrain.cs` | 按下申请 PressId 并缓冲带身份命令；松开改调 `NotifyDeflectReleased`，不再缓冲 `IdleCommand`；暂停 / 停用 / 结算 / 开场屏蔽时清按住状态并定向丢弃防御命令；崩解自动回防改系统来源 |
| | `Player/Brain/BrainBase.cs` | 增加 `HasBufferedCommand<T>` / `DropBufferedCommand<T>`；通用单槽缓冲本身不变，只多了定向清理能力 |
| | `FrameWork/Body/CharacterBody.cs` | 增加输入身份、开始 / 到期 / 资格 / 成功 / 结束 / 复位门面；`TryChangeToDeflectOrDodge` 透传 pressId；`ResetForEncounter` 与 `EnterDead` 复位；`ReceiveHit` 在伤害真正落实后按失败结束尝试 |
| 2 结果记账 | `FrameWork/Body/DeflectAttemptTracker.cs`（新增） | 按下身份、尝试生命周期、唯一窗口判定、空按记账与缩窗公式；参数为轻量快照；非法值夹紧；可选 Trace 调试钩子 |
| | `FrameWork/Body/DeflectMemory.cs` | 迁出 `RegisterDeflectPress` / `GetDeflectWindow` / `LastDeflectCancelTime` / `NotifyDeflectCancel`；保留 Boss 被动防御与连续被弹计数 |
| 3 防御判定统一 | `FrameWork/States/Ground/DeflectState.cs` | 唯一窗口判定；短点按保留窗口；成功记账前置于所有特殊分支；删除 `inBegin` 放行、`raiseSpamGuard`、多段特例调用；系统回防只恢复普通格挡；垫步与箭 Heavy 取消显式结束尝试 |
| 4 清理与参数迁移 | `Combat/HitReactionUtil.cs` | 删除 `IsMultiHitParryException`（确认无其他调用方） |
| | `SO/CharacterConfig.cs`、`SO/PlayerConfig.asset`、`SO/BossConfig.asset` | `DeflectMashWindow` → `DeflectEmptyResetInterval`、`DeflectMashPenalty` → `DeflectEmptyPenalty`（`FormerlySerializedAs` 保旧值）；`DeflectMashLimit` → `DeflectEmptyGraceCount`（float → int）；注释与 Tooltip 同步 |
| | `Editor/ArpgValidationRules.cs` | 新增 CharacterConfig 弹反窗口 / 空按参数校验，与窗口和构建校验共用 |
| | `IdleState`、`MoveState`、`StandingState`、`MidToGuardState`、`StaggerBrokenState`、`StunnedState`、`AttackState(Base)` | 把触发切换的 DeflectCommand 身份一路透传到防御状态入口；`MidToGuardState` 改用释放记录判断松手 |
| | `UI/CombatUIController.cs` | 复生选择改显式 `DeflectCommand.System()` |
| 6 文档 | `Docs/architecture/01-states.md`、`05-input-lockon.md`、`02-combat-data.md` | 同步受影响的实现说明 |

### 13.2 与提案的差异

1. `EndAttempt` 保留 `now` 参数，但只写进调试追踪：空按记账刻意不看结束时刻（第 6 节要求按"相邻两次实际开启尝试的间隔"判断恢复）。
2. `Reset()` / `ClearInput()` 内部读 `Time.time`，仅用于调试时间戳；判定路径的时间一律由调用方传入。
3. 删除 `guardStable`：全文件没有任何读取点，已无表现用途，按第 9 节"依据其剩余表现用途决定"删除。
4. 崩解自动回防增加"缓冲里已有防御指令就不重发系统请求"的判断。否则 PlayerBrain 每帧重发的系统请求会顶掉玩家真实按下的命令，第 7.3 节"真实的新按下仍在有效缓冲内则实际接受它时开启一次窗口"无法成立。
5. `MidToGuardState` 记住最近一次物理按下的 PressId（旧实现用 `released` 标志位，依赖 `IdleCommand`），起身播完时按它开窗口。
6. `IdleCommand` 类型保留但已无生产者：松手改走释放记录。删除它属于命令词汇表的额外改动，不在本次范围。
7. 两个 CharacterConfig 资源的字段改名是直接改资源 YAML 文本（本次没有开 Unity 编辑器会话，第 10 节建议的 SerializedObject / SerializedProperty 路径在无编辑器时不可用）。写入的字段名逐条与 C# 字段比对过，取值与迁移前一致，导入无报错；资源里的字段顺序与 Inspector 显示顺序可能不同，Unity 下次保存资源时会自己重排。

### 13.3 验证记录

**已实际执行：**

- `dotnet build Assembly-CSharp.csproj` / `Assembly-CSharp-Editor.csproj`：0 warning、0 error。
- Unity 2022.3.62f3 batchmode 导入 + 编译（日志 `Temp/deflect-compile-check.log`）：无 `error CS`、无 `warning CS`、无异常；新脚本已导入并生成 `.meta`；产物 `Library/ScriptAssemblies/Assembly-CSharp.dll` 含 `DeflectAttemptTracker` 与 `DeflectEmptyGraceCount`，已不含 `DeflectMashLimit` 与 `IsMultiHitParryException`。
- 现有 EditMode 测试 `ARPG.Tests.Editor.CharacterBodyHfsmFlagTests`（结果 `Temp/deflect-editmode-tests.xml`）：3/3 Passed。它只证明状态标志没有回归，**不能**证明空按记账正确。
- 两个 CharacterConfig 资源的序列化字段名逐条与 C# 字段比对：本次新增 / 改名字段全部可定位。（顺带发现 `SweepStompRadius` / `SweepStompHeight` 是本次之前就存在的死字段，代码里已无引用，本次未动。）

**仅源码检查：** 第 4～9 节规则的逐条对照。

**用户验收：** 2026-10-15 用户在 Unity 内验收，反馈"基本没啥问题"。本节不逐条代勾 13.4 的清单——哪些条目实际跑过以用户自己为准，上表复选框留空即表示没有书面记录的证据。

### 13.4 待人工验证清单（Unity 内，需在 Play 模式逐条确认）

调试日志开关：CharacterBody 的 `EnableStateDebugLog`。打开后每次记账会打印
`[CombatTiming][DeflectAttempt] 开启 attempt=… pressId=… 窗口=… 连续空按=…` 与
`结束 attempt=… 原因=… 记一次空按/不记空按`，进防御时另有 `[CombatTiming][DeflectEnter]`。

| # | 操作步骤 | 预期结果 | 通过 |
|---|---------|---------|------|
| 1 | 长按防御键 2 秒不放 | 只出现一次 `开启 attempt`；窗口到期后仍举刀格挡，不自动续窗；松开后播收刀回待机 | ☐ |
| 2 | 快速点按（0.1 秒内松开），Boss 在窗口内砍到 | 仍是完美弹反（短点按保留窗口） | ☐ |
| 3 | 快速点按且期间没被打到 | 窗口到期后直接回待机，不播收刀动画 | ☐ |
| 4 | 连续 4 次空按，每次间隔小于恢复间隔 | 每次 `开启` 打印的窗口都是 0.5 秒（宽容次数内不缩窗） | ☐ |
| 5 | 紧接第 5、6 次空按 | 第 5 次窗口 0.375 秒，第 6 次 0.28125 秒 | ☐ |
| 6 | 空按后停手超过恢复间隔再按下 | 连续空按清零，窗口回到 0.5 秒 | ☐ |
| 7 | 准确连续弹反 5 刀 | 每次都是完美弹反，连续空按始终为 0，窗口一直是 0.5 秒 | ☐ |
| 8 | 一次窗口内被同一多段招连砍两刀（如飞舟） | 两刀都能弹开（一窗多刀保留） | ☐ |
| 9 | 窗口内按垫步取消 | 记一次空按，之后不再有残留窗口 | ☐ |
| 10 | 窗口外被危字突刺/抓取命中 | 不弹开、正常受击，并记一次空按 | ☐ |
| 11 | 箭 Heavy 动画锁定期间按防御（未到开放时刻） | 命令不被消费、不记空按；到开放时刻仍按着才进防御 | ☐ |
| 12 | 架势被打崩倒地期间长按防御键 | 恢复后是普通格挡：`DeflectEnter` 的 `attempt=0`，无窗口、无空按；此后真实按下一次才开窗口 | ☐ |
| 13 | 死亡后复战 | 连续空按清零、无残留窗口，复战第一刀不吃缩窗 | ☐ |
| 14 | 弹反成功后立刻再按防御 | 新窗口回到 0.5 秒（成功已清罚） | ☐ |
| 15 | 飞舟末刀互弹、弹崩 Boss 转忍杀准备 | 日志里出现 `成功 attempt=…` 且连续空按清零（特殊分支也不丢记账） | ☐ |
| 16 | 暂停 / 胜利结算期间按住防御键，之后恢复操作 | 不凭空开启窗口，需要新的物理按下 | ☐ |
| 17 | 弹反动画还在播、但窗口已过期时再挨一刀 | 只普通格挡，不再吃完美弹反判定（动画不能延长窗口） | ☐ |
| 18 | 走路中按下防御并持续移动 | 仍能弹反（融合抬刀不再影响资格），走动不会打断窗口 | ☐ |
| 19 | 操作确认 | 主动弹反不再受 Boss 招式白名单影响：对 Boat / Boat_Full / Bow_Air5 / Kengeki_Air5 的弹反资格与其它招式一致 | ☐ |

用户已验收，方案可标为已实现；上表复选框留空只表示没有逐条书面记录，不代表条目未经验证。
