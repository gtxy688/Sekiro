# 01 状态机核心 - 验收清单（M1 受击路由）

> 配合 `01-states.md` 使用。当前验收范围：**M1 受击路由**（M4 弹反/闪避窗口、M10 处决待后续模块实现后再验）。

## 前置准备（场景搭建）

### 1. 玩家角色组装

```
Player（GameObject）
├─ CharacterBody 组件
├─ PlayerBrain 组件（输入）
├─ Rigidbody（Freeze Rotation X/Z，建议勾选 Interpolate）
├─ Capsule Collider（包住身体，物理受击目标）
├─ Animator（挂 Mixamo 角色 + Avatar）
├─ 身体上挂 Hurtbox 组件（标记"可被打"）
└─ GroundCheck（脚底空物体）
```

### 2. CharacterBody Inspector 配置

| 槽位 | 填什么 |
|------|--------|
| `Config` | `PlayerConfig.asset` |
| `Light Attack` | `atk1.asset`（攻击招式根节点） |
| `Ground Check Point` | GroundCheck 空物体 |
| `Ground Check Radius` | 0.2 |
| `Ground Layer` | 地面所在层 |

### 3. Animator 状态名（必须与代码一致，CrossFade 按名字匹配）

| 状态名 | 对应动画 | 代码出处 |
|--------|---------|---------|
| `Idle` | 只狼待机 | IdleState.cs |
| `Walk` | 只狼行走（WASD 是行走） | MoveState.cs |
| `Dodge` | 未锁定垫步 | DodgeState.cs |
| `Dodge_Forward` / `Dodge_Back` / `Dodge_Left` / `Dodge_Right` | 锁定四向垫步（独立状态，不要融合树） | DodgeState.cs |
| `Hurt_Light`（旧名 `Hurt_Ground` 可回退） | 未防御 Light | GroundStunnedState.cs |
| `Hurt_Light2` | Light 连续受击 | GroundStunnedState.cs |
| `Hurt_Mid` | 未防御 Mid | GroundStunnedState.cs |
| `Hurt_Heavy` / `Hurt_HeavyRepeat` | 未防御 Heavy / 倒地再吃 Heavy | GroundStunnedState.cs |
| `Standing` | Mid/Heavy 倒地后起身 | StandingState.cs |
| `MidToGuard` | Hurt_Mid 倒地结束前按防御 | MidToGuardState.cs |
| `Jump` | 起跳（上升段） | AirIdleState.cs |
| `Fall` | 下落（过最高点后切） | AirIdleState.cs |
| `Jump2` | 空中再跳（踩头；没踩中不升高） | AirIdleState.cs |
| `AirAttack1` | 空中轻砍 1（伤害对齐地面 atk1） | AirAttackState.cs |
| `AirAttack2` | 空中轻砍 2（伤害对齐地面 atk2） | AirAttackState.cs |
| `AirAttack3` | 空中轻砍 3（伤害对齐地面 atk3） | AirAttackState.cs |
| `Mikiri` | 踩刀 | MikiriCounterState.cs |
| `Mikiri_Deflect` | 被识破硬直（Boss；旧名 `Miriki_Deflect`） | ParriedState.cs |
| `Deflect_Slash` / `Deflect_HeavySlash` / `Deflect_HeavyArrow` | 完美弹反 Light / 其余近战 / 箭 Heavy | DeflectState.cs |
| `Deflected` | 被完美弹反硬直 | ParriedState.cs |
| `Hurt_Guard` | 普通格挡 Light/Mid | DeflectState.cs |
| `DeflectToFinsher` | 弹反崩解后的忍杀确认姿态 | FinisherReadyState.cs |
| `Finsher_Ground` / `Finsher_Deflect` / `Finsher_Mikiri` | 三组成对忍杀 | FinisherState.cs |
| 攻击状态名 | 攻击 | `atk1.asset` 的 AnimName 字段 |

> 攻击状态名要写进 atk1.asset 的 `Anim Name` 字段，两者一致。
> 移动方式：全权根运动（Animator.applyRootMotion = true），位移由动画 Root 曲线驱动，代码只负责朝向与状态切换。

## M1 受击路由验收

Play `GameScene`，用真实战斗验收（不要再挂临时按键脚本）。

| # | 操作 | 预期 |
|---|------|------|
| 1 | 打开场景看 Console | 0 个编译错误 |
| 2 | Play，让 Boss 打中未防御的玩家 | 切 StunnedState → 按该招 `HitGrade` 播 `Hurt_Light` / `Hurt_Mid` / `Hurt_Heavy` → Light 播完回 Idle；Mid/Heavy 再播 `Standing` |
| 2a | `PlayerConfig.StunDuration = 0.2`，裸吃 Light，到点按垫步 | 约 0.2s 后能垫步取消；不垫则 `Hurt_Light` 仍播完再 Idle |
| 2a2 | 裸吃 Light / `Hurt_Light2`，动画未结束就按防御 | 立刻进 `DeflectState`（抬刀/举刀），打断剩余受击；Mid/Heavy 不受此条影响 |
| 2b | `KnockdownStunDuration = 0.8`，裸吃 Mid，到点按垫步 | 约 0.8s 后能垫步；不垫则倒地播完再 `Standing`。`HurtMidFallEndTime` 内仍可 MidToGuard |
| 2b2 | `HeavyStunDuration = 0.8`，裸吃 Heavy，到点按垫步 | 约 0.8s 后能垫步；不垫则倒地播完再 `Standing`。改 Mid 字段不影响 Heavy |
| 2c | `KnockdownStunDuration = 0`，裸吃 Mid 按垫步 | Mid 倒地期间垫步无效，必须播完再 `Standing` |
| 2c2 | `HeavyStunDuration = 0`，裸吃 Heavy 按垫步 | Heavy 倒地期间垫步无效；Mid 仍按 `KnockdownStunDuration` |
| 3 | 被打进硬直后，硬直期间再被 Boss 打中 | **掉血涨架势**。动画：Light 再吃任意等级会刷新（第二次起 `Hurt_Light2`）；Mid 再吃 Light/Mid 不刷新，吃 Heavy 播 `Hurt_HeavyRepeat`；Heavy 再吃 Light/Mid 不刷新，再吃 Heavy 播 `Hurt_HeavyRepeat` |
| 3a | Hurt_Mid 倒地结束前按防御 | 进 `MidToGuard`；按住进举刀，松开回 Idle |
| 3b | Hurt_Mid 躺地后再按防御 | 不进 MidToGuard，播完 `Standing` |
| 3c | Standing 起身中再挨刀 | 按新一击完整播受击 |
| 4 | 按空格跳起，空中被 Boss 打中 | 跳跃有向上初速度（`Config.JumpSpeed`）；空中被打也进 StunnedState 播受击，落地后回地面 |
| 5 | 无硬直时被 Boss 打中 | 正常进入受击，行为同 #2 |

## 空中攻击 / Jump2

| # | 操作 | 预期 |
|---|------|------|
| A1 | 普通跳，空中点攻击 | 播 AirAttack1，可接 2、3；伤害与地面对应轻砍相同 |
| A2 | 空中三连未完落地 | 立刻回地面 Idle，不在地上把空中刀挥完 |
| A3 | 普通跳空中点跳 | 播 Jump2，人不再明显升高 |
| A4 | 同一跳第二次点跳 | 不再播 Jump2 |

## 常见问题

- **挨打没反应**：确认 Console 无报错；`ReceiveHit` 被 `CurrentHP <= 0` 挡住则已死亡；检查 Hitbox/Hurtbox 层与 `CombatManager` 接线
- **二次受击还会刷新硬直**：玩家侧按等级规则刷新是对的；Boss 被打仍不应刷新。看 `StunnedState.useHitGrade`
- **硬直时长不对**：受击动画默认播完。想早垫步，改 `PlayerConfig` 的 `StunDuration`（Light）、`KnockdownStunDuration`（Mid）、`HeavyStunDuration`（Heavy）。对应字段填 `0` 则该等级倒地/硬直期间不能垫步。
- **跳跃没跳起来**：`Config.JumpSpeed` 是否配置（>0），跳跃初速度从这里读

## M4：防御反馈验收

| # | 操作 | 预期 |
|---|------|------|
| 6 | 格挡刀/箭 Light 或 Mid | 播 `Hurt_Guard`，玩家涨架势，Boss 不涨架势 |
| 6b | 格挡刀 Heavy（踢、突刺等） | 防不住，等同没防，播 `Hurt_Heavy` |
| 6c | 格挡箭 Heavy（`Bow_Heavy`） | 播 `Stagger_Broken`（不是真崩架势）；必须播完；播完按住继续举刀 |
| 7 | 弹反玩家自己的刀（Boss 被动弹反） | `Deflect_Slash`，**玩家**播 `Deflected` 并涨架势 |
| 7b | 弹反 Mid 或刀 Heavy | `Deflect_HeavySlash` |
| 7c | 弹反箭（含 Light/Mid/Heavy） | 播对应弹反动画；**Boss 架势不变、不进 `Deflected`**。箭 Heavy 播 `Deflect_HeavyArrow` |
| 7d | 弹反 Boss 连段（`Boat` / `Slash_Double` 等） | 火花+涨 Boss 架势；**Boss 不进 `Deflected`，招继续**，可连续弹后面几刀 |
| 8 | 攻击前摇按住格挡取消，随后立即松开 | 必定播放 `Deflect_Cancel`，播完回 Idle |
| 8b | 无方向垫步识破突刺但 Boss 架势未满 | Boss 立刻停刀，播 `Mikiri_Deflect`（旧名 `Miriki_Deflect` 也可），不是继续挥危字；从打断到下一刀之前都保持被打断时的水平朝向，不追着玩家转身 |

## M10：三类忍杀验收

| # | 操作 | 预期 |
|---|------|------|
| 9 | 玩家攻击打满 Boss 架势 | Boss 播 `Stagger_Broken`，红点显示；按攻击后双方播 `Finsher_Ground` |
| 10 | 完美弹反打满 Boss 架势 | Boss 播 `Stagger_Broken_Deflect`，玩家播 `DeflectToFinsher`，红点显示；按攻击后双方播 `Finsher_Deflect` |
| 11 | 识破突刺打满 Boss 架势 | Boss 播 `Stagger_Broken_Miriki`（不是继续挥刀）；确认窗口保持被打断时朝向，不猛转到玩家；识破动画未结束前按攻击，双方播成对忍杀（玩家 `Finsher_Mikiri` / Boss `Finsher_Miriki`），开演才水平对视 |
| 12 | 弹反/识破崩解后不按攻击 | 确认动画结束后红点隐藏，Boss 架势降至 80% 并恢复 |
| 13 | 任一忍杀动画播放完毕 | 只清 Boss 一条命；开演前双方水平对视，**不瞬移站位**；成对动画播完后解锁 |
| 13a | 忍杀开演瞬间看双方朝向 | 玩家与 Boss 面对面；位置不变 |
| 13b | 打崩 Boss 后，玩家还在连招后摇里再按攻击 | 进入忍杀，不接 `NextCombo` |
| 13c | 忍杀动画播放中按攻击/跳跃/喝药/移动 | 双方都不切其他动作，直到动画结束 |
| 14 | 玩家架势打满 | 播 `Stagger_Broken`（倒地，不是格挡受击）；期间跳跃/喝药不能打断。动画结束立刻清架势条并恢复，不会额外卡住约 5 秒；起身后才能忍杀 |
| 14b | 玩家崩解倒地期间再挨 Boss 刀 | **掉血**，立刻从 `Stagger_Broken` 切到 `Hurt_Heavy` 倒地；架势条清空。倒地动画播完后播 `Standing`，再回 Idle |
| 15 | 攻击打崩 Boss 后玩家不按攻击 | Boss 播完 `Stagger_Broken` 立刻清架势条并解除崩解，不进 `Finsher_Ground`，不掉命 |
| 16 | 崩解窗口内 Boss 仍可能发攻击意图 | Boss 不播忍杀、不 `ClearLife`；Console 无「自己 CrossFade 忍杀」 |
| 17 | 玩家自己崩解期间连按攻击 | 不能忍杀 Boss；起身后再按才可能处决 |
