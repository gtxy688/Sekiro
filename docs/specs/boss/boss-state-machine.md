# Boss 状态机（一阶段）

Idle → Move → Attack(Startup→Active→Recovery) / Ranged / Stagger → Collapse → Executed

| 状态 | 说明 |
|------|------|
| IdleState | 待机，随机选择下一行为 |
| MoveState | 靠近玩家到攻击距离 |
| AttackState | 攻击（前摇/判定/后摇三段） |
| RangedState | 远程射箭（后撤+三连射） |
| StaggerState | 被弹刀硬直 ~0.3s |
| CollapseState | 架势崩溃 2s，等待忍杀 |
| ExecutedState | 忍杀演出，战斗结束 |

HFSM 实现：继承 `State` 基类，`Initialize(BossStateMachine)`，通过 `_stateMachine.TransitionTo<T>()` 切换。
