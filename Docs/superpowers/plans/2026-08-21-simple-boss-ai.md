# 简单 Boss AI 修通实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框跟踪。

**目标：** 让现有简单 Boss AI 能追击、单刀攻击、短按格挡并正确锁定玩家，同时让识破架势崩解播放 `Stagger_Broken_Miriki`。

**架构：** 保留现有三分支行为树，不引入完整三层 AI。新增角色级 `CombatTarget`，公共移动和攻击状态优先使用该目标，BossBrain 负责注入玩家目标。配置层启用 Brain 并改用 Boss 专用 Attack1，Hitbox 由用户在右手刀骨骼下手动配置。

**技术栈：** Unity 2022.3、C#、HFSM、行为树、ScriptableObject、根运动。

---

### 任务 1：修正简单 AI 的目标与冷却

**文件：**
- 修改：`Assets/Scripts/Boss/BehaviourTree/Blackboard.cs`
- 修改：`Assets/Scripts/Boss/BTBrain.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/MoveState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`

- [x] 冷却 key 不存在时让 `IsOnCooldown` 返回 false。
- [x] 在 `CharacterBody` 增加 `CombatTarget`。
- [x] `BTBrain.Start` 校验并注入玩家目标；`OnDisable` 清空 AI 移动意图。
- [x] `MoveState` 用角色自己的战斗目标计算朝向与四向参数，AI 世界输入不经玩家相机换算。
- [x] `AttackState` 优先追踪 `CombatTarget`，玩家仍回退到 `LockOnManager`。
- [x] Unity 编译，Console 预期 0 error。

### 任务 2：修正 Boss Prefab 基础配置

**文件：**
- 修改：`Assets/Prefabs/Boss.prefab`

- [x] 启用 `BTBrain`。
- [x] 把 `CharacterBody.LightAttack` 改为 Boss `Attack1.asset`。
- [x] 保留场景已有 `PlayerTarget` / `PlayerBody` override。
- [x] 不代挂 Hitbox；由用户在 `R_Katana` 下创建刀刃采样点。

### 任务 3：识破崩解特殊动画

**文件：**
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改：`Docs/architecture/01-states.md`
- 修改：`Docs/architecture/04-behavior-tree-ai-test.md`

- [x] `PostureBreakSource.Mikiri` 使用 `Stagger_Broken_Miriki`。
- [x] 保持识破忍杀确认窗口、红点和超时恢复逻辑不变。
- [x] 强制刷新 Unity 并编译；Console 预期 0 error。
- [x] 交付追击、攻击、格挡、锁定与识破崩解的手动验收清单。
