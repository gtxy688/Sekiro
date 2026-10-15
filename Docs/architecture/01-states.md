# 01 状态机与角色动作

> 现状依据：FrameWork/Body、FrameWork/States 中的当前源码。

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

## 主动防御：唯一窗口、尝试与空按记账

主动弹反资格由 `DeflectAttemptTracker` 单独判定，条件是"未结束的尝试 + 固定半开区间 `[startedAt, expiresAt)`"：命中恰好等于截止时间不算完美弹反。状态 Update 与命中入口都会先按当前时间推进到期，避免两者的执行顺序改变窗口外命中的结果。时间使用 `Time.time` 缩放时间，暂停与 HitStop 期间不会因墙钟流逝额外耗尽窗口。

一次真实物理按下生成一个 PressId；只有命令最终让身体实际开启防御才创建尝试（AttemptId）。同一个 PressId 被缓冲重试或状态重入只开一次窗口；被状态拒收、缓冲超时、被其他命令覆盖都不创建尝试，也不记空按。窗口起点是身体接受并开启防御的 `Time.time`，预输入时间不作为起点。

空按只在结果为"未弹中"时记账：窗口到期、被新的有效按下替换、垫步等主动离开防御、窗口内被无法弹反的命中打断，各记一次。死亡、复战、对象停用与输入模式切换按生命周期取消，不记空按。成功弹反立即清零连续空按与缩窗，且记账发生在飞舟末刀互弹、架势崩解转忍杀准备等分支切状态之前；Boss 被动防御入口不参与玩家主动尝试记账。一次窗口仍可弹多刀，成功不刷新起点、不延长截止时间，长按也不会自动续窗。普通格挡既不清罚也不给已经结束的尝试追加失败。

下一次窗口为 `max(最小窗口, 基础窗口 × 惩罚系数^max(0, 连续空按次数 - 宽容次数))`；相邻两次实际开启尝试的间隔超过恢复间隔则清零累计（按开启间隔判断，不按结算时刻判断）。参数取值与非法值兜底见 02-combat-data。

抬刀阶段（`inBegin`）只控制 `Deflect_Begin` / `Deflect_Repeat` 表现，不再放行弹反资格：原地抬刀在动画进度达到 92% 或抬刀计时达到 0.55 秒后结束；走路进防御不播独立抬刀动画，而是融合到举刀走路（`RaiseBlend = 0.22` 秒），融合期间继续响应转向与举刀走路切换。动画播放速度或长度都不会延长弹反窗口。

松开防御不再用 IdleCommand 立即取消窗口：窗口内松手按短点按处理，窗口保留到截止时间后直接回待机；到期时仍按住则继续普通格挡，之后再松手走 `Deflect_Cancel` 收刀。垫步等主动取消仍立刻结束窗口。箭 Heavy 动画锁定期间未实际开启窗口的防御请求不消耗、不记空按。

系统回防（崩解恢复自动回防、UI 防御选择）只请求普通格挡姿态：不开启弹反窗口，也不累计空按。Boss 被动防御（`DeflectEntryMode.PassiveGuard` / `PerfectParry`）同样不参与玩家主动尝试记账，其连挡升级与交锋反击规则保持不变。

## 核心源码

- FrameWork/Body/CharacterBody.cs
- FrameWork/Body/DeflectAttemptTracker.cs
- FrameWork/Body/DeflectMemory.cs
- FrameWork/States/Command.cs
- FrameWork/States/Ground/DeflectState.cs
- FrameWork/States/StateMachine.cs
- FrameWork/States/Base/HierarchicalState.cs
- FrameWork/States/StunnedState.cs
- FrameWork/States/Dead/DeadState.cs
- FrameWork/States/Ground/GroundedState.cs
- Player/Brain/BrainBase.cs
- Player/Brain/PlayerBrain.cs
