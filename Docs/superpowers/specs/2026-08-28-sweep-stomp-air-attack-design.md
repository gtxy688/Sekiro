# 横扫踩头（Jump2）与空中三连设计

日期：2026-08-28  
状态：已确认，待实现

## 目标

把横扫应对从「空中挨扫自动反制」改成只狼式踩头：先跳，空中再点跳播 `Jump2`；踩在正在横扫的 Boss 身上才上升并结算伤害。同时恢复空中平 A 三连（`AirAttack1/2/3`），伤害与地面轻砍相同。

本规格**不包含**：JumpThrust 镜头上抬、迷雾、落地突刺/横扫分叉。那些另开规格。

实现继续遵守：

- 顶层只装 `HierarchicalState`；`Jump2` / 空中刀都是 `AirState` 的叶子，禁止 `MainStateMachine.CurrentState is AirAttackState`。
- 踩头与空中刀的伤害都经 `CombatManager`；踩头**不**走 `ReceiveHit`（避免 Boss 进受击）。
- 战斗数值进 `AttackConfig` / `CharacterConfig`，不在 `.cs` 里写死伤害。
- 表现层仍只订 `CombatEventBus`。

## 一、空中 HFSM

```
AirState
└─ SubStateMachine
   ├─ AirIdleState     Jump / Jumping / Jump2 / Fall
   └─ AirAttackState   AirAttack1 → AirAttack2 → AirAttack3
```

- 恢复 `AirAttackState`。不要把地面 `AttackState` 挂进 `AirState`（结束会切地面 `IdleState`）。
- `AirDeflectState` / `AirStunnedState` 仍不恢复。空中被打继续切顶层 `StunnedState`。
- `AirState.OnEnter` 清「本段滞空已用 Jump2」标记。落地回 `GroundedState` 也清。

落地：

- `AirIdleState`：现有 `Fall` 播完再离空。
- `AirAttackState`：脚沾地立刻回 `GroundedState`（默认 Idle）。没挥完的 2、3 段丢掉。

## 二、Jump2 与横扫踩头

每次滞空只能成功处理 **一次** `JumpCommand` 作为 Jump2（含没踩中只播动画）。

`AirIdleState.HandleCommand(JumpCommand)`：

1. 已用过 Jump2 → 消耗命令，什么都不做。
2. 播 `Jump2`（Animator 短名必须是 `Jump2`）。
3. 踩中判定（同时满足才上升 + 结算）：
   - Boss 正在攻击，且当前 `AttackConfig.Perilous == Sweep`（段级危字烘焙进当前 ActiveAttack 也算）。
   - 玩家脚底对 Boss 躯干/ Hurtbox 做短距离检测（半径、高度从 `CharacterConfig` 读）。
4. **没踩中**：不改垂直速度（用户原话：平常也能做 Jump2，没有踩的东西就不上升）。
5. **踩中**：`QueueJump()`（速度用现有 `JumpSpeed`）+ `CombatManager.ApplySweepStomp(player, boss)`。

`ApplySweepStomp`：

- 血量 / 架势用**玩家当前 `LightAttack` 的 `BaseDamage` / `PostureDamage`**（与地面轻砍第一刀相同；LightAttack 为空则回退 CharacterConfig 占位默认 10 / 15）。
- 对 Boss 调 `TakeDamage`，**不**调 `ReceiveHit`，Boss 不播 Hurt、不切 `StunnedState`。
- 置 `boss.SuppressAttackHitbox = true`。`AttackState.ApplyHitbox` 见此旗则保持关刀，直到本段 `AttackState.OnExit` 清掉。横扫动画继续播完。
- 架势允许打崩（`PostureBreakSource.Attack`）。打崩走现有 `ForcePostureBroken`，这是崩解不是受击。
- 打铁 / 顿帧可发现有 Perfect 弹刀事件（与自动反制时同类反馈），不把 Boss 打进 `ParriedState`。

删除 `AirIdleState.OnHitReceived` 里「空中被 Sweep 命中就 `AccumulatePosture` + `ForceParryStun`」。只跳一次、空中挨横扫 = 没防挨打。垫步无敌仍可躲。突刺识破仍是地面无方向垫步 → `MikiriCounterState`，与踩头无关。

挥刀命中段不能切 Jump2；前摇 / 后摇可以（与地面攻击取消同一套时间窗）。Jump2 播着可以接空中平 A。

## 三、空中三连

`CharacterBody` 增加 `AirAttack` 槽（连段起点，类似 `LightAttack`）。三张 `AttackConfig`：

| SO | AnimName | NextCombo |
|----|----------|-----------|
| AirAttack1 | `AirAttack1` | AirAttack2 |
| AirAttack2 | `AirAttack2` | AirAttack3 |
| AirAttack3 | `AirAttack3` | 空 |

伤害 / 架势 / `HitGrade`：**与地面轻砍同一套**。AirAttack1 拷 `LightAttack`；2 / 3 拷地面 `NextCombo` 链上对应段；地面没有第 2 / 3 段则 2 / 3 也用 `LightAttack` 的数值。窗口时间按空中片自己的时间轴填，不要复用地面片时长。

`AirIdleState` 收到 `AttackCommand` → `AirAttackState`（普通跳、踩空、Jump2 后都可以）。判定仍是动画时钟 + BoxCast → `CombatManager.ReportHit`。Boss 被空中刀打中仍走现有受击（踩头才跳过受击）。

`AirAttackState` 结束且仍在空中 → 回 `AirIdleState`（按速度切 Jumping / Fall，不要切地面 Idle）。

空中不可格挡、不可垫步（现有空中也没有）。落地取消见第一节。

## 四、配置与资源

- 玩家 Animator 必须有短名：`Jump2`、`AirAttack1`、`AirAttack2`、`AirAttack3`（用户已有片）。
- `CharacterConfig`：踩头检测半径 / 相对脚底的高度容差（默认半径 0.6、高度 1.2，可调）。不要把伤害写进 Config（伤害跟 LightAttack）。
- `CharacterBody.AirAttack` 在场景里拖 AirAttack1。缺槽或缺 Animator 状态：空中平 A 不生效并 `LogError`，不要回退成地面 `LightAttack` 动画名。

## 五、架构文档（实现时改，不要改别的模块文档）

| 文件 | 改什么 |
|------|--------|
| `Docs/architecture/01-states.md` | AirState 树加上 AirAttackState / Jump2；删「AirAttackState 已移除」；空中可平 A、Jump2 一次、落地掐连段 |
| `Docs/architecture/01-states-test.md` | Animator 表加 Jump2、AirAttack1/2/3；空中平 A / 落地取消 / Jump2 无踩不上升 |
| `Docs/architecture/03-hit-detection.md` | Sweep：先跳再 Jump2 踩头；关判定、招继续；空中挨扫不再自动反制 |
| `Docs/architecture/03-hit-detection-test.md` | 横扫踩头成功 / 只跳一次挨扫 / 空中三连伤害对齐地面 |

## 六、明确不做

- JumpThrust 镜头、迷雾、落地分叉
- 空中格挡 / 空中垫步 / 空中受击专用动画
- 把 Jump2 做成无限二段跳
- 非 Sweep 时踩 Boss 给上升（没横扫就只播 Jump2）
- 踩头走 `MikiriCounterState` 或 `ForceParryStun`

## 验收要点（实现后写入上述 test 文档）

1. 地面轻跳，空中点攻击：播 AirAttack1，伤害与地面第一刀相同；可接 2、3。
2. 空中三连没打完就落地：立刻回地面，不在地上把空中刀挥完。
3. 普通跳空中点跳：播 Jump2，高度不明显再跳一次。
4. Boss 放 Sweep，跳起再 Jump2 踩中：玩家上升；Boss 掉与轻砍第一刀相同的血/架势、不播受击；横扫继续但后续扫不中。
5. 只跳一次、空中被 Sweep 扫到：挨打，不再自动把 Boss 弹开。
6. 无方向垫步仍只识破突刺，不识破横扫。
7. 同一段滞空第二次点跳：不再播 Jump2。
