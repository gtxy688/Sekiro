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
| `Hurt_Ground` | 受击 | GroundStunnedState.cs |
| `Jump` | 起跳（上升段） | AirIdleState.cs |
| `Fall` | 下落（过最高点后切） | AirIdleState.cs |
| `Mikiri` | 踩刀 | MikiriCounterState.cs |
| `Deflect_Slash` / `Deflect_HeavySlash` | 完美弹反轻/重攻击 | DeflectState.cs |
| `Hurt_Guard` / `Hurt_GuardHeavy` | 普通格挡轻/重攻击 | DeflectState.cs |
| `Hurt_Heavy` | 未格挡重攻击 | GroundStunnedState.cs |
| `DeflectToFinsher` | 弹反崩解后的忍杀确认姿态 | FinisherReadyState.cs |
| `Finsher_Ground` / `Finsher_Deflect` / `Finsher_Mikiri` | 三组成对忍杀 | FinisherState.cs |
| 攻击状态名 | 攻击 | `atk1.asset` 的 AnimName 字段 |

> 攻击状态名要写进 atk1.asset 的 `Anim Name` 字段，两者一致。
> 移动方式：全权根运动（Animator.applyRootMotion = true），位移由动画 Root 曲线驱动，代码只负责朝向与状态切换。

### 4. 测试脚本（临时，验收完删除）

`Assets/Scripts/M1HitTest.cs` 挂到 Player 上，按键：
- **H** = 触发一次受击
- **J** = 触发受击（用于硬直期间连按，测二次拦截）
- **K** = 开启武器判定（M3 用）
- **L** = 关闭武器判定（M3 用）

## M1 受击路由验收

| # | 操作 | 预期 |
|---|------|------|
| 1 | 打开场景看 Console | 0 个编译错误 |
| 2 | 按 **H** | 切 StunnedState → 播 `Hurt_Ground` → 持续 `Config.StunDuration`（0.5s）后自动回 Idle |
| 3 | 按 H 进入硬直后，硬直期间快速连按 **J** | 二次受击被拦截：**不掉血**（看 Inspector `CurrentHP` 或 UI 血条）、**硬直不刷新**（仍在原硬直剩余时间里，不从头开始） |
| 4 | 按空格跳起（空中按 **H**） | 跳跃有向上初速度（`Config.JumpSpeed`）；空中被打也进 StunnedState 播 `Hurt_Ground`，落地后回地面 |
| 5 | 无硬直时按 J（与 H 等效） | 正常进入受击，行为同 #2 |

## 常见问题

- **按 H 没反应**：确认 M1HitTest 已挂角色、Console 无报错；`ReceiveHit` 在 Update 里每帧可触发，检查是否被 `CurrentHP <= 0` 挡住（死了不结算）
- **二次受击还会刷新硬直**：`StunnedState.OnParentHandleHit` 是否返回 true（代码在 StunnedState.cs）
- **硬直时长不对**：改 `PlayerConfig.asset` 的 `StunDuration` 看是否生效
- **跳跃没跳起来**：`Config.JumpSpeed` 是否配置（>0），跳跃初速度从这里读

## M4：防御反馈验收

| # | 操作 | 预期 |
|---|------|------|
| 6 | 分别格挡轻攻击和 `Knockback > 0` 的重攻击 | 播 `Hurt_Guard` / `Hurt_GuardHeavy`，玩家涨架势，Boss 不涨架势 |
| 7 | 分别完美弹反轻攻击和重攻击 | 播 `Deflect_Slash` / `Deflect_HeavySlash`，Boss 涨架势 |
| 8 | 攻击前摇按住格挡取消，随后立即松开 | 必定播放 `Deflect_Cancel`，播完回 Idle |

## M10：三类忍杀验收

| # | 操作 | 预期 |
|---|------|------|
| 9 | 玩家攻击打满 Boss 架势 | Boss 播 `Stagger_Broken`，红点显示；按攻击后双方播 `Finsher_Ground` |
| 10 | 完美弹反打满 Boss 架势 | Boss 播 `Stagger_Broken_Deflect`，玩家播 `DeflectToFinsher`，红点显示；按攻击后双方播 `Finsher_Deflect` |
| 11 | 识破突刺打满 Boss 架势 | 识破动画期间红点显示；按攻击后双方播 `Finsher_Mikiri` |
| 12 | 弹反/识破崩解后不按攻击 | 确认动画结束后红点隐藏，Boss 架势降至 80% 并恢复 |
| 13 | 任一忍杀动画播放到命中帧 | 只清 Boss 一条命；双方位置、朝向和动画同步 |
