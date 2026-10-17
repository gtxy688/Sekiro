# 04 Boss 行为树与选招

> 现状依据：Boss/BehaviourTree、BossMovePicker 与 BossMoveTable。

## 职责划分

BTBrain 组装并更新 Boss 行为树。Node 返回 Success、Failure 或 Running；部分 Selector / 节点保存运行状态，Blackboard 保存跨节点使用的战斗上下文与冷却信息。

PlayerTarget、PlayerBody 和招式表由场景绑定，PlayerBody 不再从目标层级自动补齐。树建立后的角色、配置、黑板和执行节点按必需依赖使用；无目标、无可选招式、冷却未结束等仍是正常的决策分支。

行为树负责判断“现在做什么”；BossMovePicker 按当前调用层、目标距离、招式可用性等条件选择 BossMoveEntry；BT_ExecuteMove 将所选招式的序列 / 窗口交给 CharacterBody 的攻击状态执行。

## 当前行为分支

源码包含主动选招、交锋反击、喝药惩罚等行为节点，并含追击 / 位移与目标状态处理。具体分支顺序以 BTBrain 当前组装的树为准；不要把历史计划中的树结构、权重或待办状态直接当成现状。

## 招式数据

BossMoveTable 持有招式表数据。BossMoveEntry 表达招式层、距离区间、权重、冷却、动画序列和默认战斗数据；BossMoveWindow 描述各段时间与覆盖值；hitPulses、arrowCues 等描述段内事件。

BT_ExecuteMove 会为当前动画段构造运行时 AttackConfig，并由 AttackStateBase 按动画时间执行窗口。Boss 招式不通过 PlayerBrain 的输入缓冲生成。

## 维护边界

Boss 被动完美弹反玩家后，DeflectState 保存交锋首段，读取玩家当前 ParriedState 预计恢复时刻：最小硬直下限与动画达到 95% 的预计时刻取较晚者。单 Clip 的剩余动画时间按当前进度、Clip 长度和播放速度计算；动画尚未进入、停播或混合树无法预测时，等待可预测或玩家真实恢复，不写死 0.602 秒。

反击起手时刻 = 预计恢复时刻 + BossConfig.ParryCounterHitDelay - 首段 HitStartTime。当前 Boss 延迟为 0.1 秒，玩家 ParriedDuration 仍为 0.35 秒。等待阶段不会因弹反动画播完提前收刀，BT_ExecuteMove 也会等待反击真正进入攻击再判断段落完成。该安排只给可及时起手的单段近战提供抢先余量；前摇过长、先位移或先射箭的多段招式，以及距离和命中窗口变化，不保证统一时刻命中。

不要只看招式表中的 layer 字段就假设每个层都由同一个选择器调用。具体有哪些层会运行，由 BTBrain 与各节点的实际调用点决定；特殊中断也可能由执行节点直接处理。

## 玩家完美弹反后的普通第一刀先手

仅当玩家 Normal 模式近战完美弹反真正把 Boss 弹入 ParriedState 时，记录固定的标准反击时间窗口：玩家标准第一刀时刻 P = 弹反时刻 + BossConfig.PlayerParryAttackPreparationTime + 玩家 LightAttack 首个有效 hitPulse.start；Boss 最早攻击判定 H = P + BossConfig.PlayerParryAttackHitBuffer。当前配置分别为 0.7s 与 0.1s，第一刀判定为 0.23s 时，H 为弹反后 1.03s。0.7s 是输入与收刀的设计预算，参考手动日志，不是自动测量，也不保证玩家迟于预算反击仍有先手。

BT_ExecuteMove 为所选招式首段计算最早起手 H - 首个有效近战判定/出箭时刻。当前时刻尚未到点则保留已选序列并等待，到点后走原攻击入口；恢复硬直、原有冷却、格挡或其他状态可能进一步推迟起手，不强迫 Boss 在恢复瞬间出招。等待期间继续走位，被动格挡保持原流程；普通格挡姿态未退出时不吞掉预选攻击，Boss 若弹回玩家则退出旧等待、交还交锋层。

首段只有位移、没有攻击事件的招式保守按前摇 0 计算，等 H 后再开始，避免用虚假的 NoHit.HitStartTime 倒推。保护窗口在派发下一次攻击、Boss 弹回玩家或复战重置时清除；不跟踪玩家实际输入和连招。玩家弹反箭矢、不可打断招式或多段中间刀，以及飞舟互弹、识破、架势崩解不武装此窗口。本规则约束正常播放速度下的判定/出箭时间先后，不保证空间接触或伤害结果。

## 核心源码

- Boss/BTBrain.cs
- Boss/BehaviourTree/BT/BT_PickActive.cs
- Boss/BehaviourTree/BT/BT_Kengeki.cs
- Boss/BehaviourTree/BT/BT_HealPunish.cs
- Boss/BehaviourTree/BT/BT_ExecuteMove.cs
- Boss/BossMovePicker.cs、Boss/BossMoveTable.cs、Boss/GenichiroMoveCatalog.cs
