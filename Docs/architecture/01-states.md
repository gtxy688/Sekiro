# 01 状态机与角色动作

> 现状依据：FrameWork/Body、FrameWork/States 中的当前源码。手动验收见 01-states-test.md。

## HFSM 结构

CharacterBody 持有 MainStateMachine。顶层状态是 HierarchicalState 父状态：

- GroundedState：地面父状态，子状态机承载 Idle、Move、Attack、Deflect、Dodge、Heal、Mikiri 等地面叶子状态。
- AirState：空中父状态，管理空中移动 / 跳跃与 AirAttackState。
- StunnedState：受击父状态，承载 GroundStunnedState 等受击叶子状态。
- DeadState：死亡父状态，承载回生待机或游戏结束子状态。

父状态在进入时创建 / 切入初始叶子状态，更新时驱动子状态机，退出时清理当前叶子状态。MainStateMachine.CurrentState 表示顶层父状态；叶子状态在其父状态的 SubStateMachine 中。

## Command 路由

玩家离散输入由 PlayerBrain 转成 Command，BrainBase 提供输入缓冲；移动则作为连续意图处理。命令进入 CharacterBody 的状态机路由：父状态可以先处理或拦截，再由叶子状态处理。Boss 的行为树同样通过 CharacterBody 启动动作，但 BossMoveEntry / AttackConfig 提供招式数据。

状态切换与命令是否被接受由当前状态决定。不要把所有命令都描述成同一个固定缓冲时长。

## Hit 路由

CombatResolver 将命中结算交给 CharacterBody.ReceiveHit。CharacterBody 先把 HitData 交给当前父状态路由；HierarchicalState 先询问父层，再传给当前叶子状态。状态可以处理弹反、闪避无敌、死亡期间忽略受击或连续受击等规则；没有处理的命中才继续常规结算。

## 查询和切换约定

顶层状态与叶子状态是不同层级。业务代码不能用 MainStateMachine.CurrentState 去判断 DodgeState、AttackState 等叶子类型。通过 CharacterBody 的 IsGroundedTop、IsInGroundedSubState、TryChangeGroundedSubState、ForceChangeGroundedSubState 等封装接口完成对应查询与切换。

## 动画与位移

状态负责选择动作并协调 Animator。当前角色控制同时使用代码侧方向 / 状态逻辑和 Animator / 根运动路径；具体哪个动作允许转向、何时结束，以对应状态和配置为准。不要笼统描述为“所有位移只由代码”或“所有状态都使用同一套根运动规则”。

## 核心源码

- FrameWork/Body/CharacterBody.cs
- FrameWork/States/StateMachine.cs
- FrameWork/States/Base/HierarchicalState.cs
- FrameWork/States/StunnedState.cs
- FrameWork/States/Dead/DeadState.cs
- FrameWork/States/Ground/GroundedState.cs
- Player/Brain/BrainBase.cs
