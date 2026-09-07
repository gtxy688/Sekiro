# 忍杀输入意图与倒地闪避融合设计

## 目标

1. Boss 被当前攻击打崩架势时，不得复用破防前已进入缓冲池的攻击命令自动触发忍杀。
2. Boss 已经处于可忍杀状态后，玩家重新按攻击键，仍能从待机或攻击后摇进入对应忍杀。
3. 玩家播放 `Hurt_Mid`、`Hurt_Heavy` 或 `Hurt_HeavyRepeat` 时，在允许垫步取消后进入闪避，需要获得可调的动画融合；普通闪避手感保持不变。

## 根因

### 破防后同一攻击触发忍杀

`AttackCommand` 同时承担普通攻击、连招预输入和忍杀确认。命令在攻击锁定段会留在 `BrainBase` 的 0.2 秒缓冲池中；当前攻击打崩 Boss 后，同一条命令下一帧重试时，会被 `GroundedState` 或 `AttackStateBase` 按新的 Boss 状态解释为忍杀。

### 倒地接闪避硬切

`StunnedState` 在 Mid/Heavy 倒地取消窗口开放后直接切入 `DodgeState`。锁定状态下，`DodgeState.OnEnter` 优先调用 `AnimUtil.TryPlay`；动画存在时不会执行后面的 CrossFade 回退，因此从倒地动画到四向闪避的融合时长实际为 0。

## 设计

### 独立忍杀命令

- 新增值类型 `FinisherCommand : ICommand`。
- `AttackCommand` 只负责普通攻击和连招，不再尝试执行忍杀。
- `PlayerBrain.OnAttackStarted` 在按键按下当帧查询当前是否存在可忍杀目标：
  - 已存在：立即缓冲 `FinisherCommand`，本次按键不进入普通攻击长按判断。
  - 不存在：继续现有短按普通攻击、长按突刺流程；即使随后 Boss 被这次攻击打崩，松键产生的仍是 `AttackCommand`。
- `DuelDirector` 与 `CombatManager` 提供无副作用的 `HasAvailableFinisher(CharacterBody initiator)` 查询，复用现有可忍杀目标查找规则。
- `GroundedState`、`AttackStateBase`、`FinisherReadyState` 只响应 `FinisherCommand` 进入忍杀。
- 可忍杀窗口内遇到旧 `AttackCommand` 时将其消费并丢弃，避免它落到 Idle/Move 后启动一刀新攻击；玩家必须重新按攻击键生成 `FinisherCommand`。

### 倒地闪避专用融合

- 在 `CharacterConfig` 新增 `KnockdownToDodgeBlendDuration`，默认 `0.12` 秒，限制为 `0~0.25` 秒并提供 Inspector Tooltip。
- `DodgeState` 接受可选的固定时间融合参数；参数大于 0 时，对最终选出的 `Dodge` 或四向闪避动画调用 `CrossFadeInFixedTime`。
- 只有 `StunnedState` 从 Mid/Heavy/HeavyRepeat 取消进入闪避时传入该配置值。
- Idle、Move、Attack、Deflect、StaggerBroken 等其他入口保持现有动画进入策略和响应速度。
- 闪避状态计时、无敌帧、识破资格、锁定朝向和 Root Motion 规则不变。

## 失败与回退

- 找不到可忍杀目标时，`FinisherCommand` 被消费但不触发普通攻击，避免输入语义降级。
- 闪避动画不存在时沿用现有失败行为，不新增替代动画名。
- `KnockdownToDodgeBlendDuration = 0` 时恢复为无专用融合，方便在 Inspector 中快速 A/B 对照。

## 手动验收

1. 在玩家攻击命中段前预输入下一刀，让当前刀打满 Boss 架势：Boss 进入破防，不能自动播放忍杀。
2. Boss 破防后重新按攻击：正常播放 `Finsher_Ground`；处于攻击后摇时也能进入忍杀。
3. 弹反或识破打崩 Boss 后重新按攻击：仍进入各自的成对忍杀。
4. 玩家吃 Mid、Heavy、HeavyRepeat 倒地，在对应取消时间后按闪避：锁定和未锁定均有约 0.12 秒姿势融合，无同帧硬切。
5. 普通待机、移动、攻击前后摇和格挡进入闪避：手感与修改前一致。
6. 倒地闪避的方向、Root Motion 位移、无敌帧与无方向前垫识破保持正常。
