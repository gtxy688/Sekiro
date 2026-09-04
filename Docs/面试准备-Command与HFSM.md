# Command + HFSM：统一玩家输入与 Boss AI 的行为执行

> 这篇文档只回答一个问题：玩家和 Boss 的“脑子”完全不同，为什么最后可以复用同一套角色执行框架？

## 一、先记住简历上的一句话

**基于 Command + HFSM 统一玩家输入与 Boss AI 的行为执行：玩家输入与行为树只负责产生意图，`CharacterBody + HFSM` 负责判断意图当前能否执行，并完成状态切换。**

这句话里有三个边界：

1. **玩家输入和 Boss AI 没有被统一。**它们是两种不同的决策来源。
2. **被统一的是行为执行入口。**移动、攻击等意图最终交给 `CharacterBody` 和状态机处理。
3. **共用的是框架，不是角色数据。**玩家和 Boss 各自持有配置、运行时状态和动画参数。

## 二、五个组件各自负责什么

### 1. Brain：决定“想做什么”

- `PlayerBrain` 读取 Input System，产生攻击、弹反、闪避、移动等意图。
- `BTBrain` 驱动行为树，根据距离、冷却、权重和战斗状态选择 Boss 行为。

Brain 不应该直接负责攻击动画、伤害判定或状态生命周期。

### 2. Command：表达“我想做什么”

项目中的 `AttackCommand`、`MoveCommand`、`DeflectCommand`、`DodgeCommand` 等，是类型化的行为意图。

这里的 Command 更接近“命令消息”，而不是完整的经典 GoF Command：它没有 `Execute()`、撤销和历史记录，只负责把请求来源与执行状态解耦。面试时主动说明这一点，比硬套设计模式更准确。

### 3. CharacterBody：统一执行门面

`CharacterBody.TryExecuteCommand` 是正常行为请求的统一入口。它先处理角色级锁定，再把命令交给主状态机。

外部系统不需要知道当前状态机嵌套了几层，也不应该直接操纵某个叶子状态。

### 4. HFSM：决定“现在能不能做、由谁处理”

主状态机保存顶层父状态：

- `GroundedState`
- `AirState`
- `StunnedState`
- `DeadState`

父状态内部再保存叶子状态。例如 `GroundedState` 的子状态机中可以是：

- `IdleState`
- `MoveState`
- `AttackState`
- `DeflectState`
- `DodgeState`
- `HealState`
- `MikiriCounterState`

状态机不是看到命令就一定执行，而是把决定权交给当前状态。命令被接受时返回 `true`，当前状态拒绝时返回 `false`。

### 5. 具体状态：完成行为生命周期

状态负责进入、更新、退出，以及动画切换、位移和行为窗口等执行细节。例如 `IdleState` 收到 `AttackCommand` 后切入 `AttackState`；攻击状态负责后续攻击生命周期。

## 三、玩家的一次攻击如何走完整条链路

```text
玩家按下攻击键
    ↓
PlayerBrain 生成 AttackCommand
    ↓
BrainBase 暂存命令（默认 0.2 秒）
    ↓
CharacterBody.TryExecuteCommand
    ↓
MainStateMachine.HandleCommand
    ↓
顶层父状态先决定是否拦截
    ↓
当前叶子状态处理命令
    ↓
IdleState / MoveState 切入 AttackState
    ↓
AttackState 执行动画与攻击生命周期
```

这里最关键的不是“按键切到了攻击状态”，而是**当前状态有权拒绝命令**。例如攻击命中段不允许被某些动作取消时，命令不会立刻执行，而是暂时留在输入缓冲中。

`BrainBase` 当前采用单槽缓冲：新命令会覆盖旧命令；状态机返回 `true` 就清空，返回 `false` 就在剩余窗口内继续尝试。这种实现简单、适合 Demo，但不是多命令队列。

移动是连续意图，不走这套 0.2 秒离散输入缓冲；`PlayerBrain` 每帧直接发送 `MoveCommand`。

## 四、Boss 如何接入同一个执行层

Boss 与玩家的区别主要在决策来源：

```text
玩家：Input System → PlayerBrain ─┐
                                  ├→ Command / CharacterBody → HFSM → 行为执行
Boss：距离/冷却/权重 → 行为树 ────┘
```

具体有两类常见行为：

- `BT_MoveToTarget` 把接近目标的方向包装为 `MoveCommand`，再调用 `TryExecuteCommand`。
- `BT_ExecuteMove` 选定招式并生成运行时 `AttackConfig`，再通过 `StartAttack` 进入攻击执行链；首段通常转成 `AttackCommand` 交给状态机，连续招式后续段可在受控条件下直接切入 `AttackState`，避免多段动画衔接经过 Idle。

所以更严谨的表述不是“Boss 的所有行为都必须先变成 Command”，而是：

**玩家输入与 Boss AI 复用同一个 `CharacterBody + HFSM` 执行层；常规意图优先通过 Command 路由，明确的强制流程通过语义化状态切换入口完成。**

这也解释了行为树和状态机的分工：

- 行为树回答“下一步选哪一招”。
- HFSM 回答“角色现在处于什么行为，以及这个请求能否执行”。
- Command 是两者之间常用的意图协议。

## 五、命令为什么要先经过父状态

`HierarchicalState.HandleCommand` 的顺序是：

1. 父状态执行 `OnParentHandleCommand`。
2. 父状态未拦截，再交给当前子状态。

父状态负责跨子状态都成立的规则。例如角色处于处决锁定时，`GroundedState` 可以统一吞掉命令，不必让 Idle、Move、Attack、Dodge 等每个叶子状态重复判断。

子状态负责局部执行。例如 Idle 和 Move 都可以接受攻击命令并切入 Attack，但攻击状态可以根据自身阶段决定是否接受下一段输入。

一句话概括：**父状态是守门员，叶子状态是当前行为的执行者。**

## 六、为什么不能从主状态机直接判断 DodgeState

这不是 `DodgeState` 的特殊问题，而是所有叶子状态的共同规则。

角色闪避时，真实层级是：

```text
MainStateMachine.CurrentState = GroundedState
GroundedState.SubStateMachine.CurrentState = DodgeState
```

因此下面这些判断都会失败：

```csharp
body.MainStateMachine.CurrentState is IdleState
body.MainStateMachine.CurrentState is MoveState
body.MainStateMachine.CurrentState is DodgeState
```

我之前单独用 `DodgeState` 举例，是因为它最容易造成实际战斗 Bug：闪避涉及无敌帧，命中系统很容易想当然地查询“当前是不是 DodgeState”；查询错层后，表现就是角色明明在闪避却仍然受伤。

`IdleState` 在结构上完全一样，只是外部系统通常没有必要查询它。待机、移动和攻击的切换大多由命令路由在状态机内部完成，所以它不如闪避能直观暴露这个代价。

正确做法分两类：

- 真有查询需求时，走 `CharacterBody.IsInGroundedSubState<T>()` 等封装接口。
- 能用行为协议解决时，不查询具体类型。例如受击通过 `OnHitReceived` 从父状态路由到当前子状态，让 `DodgeState` 或 `DeflectState` 自己回答是否处理本次命中。

第二种通常更好，因为外部模块依赖的是“这次命中是否被处理”，而不是状态机内部恰好用了哪个类。

## 七、为什么还需要直接状态切换

Command 表示角色的行为请求，因此允许被拒绝；有些事件不是请求，不能等缓冲窗口：

- 受击与硬直
- 死亡
- 离地与落地
- 忍杀、投技等双方同步的强制演出
- Boss 连招中已经确认执行的后续段

这些流程通过 `CharacterBody` 的语义化入口或当前父状态直接切换。设计原则是：

**输入和 AI 意图走可拒绝的 Command；物理结果与强制演出走不可等待的状态迁移。**

如果面试时说“所有切换都走 Command”，很容易被受击、死亡和处决追问击穿。

## 八、这个架构解决了什么问题

### 决策来源可替换

玩家输入、行为树，甚至以后接入网络回放或训练 AI，都可以产生相同的意图，执行层不需要跟着重写。

### 行为规则集中

能否攻击、何时能取消、处决期间是否锁定，由角色状态决定，而不是散落在输入脚本和 AI 节点里。

### 玩家与 Boss 复用执行框架

双方复用状态生命周期、命令路由和 `CharacterBody` 能力接口，但保留各自配置与决策逻辑。

### 输入手感更稳定

短暂不能执行的离散输入可以在缓冲窗口内重试，减少玩家必须精准卡帧的问题。

## 九、必须主动承认的代价

### 调用链更长

一次输入经过 Brain、Command、CharacterBody、父状态和叶子状态，定位问题比直接调用方法更绕。因此项目给状态切换记录了完整状态路径与切换原因。

### 层级查询容易写错

主状态机只持有父状态，查询叶子必须进入子状态机或通过语义接口。这是 `DodgeState` 示例真正想表达的代价。

### 当前缓冲只是单槽

后来的输入会覆盖前一个未消费命令。Demo 中足够简单，但复杂连段可能需要按优先级或时间排序的队列。

### Command 不是所有流程的唯一通道

强制事件仍需直接状态迁移。统一执行层不等于机械地把所有东西包装成 Command。

## 十、面试回答模板

### 30 秒版本

“我的玩家和 Boss 决策来源不同：玩家由 Input System 产生输入，Boss 由行为树根据距离、冷却和权重选招。但两边最终都复用 `CharacterBody + HFSM` 执行层，常规移动和攻击意图通过 Command 路由，由当前父状态和叶子状态决定接受或拒绝。这样决策层不直接操作动画和判定，替换输入或 AI 时不用改执行逻辑。受击、死亡、处决这类不可拒绝的强制事件则走语义化状态切换，不硬塞进 Command。”

### 被追问“为什么同时用行为树和状态机”

“行为树负责选什么，状态机负责正在做什么以及现在能不能做。Command 是两者之间的意图协议。Boss 行为树选出招式后，把执行交给角色状态机；玩家则把输入意图交给同一个执行层。”

### 被追问“DodgeState 为什么判断不到”

“不是 Dodge 特殊，而是所有叶子状态都在 `GroundedState` 的子状态机里。主状态机当前值只会是 `GroundedState`，所以从顶层直接判断 Idle、Move、Dodge 都会失败。Dodge 只是最危险的例子，因为判断错会让无敌帧失效。项目里通过 `CharacterBody` 的封装查询，或让受击事件路由到当前子状态自行处理。”

## 十一、建议的源码阅读顺序

1. `Assets/Scripts/FrameWork/States/Command.cs`：先看意图有哪些。
2. `Assets/Scripts/Player/Brain/BrainBase.cs`：理解单槽输入缓冲和消费语义。
3. `Assets/Scripts/Player/Brain/PlayerBrain.cs`：看玩家输入怎样产生 Command。
4. `Assets/Scripts/FrameWork/Body/CharacterBody.cs`：看统一入口和语义化切换门面。
5. `Assets/Scripts/FrameWork/States/StateMachine.cs`：看状态机最小职责。
6. `Assets/Scripts/FrameWork/States/Base/HierarchicalState.cs`：看父状态优先、子状态后处理的路由。
7. `Assets/Scripts/FrameWork/States/Ground/GroundedState.cs`：看父状态怎样集中处理共性规则。
8. `Assets/Scripts/FrameWork/States/Ground/IdleState.cs` 与 `MoveState.cs`：看叶子状态怎样消费命令。
9. `Assets/Scripts/Boss/BTBrain.cs`、`BT_MoveToTarget.cs`、`BT_ExecuteMove.cs`：最后看 Boss 决策如何接入执行层。

读完后应当能不看文档回答四个问题：

1. `TryExecuteCommand` 返回 `false` 对输入缓冲意味着什么？
2. 为什么移动命令不使用 0.2 秒缓冲？
3. 为什么 Boss 连招后续段不一定再次发送 `AttackCommand`？
4. 为什么受击、死亡和忍杀不应该等待 Command 被当前状态接受？
