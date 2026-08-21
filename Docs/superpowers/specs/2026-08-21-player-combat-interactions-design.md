# 玩家战斗交互与成对忍杀设计

日期：2026-08-21  
状态：已确认，待实现

## 目标

同步玩家新增动画，完善防御反馈、攻击后摇、攻击转向、长按突刺、移动喝药，并将攻击崩解、弹反崩解、识破崩解三类忍杀接入现有 HFSM。

实现继续遵守：

- 顶层状态机只装 `HierarchicalState`，功能状态放在 `GroundedState.SubStateMachine`。
- Hit 仍经 `CombatManager` 与 `OnHitReceived` 路由。
- 战斗数值放入 `AttackConfig` / `CharacterConfig`。
- Animator 负责动画、Root Motion 与 Layer 混合；行为规则由 HFSM 决定。
- 忍杀清命只由动画事件触发，且必须防止重复结算。

## 一、防御与受击动画

攻击轻重暂时沿用 `HitData.knockback > 0` 区分，不新增攻击重量字段。

| 防御结果 | 轻攻击 | 重攻击 |
|---|---|---|
| 未格挡受击 | `Hurt_Ground` | `Hurt_Heavy` |
| 普通格挡 | `Hurt_Guard` | `Hurt_GuardHeavy` |
| 完美弹反 | `Deflect_Slash` | `Deflect_HeavySlash` |

规则：

1. 普通格挡只增加玩家架势，不增加 Boss 架势。
2. 完美弹反增加 Boss 架势；玩家自身架势按现有完美弹反系数结算且不会因此崩解。
3. 完美弹反未导致 Boss 崩解时，Boss 进入现有被弹反硬直。
4. 完美弹反导致 Boss 崩解时，不再调用普通被弹反硬直，避免覆盖崩解状态。
5. 玩家松开格挡键时，无论按住时长，都播放 `Deflect_Cancel`，动画结束后回待机。
6. 格挡进入、反应、退出之间使用短固定时长 CrossFade，避免从攻击取消进入格挡后突然跳回待机。

`CharacterConfig` 的受击动画默认映射同步为上述名称。

## 二、架势崩解来源

架势崩解需要携带来源，至少包含：

- `Attack`：被玩家攻击打满。
- `Deflect`：Boss 攻击被玩家完美弹反后打满。
- `Mikiri`：Boss 突刺被识破后打满。

`CharacterBody.AccumulatePosture` 在首次达到最大架势时返回“本次是否造成崩解”，并记录崩解来源。调用方据此进入对应流程。已经崩解时不重复触发。

三种 Boss 等待状态：

- `Attack`：播放现有 `Stagger_Broken`。
- `Deflect`：播放新增 `Stagger_Broken_Deflect`。
- `Mikiri`：沿用现有识破后的 Boss 反应动画并锁定行为，不新增等待动画。
- 任一种来源使 Boss 进入可忍杀状态时，都通过 `CombatEventBus` 驱动锁定 UI 显示忍杀红点，禁止 UI 每帧轮询。

弹反或识破忍杀窗口超时：

- `IsPostureBroken` 解除；
- Boss 当前架势设为最大架势的 80%（即从满值恢复 20%）；
- 发送架势变化事件并恢复 Boss 行动。
- 发送忍杀机会结束事件并隐藏红点。

攻击导致的普通崩解仍沿用原有超时规则，不改成 80%。

## 三、三类成对忍杀

动画资源当前使用的 `Finsher_*` 拼写保持不变，代码必须按该准确状态名调用。

### 1. 攻击崩解忍杀

1. Boss 因玩家攻击进入 `Stagger_Broken`。
2. 玩家在范围内按攻击。
3. 双方对齐并分别在自己的 Animator 中播放 `Finsher_Ground`。
4. 玩家动画命中帧调用 `ExecuteFinisher`。

### 2. 弹反崩解忍杀

1. 完美弹反使 Boss 架势崩解。
2. Boss 播放 `Stagger_Broken_Deflect`，玩家进入叶子状态并播放 `DeflectToFinsher`。
3. `DeflectToFinsher` 播放期间按攻击，双方对齐并播放 `Finsher_Deflect`。
4. 未按攻击且 `DeflectToFinsher` 播放结束：玩家回待机，Boss 架势恢复至 80%。

### 3. 识破崩解忍杀

1. 识破成功使 Boss 架势崩解。
2. 现有玩家识破动画的剩余时长作为忍杀确认窗口。
3. 窗口内按攻击，双方对齐并播放 `Finsher_Mikiri`。
4. 未按攻击且识破动画结束：玩家回待机，Boss 架势恢复至 80%。

### 成对动画同步

处决开始时：

- 玩家与 Boss 同帧 CrossFade 到相同类型的成对动画。
- 以 Boss 为基准，将玩家放到该忍杀类型配置的本地偏移位置。
- 双方水平朝向相对，清除各自攻击判定并锁定命令与受击。
- 三类忍杀分别保存可调站位偏移，避免不同成对动画穿模。
- 忍杀确认输入被接受后立即隐藏红点，避免红点覆盖处决演出。

处决结算：

- 玩家 Animator 所在对象提供 `ExecuteFinisher` 动画事件入口，并转发给 `CombatManager`。
- `CombatManager` 保存当前处决双方，并以幂等标志保证每次忍杀只清一条命。
- 动画播放结束后双方退出处决状态；若命中事件缺失，输出明确错误并执行一次兜底结算，避免永久锁死。
- 旧 `FinisherState` 的定时清命移除，防止动画事件与定时器双重扣命。

## 四、攻击窗口与输入缓冲

`AttackConfig.ComboWindowStart` 更名为 `RecoveryWindowStart`，使用 `FormerlySerializedAs` 保留现有 SO 数值。

时间语义：

1. `stateTimer < HitStartTime`：保留现有攻击前摇格挡/闪避取消。
2. `HitStartTime <= stateTimer < RecoveryWindowStart`：攻击锁定阶段，不接受取消。
3. `RecoveryWindowStart <= stateTimer <= ComboWindowEnd`：
   - `AttackCommand` 立即切 `NextCombo`。
   - 移动、格挡、闪避、跳跃、喝药立即取消当前攻击。
4. `stateTimer > ComboWindowEnd`：
   - 不再衔接本段 `NextCombo`。
   - 离散命令继续使用通用 0.2 秒缓冲；若动作结束前仍未过期，则由后续状态处理。

攻击预输入不新增专用缓存：

- 窗口前收到 `AttackCommand` 时，`AttackState` 返回 `false`。
- `BrainBase` 每帧重试该命令。
- 进入 `RecoveryWindowStart` 前不超过 0.2 秒的输入会自动落地。
- 输入过早则自然超时。
- 删除无实际作用的 `hasBufferedNextHit`。

父状态的跳跃、喝药拦截必须服从攻击窗口，不能继续在攻击任意阶段无条件强切。

## 五、攻击转向

`AttackConfig` 增加：

- 是否允许转向。
- 攻击转向速度。
- 转向窗口结束时间。

规则：

- 锁定目标存在时，转向窗口内持续朝向 Boss。
- 未锁定时，转向窗口内按当前移动输入调整攻击方向。
- `MoveCommand` 在攻击锁定阶段只更新方向意图，不切 `MoveState`。
- 到达 `RecoveryWindowStart` 后，有移动输入才切 `MoveState`；零输入不会自动取消攻击。
- 攻击 Clip 的 Root Transform Rotation 烘焙进姿势，Root Transform Position 继续提供根位移，避免动画根旋转与代码转向竞争。

## 六、长按攻击触发突刺

输入规则：

- Attack 按下时开始计时，暂不发送普通攻击。
- 达到 `CharacterConfig.AttackHoldDuration`（默认 0.3 秒）且仍按住时，立即发送一次突刺攻击。
- 达到阈值后继续按住不重复发送。
- 阈值前松开时发送普通攻击。
- 不修改 InputAction，不添加 Hold Interaction。

数据：

- `CharacterBody` 增加玩家突刺用 `ThrustAttack` 引用。
- 突刺使用独立 `AttackConfig`，`AnimName = "Thrust"`。
- `AttackCommand` 保持空 struct；`PlayerBrain` 在发送命令前设置本次主动攻击配置。
- `AttackState` 取得配置后清除一次性的主动攻击选择，避免后续普攻误用突刺。

普通攻击改为短按松开触发，这是单键区分短按与长按的必要代价。

## 七、移动喝药

Animator：

- Base Layer：现有 `Idle` 与用户已配置的 `Walk_Slow_Strafe`。
- UpperBody Layer：Override 模式、`Player_UpperBody` Avatar Mask、状态 `Drink_UpperBody`。
- Avatar Mask 包含脊柱、胸、头和双臂，排除 Root、Hips 与双腿。
- `Drink` Clip 的根旋转和根位置全部 Bake Into Pose，不参与角色位移。
- Base Layer 不再需要 `Drink` 状态；代码迁移完成前可暂时保留。

状态规则：

- `HealState` 进入时消耗葫芦，Base Layer 按输入播放 `Idle` / `Walk_Slow_Strafe`，UpperBody Layer 播放 `Drink_UpperBody`。
- 仅放行 `MoveCommand`，攻击、格挡、闪避、跳跃、重复喝药均禁止。
- 锁定时持续面向 Boss，并以 `MoveX` / `MoveZ` 驱动慢走四向混合树。
- 未锁定时按输入方向转身并慢走。
- 无输入时 Base Layer 回现有 `Idle`，不需要慢走待机动画。
- 正常结束、受击打断和任何状态退出路径都将 UpperBody Layer Weight 清零。
- UpperBody Layer 索引按名称查找并缓存；缺层或缺状态时记录错误并安全退回待机。

该规则取代原架构文档中“喝药期间不可移动”的旧规定。

## 八、涉及文件

预计修改：

- `Assets/Scripts/SO/AttackConfig.cs`
- `Assets/Scripts/SO/CharacterConfig.cs`
- `Assets/Scripts/Player/Brain/PlayerBrain.cs`
- `Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- `Assets/Scripts/FrameWork/States/Command.cs`
- `Assets/Scripts/FrameWork/States/Ground/AttackState.cs`
- `Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`
- `Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`
- `Assets/Scripts/FrameWork/States/Ground/HealState.cs`
- `Assets/Scripts/FrameWork/States/Ground/GroundedState.cs`
- `Assets/Scripts/FrameWork/States/Ground/FinisherState.cs`
- `Assets/Scripts/FrameWork/States/Ground/StaggerBrokenState.cs`
- `Assets/Scripts/Combat/CombatManager.cs`
- `Assets/Anim/player.controller`
- `Docs/architecture/01-states.md`
- `Docs/architecture/02-combat-data.md`
- `Docs/architecture/05-input-lockon.md`
- `Docs/architecture/07-anim-events.md`

预计新增：

- 弹反忍杀确认叶子状态。
- Boss 弹反崩解锁定状态。
- Boss 识破崩解锁定状态。
- 处决受害者锁定状态。
- 玩家突刺 `AttackConfig` 资源（由用户按动画时长与战斗数值配置并绑定）。

## 九、错误处理与验收重点

错误处理：

- Animator Layer、状态或攻击配置缺失时输出包含角色名与资源名的错误，不静默失败。
- 忍杀执行使用幂等保护，重复动画事件不会重复清命。
- 架势已经崩解时不再次进入其他崩解状态。
- 弹反崩解后禁止普通 `ForceParryStun` 覆盖崩解状态。
- 状态退出统一关闭 Hitbox、清理 UpperBody Layer 和一次性主动攻击配置。
- 忍杀红点在进入任一确认窗口时显示，并在超时、执行开始、目标失效或清命后可靠隐藏。

手动验收重点：

1. 六种防御/受击动画按轻重正确选择。
2. 攻击前摇取消后松开格挡必播 `Deflect_Cancel`，衔接无明显跳变。
3. 攻击预输入早于 0.2 秒会丢失，临近后摇输入会自动接下一段。
4. 后摇开始后，除攻击外的允许行为立即响应；攻击仍按连招截止时间处理。
5. 锁定攻击追踪 Boss，未锁定攻击按移动方向转向，动画根运动无方向争夺。
6. 短按正常攻击，长按 0.3 秒自动播放 `Thrust`，长按不连发。
7. 喝药时可用慢走四向移动，上下身动画同时正确播放，受击后 Layer 不残留。
8. 三类架势崩解进入正确等待动画和确认窗口。
9. 三类确认窗口均显示忍杀红点，超时或进入处决演出后立即隐藏。
10. 三组成对忍杀位置、朝向和动画同步，命中帧只清一条命。
11. 弹反/识破忍杀超时后 Boss 架势为 80%，普通攻击崩解仍保持原超时规则。
