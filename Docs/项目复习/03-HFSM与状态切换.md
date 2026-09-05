# HFSM 与状态切换

这一篇回答：角色为什么需要两层状态机，以及命令和受击如何到达真正的叶子状态。

## 1. 状态层级

```text
MainStateMachine
├── GroundedState
│   └── Idle / Move / Attack / Deflect / Dodge / Heal / Mikiri...
├── AirState
│   └── AirIdle / AirAttack
├── StunnedState
│   └── 具体受击状态
└── DeadState
    └── 回生等待 / 真正死亡
```

顶层表达角色所处的大类环境，叶子状态表达当前具体行为。

## 2. 为什么不是扁平 FSM

扁平 FSM 会把所有组合都放在同一级：

```text
GroundIdle / GroundMove / GroundAttack
AirIdle / AirAttack
GroundStunned / AirStunned...
```

状态越多，共同判断和状态连接越容易重复。HFSM 可以把公共规则放到父状态：

- `GroundedState` 统一处理跳跃、喝药、处决与地面离开。
- `AirState` 统一处理空中更新与落地。
- `DeadState` 统一屏蔽正常战斗行为。

## 3. 生命周期

`StateMachine.ChangeState` 只负责：

```text
旧状态 OnExit
→ 保存旧状态与调试路径
→ 新状态 OnEnter
→ 记录切换原因
```

`HierarchicalState` 进入时创建或切入默认子状态，更新时驱动子状态机，退出时清空当前子状态。

状态机框架不决定“应该从哪里切到哪里”；具体规则由当前状态或 `CharacterBody` 的语义化入口决定。

## 4. 命令如何路由

`HierarchicalState.HandleCommand` 的顺序：

```text
父状态 OnParentHandleCommand
→ 父状态未消费
→ 当前叶子状态 HandleCommand
```

父状态是守门员，叶子状态是当前行为的执行者。处决锁定等公共规则不需要复制到每个叶子状态。

## 5. 受击如何路由

受击也使用父状态优先、叶子状态随后处理：

```text
CharacterBody.ReceiveHit
→ 顶层状态 OnHitReceived
→ 父状态决定是否拦截
→ 当前子状态 OnHitReceived
→ 未拦截才进入普通伤害结算
```

因此 `DeflectState`、`DodgeState` 和 `MikiriCounterState` 即使藏在 `GroundedState` 内，也能参与命中判断。

## 6. Command 与直接切换的边界

Command 是可以被拒绝的行为请求，适合玩家输入和 AI 意图。

受击、死亡、离地、落地、处决和投技属于物理结果或强制演出，不能等待当前状态同意，因此通过 `CharacterBody` 的语义化入口直接切换。

可以概括为：

> 意图走 Command，事实走强制状态迁移。

## 7. 叶子状态查询陷阱

闪避时真实结构为：

```text
MainStateMachine.CurrentState = GroundedState
GroundedState.SubStateMachine.CurrentState = DodgeState
```

所以从主状态机直接判断 `IdleState`、`MoveState` 或 `DodgeState` 都会失败。`DodgeState` 只是最危险的例子，因为查错会让无敌帧判断失效。

需要查询时走 `CharacterBody.IsInGroundedSubState<T>()`。更好的方式是把请求路由给当前状态，让它自己回答，而不是外部猜测内部状态类型。

## 8. 代价与补偿

- 父子层级增加理解与调试成本。
- 状态切换不再是一眼可见的直接调用。
- 外部代码容易查错状态层级。

项目用完整状态路径、上一个状态、切换原因和切换次数补偿调试成本。

## 面试追问梯度

### 基础概念

1. FSM 和 HFSM 的区别是什么？
2. `OnEnter / OnUpdate / OnExit` 分别适合放什么逻辑？

### 项目实现

3. 父状态为什么先处理 Command 和 HitData？
4. GroundedState 退出时为什么必须清理子状态？

### 方案取舍

5. 哪些规则应该放父状态，哪些应该留在叶子状态？
6. 为什么强制受击不走可拒绝的 Command？

### 扩展设计

7. 如果再增加“游泳”或“攀爬”大类状态，如何接入？
8. 如果一个行为需要同时满足移动和攻击两个正交状态，HFSM 是否仍然合适？

### 故障排查

9. 当前明明在 DodgeState，顶层查询却失败，根因是什么？
10. 状态切换后动画仍停在旧状态，应检查生命周期的哪些位置？

回答取舍题时不要只说“减少 if”。要讲公共规则上提、状态组合表达、路由成本和层级调试代价。

## 自测

1. 父状态和叶子状态各负责什么？
2. 为什么受击不适合包装成普通 Command？
3. 为什么 `MainStateMachine.CurrentState is DodgeState` 永远为 `false`？
4. `StateMachine` 为什么不负责业务切换条件？
