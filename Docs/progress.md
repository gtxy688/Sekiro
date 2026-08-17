# 项目进度文档

> 复刻只狼弦一郎 Boss 战 | Unity 2022 LTS + URP | 秋招作品集
> 更新日期：2026-08-13
> 本文档记录「已完成的代码模块」与「待办模块」，供快速查阅。设计总纲见 `../策划案.md`，模块细节见 `Docs/architecture/`。

---

## 总览

```
战斗核心（M1/M2/M3 已完成） ──► 战斗完整（M4/M9/M10/M17）
        │                              │
        ▼                              ▼
  表现层（M12相机/M13UI/M15音效）   Boss AI（M5/M7）
```

**当前阶段**：战斗闭环 + Boss AI + 表现层（UI/音效/震屏）**代码全部完成**。剩余：M8 动画事件接线、M12 相机完善、场景装配（Canvas 布局/挂 View）、调参与全流程验收。

---

## 已完成的模块（代码级）

### ✅ 战斗闭环代码（2026-08-17 批次，全部待验收）

| 模块 | 内容 |
|------|------|
| M8 接线 | `AttackState` 进出开关 `EnableWeaponHit/DisableWeaponHit` + 危字事件 |
| M4 弹反/格挡 | `DeflectState` 重写：短按弹反/长按格挡/抖刀惩罚/危字放行；`ParriedState` 被弹开硬直 |
| M9 架势 | 非线性回复（`PostureDecayInverse`）+ 格挡回复×5 + 崩解 `StaggerBrokenState`（玩家倒地/Boss 处决窗口） |
| M17 横扫 | `AirIdleState` 跳踩反制（Sweep 危字）；`DodgeState` 无敌帧 0.3s |
| M10 处决 | `CombatManager.TryExecuteFinisher` + `FinisherState`（清命，2 命后胜利事件） |
| M16 葫芦 | `HealState` 喝药动画 + 可被打断 |
| M14 复活 | `DeadState`（回生待机/游戏结束）+ 按攻击键复活/重开场景 |
| M11 锁定 | `LockOnManager` + `MoveState` 锁定面向 Boss |
| M5/M7 AI | `Blackboard` + Sequence/Selector Running 记忆 + `BT_Combo/BT_Deflect/BT_BowShot` + 三层 Boss AI 树 |
| 打击感 | `CombatManager.HitStop` 顿帧 |
| 受击接口 | `HurtContext`（Normal/Heavy/Guard/Deflected）+ `CharacterConfig` 受击动画名映射 + `AttackConfig.Knockback` |
| 输入 | 双 Control Scheme（KeyboardMouse/Gamepad）+ Player Input Auto-Switch + 每帧 ReadValue |
| M13 UI | `RevivePromptView/GameOverView/VictoryView` 新增；`CombatUIController` 补全事件（清命/复活/锁定/胜利/死亡）；`PlayerStatusView.SetDanger` 架势高亮；事件总线补 `OnLifeCleared/OnRevived/OnLockOnChanged` |
| M15 音效 | `AudioManager` 补 revive/victory 音效 |
| M12 表现 | `CameraShake`（DoTween 震屏，订阅 OnCameraShake）；弹反/崩解/处决触发 |

### ✅ M1 HFSM 受击路由（代码完成，待验收）

| 文件 | 职责 |
|------|------|
| `FrameWork/States/Base/BaseState.cs` | 基类 + `OnHitReceived(HitData)` 虚方法 |
| `FrameWork/States/Base/HierarchicalState.cs` | 父状态：`OnHitReceived` 转发（父拦截 → 子状态）+ `OnParentHandleHit` |
| `FrameWork/States/StunnedState.cs` | 受击期间二次受击全拦截（防硬直刷新） |
| `FrameWork/Body/CharacterBody.cs` | `ReceiveHit`：打包 HitData → 问 `OnHitReceived` → 未拦截则扣血 + 切 StunnedState |

### ✅ M2 战斗数据（代码完成，待验收）

| 文件 | 职责 |
|------|------|
| `SO/CharacterConfig.cs` | 角色配置 SO：血量/架势/硬直/移动/葫芦/复活 |
| `SO/AttackConfig.cs` | 招式配置 SO：伤害/架势伤害/连招窗口/危字标记 |
| `FrameWork/Body/CharacterBody.cs` | `TakeDamage`/`AccumulatePosture`/`UseGourd`/`Revive`/`ClearLife`/`UpdatePostureDecay` |

### ✅ M3 命中判定（代码完成，待验收）

| 文件 | 职责 |
|------|------|
| `Combat/Hitbox.cs` | SphereCast 扫描（上一帧→当前帧）+ 去重 + 拼刀检测 |
| `Combat/Hurtbox.cs` | 受击盒标记 |
| `Combat/CombatManager.cs` | 中间层单例：`ReportHit`（转发伤害）+ `ReportClash`（拼刀） |

### ✅ M6 输入（代码完成，待验收）

| 文件 | 职责 |
|------|------|
| `Player/Brain/PlayerBrain.cs` | 全按键绑定：Move/Attack/Jump/Deflect/Dodge/Heal/LockOn |
| `Player/Brain/BrainBase.cs` | 指令缓冲池（0.2s 预输入窗口） |
| `Resources/Input/PlayerInputActions.cs` | Input System 自动生成（键鼠+手柄双绑定） |

### ✅ 状态机框架（早期基础，已验收）

`BaseState` / `HierarchicalState` / `StateMachine` / `Command` + 叶子状态 Idle/Move/Attack/Deflect/Dodge/MikiriCounter + 父状态 Grounded/Air/Stunned。

### ✅ 表现层框架（M13 UI + M15 音效，框架级，待建场景）

`CombatEventBus`（+11 个事件）+ `UIView` 基类 + 各 View + `CombatUIController` + `AudioManager`。DoTween 动画已实现（架势条高亮/忍杀红点脉动）。

### ✅ M17 危字/识破（代码已完整，**确认保留**）

| 文件 | 职责 |
|------|------|
| `Configs/PerilousType.cs` | 危字类型枚举 |
| `FrameWork/States/Ground/DodgeState.cs` | 垫步 + 突刺危字→识破触发（`OnHitReceived`） |
| `FrameWork/States/Ground/MikiriCounterState.cs` | 识破（踩刀）状态：涨攻击者架势 |
| `UI/Views/PerilousWarningView.cs` | "危"字提示 |

> 注：早期文档曾写「已移除」，经确认**保留**（用户已解包对应动画）。横扫危字（跳踩反制）尚未实现，见待办。

---

## 待办模块（按推荐顺序）

| 顺序 | 模块 | 内容 | 依赖 |
|------|------|------|------|
| 1 | **M8 攻击接线** | `AttackState` 接 `EnableWeaponHit/DisableWeaponHit`（当前攻击打不出伤害，硬伤） | M3 |
| 2 | **M4 弹反/格挡** | 方案 B 输入（短按弹反/长按格挡）+ 0.3s 窗口 + 抖刀惩罚 + 弹开硬直 + 独立弹反动画 | M1/M3 |
| 3 | **M9 架势** | 非线性回复（格挡 ×5 / Boss 反函数）+ 击飞倒地 + 崩解窗口 | M1/M2 |
| 4 | **M17 横扫** | 横扫危字 + 跳踩反制 | M1/M3 |
| 5 | **M10 处决** | 2 命处决 + 走近按键触发 + 5s 窗口 | M3/M9 |
| 6 | **M16 葫芦** | 喝药动画 + 硬直（会被打断）+ 回满血 | M6 |
| 7 | **M14 复活** | 回生动画 + 无敌帧 + 死亡重开 | M6/M13 |
| 8 | **M11 锁定** | LockOnManager（手动锁定/解锁，单 Boss 不切目标） | M6 |
| 9 | **M12 相机** | Cinemachine FreeLook + TargetGroup + Impulse 震屏 | M11 |
| 10 | **M5 行为树补全** | 黑板 + Running 记忆 | — |
| 11 | **M7 弦一郎 AI** | 三层 AI + 射箭 + 飞渡符舟 + Boss 弹反玩家 | M5/M3 |
| 12 | **打击感** | hitstop + 打铁火花（火花 FX 已有，补停顿） | M3/M4 |

---

## 文档体系

| 文档 | 内容 |
|------|------|
| `Docs/策划案.md` | **设计总纲（权威）**：玩法/机制/数值/范围 |
| `Docs/total.md` | 用户视角决策记录 |
| `Docs/architecture/00-overview.md` | 模块总览 + 依赖图 |
| `Docs/architecture/01-07-*.md` | 各模块架构设计 |
| `Docs/architecture/0X-xxx-test.md` | 各模块验收清单 |
| `Docs/README.md` | 文档导航 |

---

## 验收状态

| 模块 | 代码状态 | 用户验收状态 |
|------|---------|-------------|
| 状态机框架 | ✅ 完成 | ✅ 已验收 |
| M1 受击路由 | ✅ 完成 | ⬜ 待验收 |
| M2 战斗数据 | ✅ 完成 | ⬜ 待验收 |
| M3 命中判定 | ✅ 完成 | ⬜ 待验收 |
| M6 输入 | ✅ 完成 | ⬜ 待验收 |
| M17 危字/识破 | ✅ 完成 | ⬜ 待验收 |
| 表现层框架（UI/音效） | ✅ 框架 | ⬜ 待验收（需建场景） |
| 其余模块 | ⬜ 未做 | — |

---

## 下一步建议

**M8 攻击判定接线**：把 `AttackState` 的攻击帧接上 `EnableWeaponHit/DisableWeaponHit`，让攻击真正能打出伤害——这是当前唯一让游戏"打不出伤害"的硬伤，也是跑通可玩闭环的第一步。
