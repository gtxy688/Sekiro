# 项目代码现状

> 更新日期：2026-09-23
> 本页只记录在工程中能定位到的代码 / 配置模块，不代表功能质量或场景完整度。

## 核心运行链路

| 功能 | 当前代码入口 |
|---|---|
| 玩家输入 | PlayerBrain、BrainBase、PlayerInputActions |
| 角色与状态 | CharacterBody、StateMachine、HierarchicalState、GroundedState、AirState、StunnedState、DeadState |
| Boss AI | BTBrain、Blackboard、BossMovePicker、BossMoveTable、BT_ExecuteMove |
| 攻击时间轴 | AttackStateBase、AttackAnimClock、AttackConfig、BossMoveWindow |
| 命中与结算 | Hitbox、Hurtbox、ArrowProjectile、CombatManager、CombatResolver、CharacterBody.ReceiveHit |
| 表现事件 | CombatEventBus、CombatUIController、AudioManager、CameraController |
| 支持系统 | LockOnManager、EncounterScope、Faction、TimeScaleController |

## 配置现状

- CharacterConfig 与 AttackConfig 是 ScriptableObject。
- BossMoveTable 与 BossMoveEntry / BossMoveWindow 保存 Boss 招式、判定窗口和覆盖数据。
- 默认伤害档位包含在 AttackCombatResolve；不可把全部战斗数值描述为完全没有代码默认值。
- 当前可编辑工具见 Assets/Editor；工具存在不代表每个资源已填完或场景已正确装配。

## 文档维护

此前版本记录于 2026-08-13，包含过期的“攻击尚未接线”“PlayerBrain 只绑定 Move”等表述。已不再作为当前进度依据。
