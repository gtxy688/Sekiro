# Command 与输入缓冲

这一篇回答：玩家按下一次攻击后，这个意图为什么没有直接控制动画？

## 1. 一次攻击的调用链

```text
Input System
→ PlayerBrain.OnAttackCanceled
→ 创建 AttackCommand
→ BrainBase.BufferCommand
→ 每帧尝试 CharacterBody.TryExecuteCommand
→ MainStateMachine.HandleCommand
→ GroundedState 向子状态转发
→ IdleState.HandleCommand
→ 切换到 AttackState
```

输入层只负责产生请求，最终是否攻击由当前状态决定。

## 2. 项目中的 Command 是什么

`ICommand` 是空接口，`AttackCommand`、`DeflectCommand`、`DodgeCommand` 等是很轻的值类型。

它不是完整的 GoF Command，因为没有 `Execute()`、`Undo()` 或历史记录。更准确的定义是：

> Command 是角色意图的类型化消息，描述“想做什么”，由当前状态决定“能不能做、怎么做”。

这层抽象避免 `PlayerBrain` 直接操作 Animator、Hitbox 和角色状态，也让玩家输入和 Boss AI 可以使用同一种行为语言。

## 3. 输入缓冲如何工作

`BrainBase` 只保存：

```csharp
private ICommand bufferedCommand;
private float bufferTimer;
```

默认缓冲时间为 0.2 秒。

```text
玩家提前按攻击
→ 当前状态尚未开放连招窗口
→ HandleCommand 返回 false
→ 命令留在缓冲中
→ 下一帧再次尝试
→ 连招窗口开放
→ 当前 AttackState 接受命令
→ 清空缓冲
```

返回值的含义：

- `true`：命令已经被处理，应从缓冲中移除。
- `false`：当前暂时不能处理，可以在剩余时间内重试。
- 超过缓冲时间仍未处理：丢弃。

`true` 不等于一定切换状态。状态也可以吞掉一条无效命令，避免它持续重试。

## 4. 单槽缓冲，不是命令队列

新命令会覆盖旧命令：

```text
AttackCommand 已缓冲
→ 玩家随后输入 DodgeCommand
→ DodgeCommand 覆盖 AttackCommand
```

它表达“最新离散意图优先”，实现简单，适合当前 Demo；但不适合必须完整保存搓招序列的格斗游戏。

移动是连续意图，每帧都会更新，因此 `MoveCommand` 不需要使用这套 0.2 秒离散输入缓冲。

## 5. 同一个命令由状态解释

`AttackCommand` 到达不同状态时结果不同：

- `IdleState`：进入第一段攻击。
- `MoveState`：停止移动并进入攻击。
- `AttackState`：窗口开放时接下一段，否则返回 `false`。
- `StunnedState`：拒绝或吞掉攻击。
- `GroundedState`：如果 Boss 已崩解，优先尝试忍杀。

这就是状态模式的核心：行为不仅取决于输入，也取决于接收输入时对象所处的状态。

## 6. 代价

- 调用链比直接调用更长。
- 需要追踪究竟由哪个状态消费命令。
- 单槽会覆盖上一条意图。
- `struct` Command 转为 `ICommand` 时会装箱，转换可能发生在传参时，不能只看字段赋值。`PlayerBrain.Update` 每帧发送 `MoveCommand`，因此不能用“离散输入低频”概括整个命令链路。实际分配与耗时应在目标构建中测量，尚不能宣称影响可忽略。

## 源码精读与边界题

入口：[PlayerBrain.cs](../../Assets/Scripts/Player/Brain/PlayerBrain.cs) 的 `OnAttackCanceled / Update`、[BrainBase.cs](../../Assets/Scripts/Player/Brain/BrainBase.cs) 的 `BufferCommand / ProcessCommandBuffer`。

- 普通攻击在松开攻击键时产生；长按另由 `ProcessHeldAttack` 处理，不能说成“按下就立即普攻”。
- 缓冲按 `Time.deltaTime` 计时，且只在拒收分支扣时间。暂停时 `PlayerBrain.Update` 提前返回；0.2 秒不是始终流逝的现实时间。
- 当前顺序是“先尝试消费，失败后扣时间并判断超时”。临界帧不能直接套用“先超时、后执行”的伪代码。
- 缓冲重试传递的是已有接口引用，不代表每重试一次就把同一个命令重新装箱；应定位最初的值类型到接口转换。

**闭卷练习**：画出攻击被闪避覆盖、硬直结束前成功消费、连续拒收后超时三条时间线，标出每帧返回值。再解释为何 `MoveCommand` 不入缓冲仍可能分配。语言规则参见 [Microsoft：装箱与拆箱](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/types/boxing-and-unboxing)，测量步骤见 [16](16-性能验证与故障排查实战.md)。

## 面试回答

> Command 把玩家输入和 AI 决策统一成角色意图，当前状态决定是否消费。未被接受的离散命令会保留约 0.2 秒，形成输入缓冲。输入层因此不直接依赖动画和战斗实现，玩家与 AI 可以复用执行逻辑。

## 面试追问梯度

### 基础概念

1. 经典 Command 模式通常包含什么？你的实现为什么没有 `Execute()`？
2. 值类型 Command 存进接口字段时会发生什么？

### 项目实现

3. `HandleCommand` 的布尔返回值怎样驱动输入缓冲？
4. 连招窗口尚未开放时，AttackCommand 为什么不会立即丢失？

### 方案取舍

5. 为什么用单槽而不是队列？
6. 为什么 MoveCommand 不和 AttackCommand 使用同样的缓冲策略？

### 扩展设计

7. 如果要支持搓招、优先级和输入序列，缓冲结构怎样升级？
8. 如果做网络回放，Command 是否可以作为输入记录？

### 故障排查

9. 玩家偶尔按攻击没有反应，如何区分输入没产生、命令被覆盖、状态拒绝和窗口超时？

回答时要主动承认：这是“类型化意图消息”，不是完整 GoF Command；单槽覆盖是当前 Demo 的简化。

## 自测

1. 为什么 `AttackCommand` 没有 `Execute()`？
2. `HandleCommand` 返回 `false` 为什么不表示程序错误？
3. 为什么移动不进入 0.2 秒缓冲？
4. 单槽缓冲与命令队列的差别是什么？
