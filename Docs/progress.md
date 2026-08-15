# 项目进度文档

> 复刻只狼弦一郎 Boss 战 | Unity 2022 LTS + URP | 秋招作品集
> 更新日期：2026-08-13
> 本文档记录「已完成的代码模块」与「待办模块」，供快速查阅。详细设计与验收见 `Docs/architecture/` 各模块文档。

---

## 总览

```
战斗核心（M1/M2/M3） ──► 战斗数据（M2/M9/M16/M14）
        │                       │
        ▼                       ▼
  表现层（M12相机/M13UI/M15音效）   Boss AI（M5/M7）
```

**当前阶段**：战斗数据层（M2）已完成，表现层框架（UI/音效）已搭好，接下来进入受击路由（M1）与命中判定（M3）。

---

## 已完成的模块

### ✅ 表现层框架（M13 UI + M15 音效，框架级）

| 文件 | 职责 |
|------|------|
| `Assets/Scripts/Mgr/CombatEventBus.cs` | 事件总线扩展：+9 个表现层事件，**携带完整数据**（hp/maxHp、posture/maxPosture） |
| `Assets/Scripts/UI/Views/UIView.cs` | MVC View 基类：`Show()/Hide()/OnViewInit()` |
| `Assets/Scripts/UI/Views/BossStatusView.cs` | 左上：忍杀灯 + Boss 血条 + 名称 |
| `Assets/Scripts/UI/Views/BossPostureBarView.cs` | 顶部 Boss 架势条（中心双向 + 高亮动画） |
| `Assets/Scripts/UI/Views/PlayerStatusView.cs` | 玩家血条 + 架势 + 回生 |
| `Assets/Scripts/UI/Views/ItemSlotView.cs` | 葫芦槽位（图标 + 数量） |
| `Assets/Scripts/UI/Views/LockOnIndicatorView.cs` | 世界空间锁定点（崩解变红点脉动） |
| `Assets/Scripts/UI/CombatUIController.cs` | MVC Controller：订阅事件 → 按 player/boss 路由到 View |
| `Assets/Scripts/Audio/AudioManager.cs` | 音效：订阅事件播 AudioClip（叮/笃/受击/处决/死亡/葫芦） |

**DoTween 动画**（已实现，替换了 TODO 占位）：
- Boss 架势条 > 80%：颜色变亮 + 边缘尖刺脉动
- 忍杀红点：放大 + 红色脉动

> 已移除：`PerilousType.cs`（危字枚举）、`PerilousWarningView.cs`（"危"字 UI）——M17 不在本项目范围。

> 注意：M13/M15 只搭了**框架**，还需在 Unity 场景里建 UI 组件、把引用拖到 Controller/View 上（验收清单见 `06-presentation-test.md`）。

### ✅ M2 战斗数据层（属性/架势/葫芦/复活 的数据基础）

| 文件 | 职责 |
|------|------|
| `Assets/Scripts/Configs/CharacterConfig.cs` | 角色配置 SO：血量/架势/硬直/移动/葫芦/复活，玩家和 Boss 各配一份 |
| `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | 战斗属性 + 全部结算方法 |

**CharacterBody 新增能力**：
- `TakeDamage()`：扣血 + 涨架势 + 触发事件 + 死亡判定（已死不重复结算）
- `AccumulatePosture()`：涨架势，满则崩解（`IsPostureBroken`）+ 触发事件
- `UpdatePostureDecay()`：停止受击超延迟秒后架势自然回复（崩解中不回复）
- `UseGourd()`：葫芦（有次数且没满血才生效）
- `Revive()`：复活（回满血 + 架势清零）
- `ClearLife()`：处决后清一条命（M10 用）
- `StunDuration`：从 Config 读，容错默认值

> 硬直时长已从 CharacterBody 硬编码迁入 SO（AirStunnedState/GroundStunnedState 同步改为 `body.StunDuration`）。

### ✅ 状态机框架（早期基础，未含受击路由）

| 文件 | 职责 |
|------|------|
| `FrameWork/States/Base/BaseState.cs` | 状态基类：OnEnter/OnUpdate/OnExit/HandleCommand |
| `FrameWork/States/Base/HierarchicalState.cs` | 父状态（HFSM 层级节点） |
| `FrameWork/States/StateMachine.cs` | 状态机驱动器 |
| `FrameWork/States/Command.cs` | Command 接口 + MoveCommand 等 |
| `FrameWork/States/Ground/` | GroundedState + Idle/Move/Attack/Deflect/GroundStunned |
| `FrameWork/States/Air/` | AirState + AirIdleState（跳跃/下落共用） |
| `FrameWork/States/StunnedState.cs` | 顶层受击父状态（统一进 GroundStunnedState） |

> 已移除（无动画资源）：DodgeState（垫步）、AirAttackState/AirDeflectState（空中攻击/格挡）、JumpState/FallState（并入 AirIdleState）、AirStunnedState（空中受击）。

### ✅ 输入/Boss 基础（早期，未完整）

| 文件 | 状态 |
|------|------|
| `Player/Brain/PlayerBrain.cs` + `BrainBase.cs` + `PlayerInputActions.cs` | 移动已通；**Attack/Jump/Defend 等按键未绑定 Command**（待 M6） |
| `Boss/BehaviourTree/`（Node/Selector/Sequence/ConditionNode/BT_Attack/BT_MoveToTarget） | 行为树骨架已建 |
| `Boss/BTBrain.cs` | 简单追近 + 攻击树 |
| `Combat/Hitbox.cs` | M3：动画事件/脚本开启 + SphereCast 扫描（已替换 OnTrigger 版） |
| `Mgr/FXManager.cs` | 打铁火花特效（订阅事件） |

---

## 待办模块（按推荐顺序）

| 顺序 | 模块 | 内容 | 依赖 |
|------|------|------|------|
| 1 | **M1 受击路由** | `ReceiveHit` 里 A3 待办：防御拦截/二次受击的层级查询（`OnHitReceived`） | M2 已完成 |
| 2 | **M3 命中判定** | WeaponHitbox 改 SphereCast，引入 CombatManager 中间层，Hurtbox | M1/M2 |
| 3 | **M9 架势** | 崩解 → EndureState 切换（数据层已就绪，只差状态） | M1/M2 |
| 4 | **M6 输入** | PlayerBrain 绑定 Attack/Jump/Defend 按键 → Command | 输入系统已接入 |
| 5 | **M4 盾反** | DeflectState 盾反窗口/普通格挡逻辑细化 | M1/M3 |
| 6 | **M8 动画事件** | 配置攻击动画帧事件 | 依赖动画配置 |
| 7 | **M10 处决** | 忍杀：崩解窗口 → 清命 → 动画 | M3/M9 |
| 8 | **M14 复活** | 数据层已好，接 UI 提示 + R 键确认 | M6/M13 |
| 9 | **M16 葫芦** | 数据层已好，接 HealCommand + 喝药状态 | M6 |
| 10 | **M11 锁定** | LockOnManager：锁定目标切换 | M6 |
| 11 | **M12 相机** | Cinemachine（包已装）：FreeLook 自由 + TargetGroup 锁定 + Impulse 震屏 | M11 |
| 12 | **M5 行为树完善** | 黑板 + Running 记忆 | — |
| 13 | **M7 弦一郎 Boss AI** | 三层结构（主动/交锋/变招防御），参考 `Docs/references/sekiro-genichiro-ai.md` | **最后做**，依赖 M5/M3 及大部分战斗系统 |

> 已移除：M17 危字/识破（无动画资源）。

---

## 文档体系

| 文档 | 内容 |
|------|------|
| `Docs/total.md` | 用户视角决策记录（框架决策 1-11） |
| `Docs/architecture/00-overview.md` | 模块总览 + 依赖图 |
| `Docs/architecture/01-07-*.md` | 各模块架构设计 |
| `Docs/architecture/0X-xxx-test.md` | 各模块验收清单（用户在 Unity 中逐条验证） |
| `Docs/README.md` | 文档导航（待补模块总表） |
| `CLAUDE.md` | 架构约束 + 代码规范 + 文档加载指引 |

---

## 验收状态

| 模块 | 代码状态 | 用户验收状态 |
|------|---------|-------------|
| 状态机框架 | ✅ 完成 | ✅ 已验收 |
| 表现层框架（UI/音效） | ✅ 完成 | ⬜ 待验收（需建场景） |
| M2 战斗数据层 | ✅ 完成 | ⬜ 待验收（见下方） |
| 其余模块 | ⬜ 未做 | — |

**M2 验收清单**（`Docs/architecture/02-combat-data-test.md`）：需在 Project 建 PlayerConfig/GenichiroConfig 两个 SO 资源 → 拖进对应 CharacterBody 的 Config 槽 → 逐条验证属性初始化/架势/葫芦/复活。

---

## 下一步建议

**M1 受击路由**：把 `CharacterBody.ReceiveHit()` 里的 A3 待办实现掉——`BaseState.OnHitReceived` + `HierarchicalState` 转发，DeflectState/DodgeState 重写拦截。这是战斗核心的入口，其余模块都依赖它。
