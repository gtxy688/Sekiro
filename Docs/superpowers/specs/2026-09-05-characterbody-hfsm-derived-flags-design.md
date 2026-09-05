# CharacterBody HFSM 派生状态标志

## 目标

移除 `IsAttacking` 作为可写快照的双重状态来源，使其由 CharacterBody 当前 HFSM 叶子状态派生；外部模块继续只读取 CharacterBody 的语义接口，不依赖具体状态类型。

## 本次范围

- 新增空语义标记 `IAttackingState`，由 `AttackStateBase` 实现，因此 `AttackState` 与 `AirAttackState` 自动属于攻击类叶子。
- CharacterBody 新增私有的任意层级叶子查询，并将 `IsAttacking` 改为只读派生属性。
- 删除所有对 `IsAttacking` 的写入；强制弹反、识破、崩解、死亡、忍杀、投技、复战和 Boss 后撤仍各自负责切换状态、关闭判定、清理攻击阶段数据。
- 添加 EditMode 回归测试，覆盖状态机在攻击叶子退出期间与切换完成后的可观察语义，以及公开 API 不再允许外部写入。

## 保留范围

- `IsGuarding`、`IsHealing`、`IsParried` 不在本次改动中，需各自完成独立语义审计。
- `IsAttackRecoveryOpen` 与 `AttackUninterruptible` 保留为攻击状态内部阶段数据；其 owner 仍是 `AttackStateBase`。
- 不改动 Command 路由、输入缓冲、攻击窗口、命中判定、动画、CombatStats 的数值规则或 HFSM 拓扑。

## 设计

`CharacterBody` 是外部模块唯一可见的 Facade。它遍历 `MainStateMachine.CurrentState` 与嵌套 `HierarchicalState.SubStateMachine.CurrentState`，取得当前叶子后判断是否实现 `IAttackingState`。

状态机的既有生命周期是“旧状态 `OnExit` → 替换 `CurrentState` → 新状态 `OnEnter`”。因此在旧攻击叶子的 `OnExit` 执行期间，`IsAttacking` 仍返回 `true`；状态切换完成后才返回新叶子的语义。这与“当前状态”的实际所有权一致，且测试会锁定该契约。

无效攻击配置会在 `AttackStateBase.OnEnter` 中立即切回退出目标。它在这次重构后可能短暂属于攻击叶子，但不会对外发出状态变更通知；最终可观察状态仍由完成后的叶子决定。

## 验收

- `IsAttacking` 是无 setter 的只读属性。
- 任意攻击叶子退出期间仍被识别为攻击；切到非攻击叶子后立即为 false。
- 项目脚本中不存在对 `IsAttacking` 的赋值。
- 现有强制打断路径仍保留关闭武器判定、清理攻击窗口和切换正确 HFSM 状态的行为。
