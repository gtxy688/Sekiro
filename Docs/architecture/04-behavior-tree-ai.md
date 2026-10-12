# 04 Boss 行为树与选招

> 现状依据：Boss/BehaviourTree、BossMovePicker 与 BossMoveTable。手动验收见 04-behavior-tree-ai-test.md。

## 职责划分

BTBrain 组装并更新 Boss 行为树。Node 返回 Success、Failure 或 Running；部分 Selector / 节点保存运行状态，Blackboard 保存跨节点使用的战斗上下文与冷却信息。

行为树负责判断“现在做什么”；BossMovePicker 按当前调用层、目标距离、招式可用性等条件选择 BossMoveEntry；BT_ExecuteMove 将所选招式的序列 / 窗口交给 CharacterBody 的攻击状态执行。

## 当前行为分支

源码包含主动选招、交锋反击、喝药惩罚等行为节点，并含追击 / 位移与目标状态处理。具体分支顺序以 BTBrain 当前组装的树为准；不要把历史计划中的树结构、权重或待办状态直接当成现状。

## 招式数据

BossMoveTable 持有招式表数据。BossMoveEntry 表达招式层、距离区间、权重、冷却、动画序列和默认战斗数据；BossMoveWindow 描述各段时间与覆盖值；hitPulses、arrowCues 等描述段内事件。

BT_ExecuteMove 会为当前动画段构造运行时 AttackConfig，并由 AttackStateBase 按动画时间执行窗口。Boss 招式不通过 PlayerBrain 的输入缓冲生成。

## 维护边界

不要只看招式表中的 layer 字段就假设每个层都由同一个选择器调用。具体有哪些层会运行，由 BTBrain 与各节点的实际调用点决定；特殊中断也可能由执行节点直接处理。

## 核心源码

- Boss/BTBrain.cs
- Boss/BehaviourTree/BT/BT_PickActive.cs
- Boss/BehaviourTree/BT/BT_Kengeki.cs
- Boss/BehaviourTree/BT/BT_HealPunish.cs
- Boss/BehaviourTree/BT/BT_ExecuteMove.cs
- Boss/BossMovePicker.cs、Boss/BossMoveTable.cs、Boss/GenichiroMoveCatalog.cs
