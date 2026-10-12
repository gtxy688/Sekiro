# 项目协作说明

本文件是本项目唯一的 Agent / AI 项目指引。当前实现事实以 Assets/Scripts、Assets/Editor、Unity 序列化资源和场景为准。Docs 用于说明和导航；与代码不一致时，先修订活跃文档，再按用户需求处理代码行为。历史计划、评审记录和用户决策记录不作为当前实现规范。

## 项目概况

《只狼》弦一郎 Boss 战复刻，Unity 2022.3 LTS + URP，面向个人作品集。AI 协助开发和源码梳理，用户按模块在 Unity 中手动验收。

## 当前架构事实

- 玩家意图来自 Unity Input System。PlayerBrain 处理输入；连续移动直接送入 CharacterBody，离散动作经 BrainBase 的缓冲后作为 Command 路由。LockOn 由 LockOnManager 处理。
- Boss 使用行为树与 Blackboard 决策。BossMovePicker 从 BossMoveTable 选择招式；BT_ExecuteMove 按招式段驱动角色攻击状态。
- CharacterBody 是角色运行时 MonoBehaviour，持有 MainStateMachine。HFSM 顶层是 HierarchicalState 父状态：GroundedState、AirState、StunnedState、DeadState；叶子状态属于父状态各自的 SubStateMachine。
- AttackStateBase 读取 AttackAnimClock 的动画时间，根据 AttackConfig 或 BossMoveWindow 推进攻击窗口、音效、出箭与连招。
- Hitbox 使用 Physics 查询；当前使用 SphereCastAll 扫描，并有 OverlapSphereNonAlloc / OverlapCapsuleNonAlloc 查询，不使用 OnTrigger 作为近战命中主流程。
- Hitbox 与箭矢把命中交给 CombatManager / CombatResolver，再调用目标 CharacterBody.ReceiveHit。不得从 Hitbox 直接结算角色伤害。
- CharacterConfig、AttackConfig 是 ScriptableObject。BossMoveTable 也承载招式数据；AttackCombatResolve 含按 HitGrade / 箭矢类别计算的代码默认值。不能笼统写“所有战斗数值都只来自 SO”。
- CombatEventBus 将多类战斗事件通知给 UI、音效和相机订阅方。不要把项目描述成完整 MVC，也不要在没有核对源码时声称表现层完全没有轮询。

## 架构约束

1. MainStateMachine 顶层只装 HierarchicalState 父状态。GroundedState、AirState、StunnedState、DeadState 是父状态；叶子状态留在父状态 SubStateMachine 内。
2. 业务代码不得用 MainStateMachine.CurrentState 判断叶子状态。查询或切换地面父状态 / 子状态时，优先使用 CharacterBody 的 IsGroundedTop、IsInGroundedSubState<T>、TryChangeGroundedSubState、ForceChangeGroundedSubState 等封装 API。顶层父状态类型判断只允许留在 CharacterBody 的封装内部。
3. 命中判定使用当前 Hitbox / ArrowProjectile 查询流程；近战主流程禁止改成 OnTrigger。近战时间由攻击状态和配置窗口推进，不要把 Clip Animation Event 当作 Hitbox 必需开关。
4. 命中数据由 AttackConfig、BossMoveTable 及各自窗口 / cue 提供。代码默认值存在时应明确说明，不要声称没有硬编码兜底。
5. 命中由 Hitbox / ArrowProjectile 报告给 CombatManager，再经 CombatResolver 到目标 ReceiveHit；不允许攻击框直接结算目标。
6. 玩家 / AI 离散 Command 与物理 Hit 走状态机路由；环境或受击造成的强制状态切换按 CharacterBody 当前入口直接切换，不伪装成玩家 Command。
7. 表现系统按需订阅 CombatEventBus；新增订阅要在组件停用时解绑。具体 UI、相机和音频路径以当前组件为准。

## 命名与数据结构

- 公开成员 PascalCase，私有成员 camelCase；不使用 m_ 前缀。
- Command 使用“动作名 + Command”命名，如 MoveCommand、AttackCommand。
- 状态是普通 C# 类；CharacterBody 是 MonoBehaviour。Command / Hit 数据按现有类型定义使用值类型。
- 状态类命名以当前目录现实为准：
  - 父状态继承 HierarchicalState：GroundedState 在 States/Ground，AirState 在 States/Air，DeadState 在 States/Dead，StunnedState 在 States 根目录。
  - Ground 叶子状态通常不加区域前缀，如 IdleState、MoveState、AttackState、DeflectState、DodgeState、HealState。
  - Air 叶子状态通常加 Air 前缀，如 AirIdleState、AirAttackState。
  - GroundStunnedState 是历史命名例外；新增状态不照抄。StunnedState 与 GroundStunnedState 是两个不同类。
- 新增配置优先沿用现有 ScriptableObject / 轻量数据类型；不要为了统一外观把身份枚举、运行时状态或命令都改成 SO。
- 注释使用中文，优先解释决策原因。尚未替换的占位动画名必须注明占位状态。

## 目录与命名空间

- 状态机：Assets/Scripts/FrameWork/States/<区域>/
- 战斗：Assets/Scripts/Combat/
- SO：Assets/Scripts/SO/，主要为 AttackConfig、CharacterConfig
- 配置枚举 / 轻量数据：Assets/Scripts/Configs/
- Boss AI：Assets/Scripts/Boss/BehaviourTree/，命名空间 ARPG.Boss.BehaviourTree
- 玩家输入 / 控制：Assets/Scripts/Player/Input/、Assets/Scripts/Player/Control/
- 表现：Assets/Scripts/UI/、Audio/、Camera/
- 管理器：Assets/Scripts/Mgr/
- 编辑器扩展：Assets/Editor/，第三方插件自身 Editor 目录不随意修改
- 新类型放入对应 ARPG.* 命名空间，跨模块引用显式 using 对应命名空间。

## 编辑器扩展约定

以下规则用于新增或修改工具；它们是维护约定，不代表仓库所有旧工具已经完成迁移：

1. 修改序列化字段优先用 SerializedObject / SerializedProperty，确保 Undo、序列化和 Prefab 覆盖可用。
2. 为临时像素读取或导入处理改变资源设置时，结束前恢复原值，所有提前退出路径也要还原。
3. 需要跨 Domain Reload 保留的窗口 UI 状态用序列化字段或项目现有持久化方式保存。
4. AssetPostprocessor 只在 importSettingsMissing 等首次导入条件满足时接管设置，不无条件覆盖已有资源。
5. OnGUI 不做全项目遍历或大文件读写；重操作需缓存结果并提供进度或日志反馈。
6. 新增校验规则放入 Assets/Editor/ArpgValidationRules.cs，与窗口和构建校验共用。

## 文档与验收工作流

1. 文档入口为 Docs/README.md；先定位对应模块，只读完成当前任务所需的说明与手动验收清单。
2. 代码 / 资源 / 场景用于描述当前实现；历史设计和计划只能作为背景。明确区分“已实现”“计划中”和“用户已验收”。
3. 代码存在不等于 Unity 场景可运行，也不等于用户验收通过。交付模块时附对应手动验收清单和预期结果。
4. 项目以 Unity 手动验收作为模块验收流程。当前仓库存在 Assets/Tests/Editor/CharacterBodyHfsmFlagTests.cs；不能宣称仓库完全没有测试代码，也不能据此宣称已建立完整自动化测试体系。
5. 未经用户要求，不新增自动化测试或声称编译 / 测试通过；此工作流不代替用户在 Unity 中验收。
6. 文档与明确需求冲突时，报告冲突和影响；描述当前代码事实时以实际源码为准。
7. Docs/README.md 标记为历史 / 参考的内容不作为当前规范。Docs/策划案.md 与 .codebuddy 路径当前不存在，不得继续引用为有效规范入口。

## 可用项目技能

仅在确实匹配任务时使用：brainstorming、systematic-debugging、requesting-code-review、using-git-worktrees。不要为普通文档阅读强行调用功能开发流程。
