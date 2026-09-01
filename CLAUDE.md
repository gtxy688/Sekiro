
<!-- superpowers-zh:begin (do not edit between these markers) -->
# Superpowers-ZH 中文增强版

本项目已安装 superpowers-zh 技能框架（20 个 skills）。

## 核心规则

1. **收到任务时，先检查是否有匹配的 skill** — 哪怕只有 1% 的可能性也要检查
2. **设计先于编码** — 收到功能需求时，先用 brainstorming skill 做需求分析
3. **测试先于实现** — 写代码前先写测试（TDD）
4. **验证先于完成** — 声称完成前必须运行验证命令

## 可用 Skills

Skills 位于 `.claude/skills/` 目录，每个 skill 有独立的 `SKILL.md` 文件。

- **brainstorming**: 在任何创造性工作之前必须使用此技能——创建功能、构建组件、添加功能或修改行为。在实现之前先探索用户意图、需求和设计。
- **chinese-code-review**: 中文 review 沟通参考——话术模板、分级标注（必须修复/建议修改/仅供参考）、国内团队常见反模式应对。仅在用户显式 /chinese-code-review 时调用，不要根据上下文自动触发。
- **chinese-commit-conventions**: 中文 commit 与 changelog 配置参考——Conventional Commits 中文适配、commitlint/husky/commitizen 中文模板、conventional-changelog 中文配置。仅在用户显式 /chinese-commit-conventions 时调用，不要根据上下文自动触发。
- **chinese-documentation**: 中文文档排版参考——中英文空格、全半角标点、术语保留、链接格式、中文文案排版指北约定。仅在用户显式 /chinese-documentation 时调用，不要根据上下文自动触发。
- **chinese-git-workflow**: 国内 Git 平台配置参考——Gitee、Coding.net、极狐 GitLab、CNB 的 SSH/HTTPS/凭据/CI 接入差异与镜像同步配置。仅在用户显式 /chinese-git-workflow 时调用，不要根据上下文自动触发。
- **dispatching-parallel-agents**: 当面对 2 个以上可以独立进行、无共享状态或顺序依赖的任务时使用
- **executing-plans**: 当你有一份书面实现计划需要在单独的会话中执行，并设有审查检查点时使用
- **finishing-a-development-branch**: 当实现完成、所有测试通过、需要决定如何集成工作时使用——通过提供合并、PR 或清理等结构化选项来引导开发工作的收尾
- **mcp-builder**: MCP 服务器构建方法论 — 系统化构建生产级 MCP 工具，让 AI 助手连接外部能力
- **receiving-code-review**: 收到代码审查反馈后、实施建议之前使用，尤其当反馈不明确或技术上有疑问时——需要技术严谨性和验证，而非敷衍附和或盲目执行
- **requesting-code-review**: 完成任务、实现重要功能或合并前使用，用于验证工作成果是否符合要求
- **subagent-driven-development**: 当在当前会话中执行包含独立任务的实现计划时使用
- **systematic-debugging**: 遇到任何 bug、测试失败或异常行为时使用，在提出修复方案之前执行
- **test-driven-development**: 在实现任何功能或修复 bug 时使用，在编写实现代码之前
- **using-git-worktrees**: 当需要开始与当前工作区隔离的功能开发，或在执行实现计划之前使用——通过原生工具或 git worktree 回退机制确保隔离工作区存在
- **using-superpowers**: 在开始任何对话时使用——确立如何查找和使用技能，要求在任何响应（包括澄清性问题）之前调用 Skill 工具
- **verification-before-completion**: 在宣称工作完成、已修复或测试通过之前使用，在提交或创建 PR 之前——必须运行验证命令并确认输出后才能声称成功；始终用证据支撑断言
- **workflow-runner**: 在 Claude Code / OpenClaw / Cursor 中直接运行 agency-orchestrator YAML 工作流——无需 API key，使用当前会话的 LLM 作为执行引擎。当用户提供 .yaml 工作流文件或要求多角色协作完成任务时触发。
- **writing-plans**: 当你有规格说明或需求用于多步骤任务时使用，在动手写代码之前
- **writing-skills**: 当创建新技能、编辑现有技能或在部署前验证技能是否有效时使用

## 如何使用

当任务匹配某个 skill 时，使用 `Skill` 工具加载对应 skill 并严格遵循其流程。绝不要用 Read 工具读取 SKILL.md 文件。

如果你认为哪怕只有 1% 的可能性某个 skill 适用于你正在做的事情，你必须调用该 skill 检查。
<!-- superpowers-zh:end -->

# 【本项目覆盖声明】优先级高于上方 superpowers 通用规则

上方 superpowers-zh 的以下条款在本项目**不适用**；冲突时一律以本项目规范为准：

| superpowers 条款 | 本项目做法 |
|---|---|
| 「测试先于实现（TDD）」 | **不适用**。本项目不做自动化测试，见文末「测试要求」 |
| 「验证先于完成 — 必须运行验证命令」 | **不适用**。项目无测试/构建验证命令。验证方式唯一：交付「验收清单」+ 用户在 Unity 中手动逐条验收 |
| 「哪怕 1% 可能也要调用 skill」 | **收窄**。仅下列 4 个可用：`brainstorming`（需求设计）、`systematic-debugging`（排查 bug）、`requesting-code-review`（交付前自检）、`using-git-worktrees`（隔离开发） |

**禁止调用**与本项目无关的 skill，包括但不限于：`test-driven-development`、`verification-before-completion`、`chinese-git-workflow`、`chinese-commit-conventions`、`chinese-documentation`、`mcp-builder`、`workflow-runner`。

> 维护提示：本声明位于 superpowers 标记块**之外**，不会被 superpowers 重新安装覆盖。若重装 superpowers 后此段消失，必须补回。

# 项目概述

复刻只狼战斗系统 | 一场经典boss战：苇名弦一郎
> 引擎：Unity 2022 LTS + URP | 用途：秋招作品集

AI 辅助开发，**用户负责测试与验收（按模块）**。AI 每完成一个模块，交付「验收清单」供用户逐条验证，验收通过才算完成。

# 架构约束

1. **HFSM 顶层只装 HierarchicalState**：GroundedState/AirState/StunnedState 是父状态。叶子状态（Idle/Move/Attack/Deflect/Dodge/Mikiri）永远在父状态 SubStateMachine 内。
2. **禁止在业务代码直接判顶层状态类型**：`MainStateMachine.CurrentState is DodgeState` 永远 false，禁止。需要查询/切入顶层地面态时，一律走 `CharacterBody` 的封装 API（`IsGroundedTop` / `IsInGroundedSubState<T>` / `TryChangeGroundedSubState` / `ForceChangeGroundedSubState`），顶层类型判定只允许存在于 CharacterBody 内部。叶子状态查询走 OnHitReceived / 层级路由。
3. **命中判定不用 OnTrigger**：用动画事件 + Physics.BoxCast/SphereCast（上一帧位置→当前帧位置扫描）。
4. **伤害数据归属 AttackConfig（SO）**：不在 WeaponHitbox 等其他地方重复硬编码伤害值。
5. **战斗数值全部走 SO**：不硬编码在 .cs 里。角色属性 → CharacterConfig，招式属性 → AttackConfig。
6. **Hit 结算走 CombatManager 中间层**：Hitbox 扫到 Hurtbox → 报告 CombatManager → CombatManager 调 target.ReceiveHit。不直接调。
7. **Command 路由规范**：Command 来自 Brain（输入/AI），Hit 来自物理碰撞，都走状态机路由。环境/物理强制切换（受击）不走 Command，直接 ChangeState。
8. **表现层只用事件总线**：UI/相机/音效订阅 CombatEventBus，禁止每帧轮询。

# 代码规范

## 命名
- 公开成员 PascalCase，私有 camelCase（Unity 习惯，不用 m_ 前缀）
- 命令 struct 命名 `<动作>Command`（MoveCommand/AttackCommand/HealCommand）
- 状态类命名 —— **以代码现实为准**，新增状态沿用所在目录的现有习惯：
  - 父状态继承 `HierarchicalState`，无区域前缀：`GroundedState`(`States/Ground/`) / `AirState`(`States/Air/`) / `DeadState`(`States/Dead/`) / `StunnedState`（**在 `States/` 根目录**，不在任何子目录）
  - 叶子状态继承 `BaseState`：`States/Ground/` 下**不加前缀**（`IdleState`/`MoveState`/`AttackState`/`DeflectState`/`DodgeState`/`HealState`）；`States/Air/` 下**加 `Air` 前缀**（`AirIdleState`/`AirAttackState`）
  - 理由：Ground 是默认域故省略前缀，Air 是特例故显式标注
  - **`GroundStunnedState` 是历史例外**（位于 `Ground/` 却带前缀），新增状态不要模仿；规范里曾出现的 `AirStunnedState` 代码中不存在，勿用
  - 别混淆：顶层父状态 `StunnedState`（`States/StunnedState.cs`）与地面叶子状态 `GroundStunnedState`（`States/Ground/`）是两个不同的类

## 文件组织
- 状态机：`Assets/Scripts/FrameWork/States/<区域>/`（顶层父状态 `StunnedState` 直接在 `States/` 根）
- 战斗：`Assets/Scripts/Combat/`
- 配置 SO：`Assets/Scripts/SO/`（`AttackConfig` / `CharacterConfig` / `AttackCombatResolve`）
- 配置枚举与轻量数据：`Assets/Scripts/Configs/`（`HitGrade` / `PerilousType` / `AttackHitboxSlot`）
- 大脑：`Assets/Scripts/Boss/`（含 `BehaviourTree/`）、玩家侧 `Assets/Scripts/Player/Brain/`
- 玩家输入与控制：`Assets/Scripts/Player/Input/`、`Assets/Scripts/Player/Control/`
- 表现：`Assets/Scripts/UI/`（含 `Views/`）、`Assets/Scripts/Audio/`、`Assets/Scripts/Camera/`
- 管理器：`Assets/Scripts/Mgr/`
- 编辑器扩展：`Assets/Editor/`（第三方插件的 Editor 目录勿动）
- 命名空间：全部代码位于 `ARPG.*`（与文件夹结构对齐：Audio→`ARPG.Audio`、Boss→`ARPG.Boss`(`BehaviourTree` 子目录→`ARPG.Boss.BehaviourTree`)、Camera→`ARPG.Camera`、Combat→`ARPG.Combat`、Configs+SO→`ARPG.Configs`、FrameWork→`ARPG.FrameWork`(`Body`→`ARPG.FrameWork.Body`，`States`→`ARPG.FrameWork.States.{Ground,Air,Dead,Base}`)、Mgr→`ARPG.Mgr`、Player→`ARPG.Player`、UI→`ARPG.UI`）。新增脚本必须放入对应命名空间，跨模块类型引用一律 `using ARPG.X;`，禁止在全局命名空间新增类型。

## 数据结构
- 配置数据用 ScriptableObject（AttackConfig/CharacterConfig），带 `[CreateAssetMenu]`
- 命令/Hit 数据用 struct（值类型，避免引用语义）
- 状态是纯 C# 类（非 MonoBehaviour），CharacterBody 是 MonoBehaviour

## 注释
- 中文注释，解释"为什么"而非"是什么"
- 占位动画名（如 "Hurt_Ground"）注明「M8 接动画前为占位名」

## 编辑器扩展（`Assets/Editor/`）

编辑器代码同样受本文件全部约束，另守以下 6 条：

1. **改序列化字段一律走 `SerializedObject` + `SerializedProperty`**，禁止直接给 SO/组件字段赋值——否则 Undo 失效、改动不持久化、Prefab 覆盖丢失
2. **临时修改必须还原**：为读像素开 `isReadable`、改压缩格式这类操作，用完要恢复原值（提前 return 的分支也要走到还原）。历史教训：工具每次跑都把贴图设成可读且不还原
3. **UI 状态用 `[SerializeField]` 保存**（展开/折叠、选中项等），否则每次 domain reload（改代码回 Unity）都会被重置
4. **`AssetPostprocessor` 只在首次导入（`importSettingsMissing`）时接管设置**，不要无条件覆写——会动到已有资源
5. **`OnGUI` 里不做重活**（不遍历全项目资源、不读写大文件），结果要缓存；耗时操作给用户进度条或 Log，不要静默跑几十秒
6. **新增校验规则写进 `Assets/Editor/ArpgValidationRules.cs`**，窗口与构建校验共用同一份，**禁止另起一套规则**（两套规则必然漂移）

# 工作流（AI 实现 → 用户验收）

1. 收到任务 → 按「文档加载指引」读对应架构文档 + spec，只读需要的章节
2. 实现代码 → 不自己宣称"完成"
3. 在交付说明中附「验收清单」：操作步骤 + 预期结果（供用户在 Unity 中逐条验证）
4. 用户验收通过 → 提交；验收不通过 → 修正后重新交付

# 文档访问纪律（硬规则）

1. 收到任务 → 在下方加载指引表定位对应架构文档，**只读该文件**，禁止通读 `Docs/` 下所有文档
2. 禁止跨模块引用其他文档（改弹刀就读 01-states，不读 06-presentation）
3. **读完后声明**：在交付说明中写明「本任务依据 `Docs/architecture/0X-xxx.md`，未读其他文档」。**不需要动手前停下来等确认**——那只会把一次交互变成两次往返，且复述表格内容没有信息增量；读完说清楚即可追溯
4. 文档与代码冲突 → **停下报告，不自行选边**（如：架构文档说 HFSM 而代码是 FSM）
5. 遇到缺失的文档或字段 → 停下询问用户，不臆造

# 文档加载指引

架构文档在 `Docs/architecture/`，每个模块配 `XX-xxx.md`（架构）+ `XX-xxx-test.md`（验收）。AI 只读与本任务相关的文件。

| 任务类型 | 读这个 |
|---------|--------|
| 状态机/受击/弹反/闪避/处决 | `Docs/architecture/01-states.md` |
| 属性/架势/葫芦/复活 | `Docs/architecture/02-combat-data.md` |
| 命中判定/危字攻击 | `Docs/architecture/03-hit-detection.md` |
| 行为树/Boss AI | `Docs/architecture/04-behavior-tree-ai.md` |
| 输入/锁定 | `Docs/architecture/05-input-lockon.md` |
| 相机/UI/音效 | `Docs/architecture/06-presentation.md` |
| 动画事件 | `Docs/architecture/07-anim-events.md` |
| 项目总览/模块依赖 | `Docs/architecture/00-overview.md` |
| 跨模块实现决策（弹反模型/攻击接线/hitstop/射箭/架势回复） | `Docs/实现指导.md` |
| 用户决策记录 | `Docs/total.md`（用户视角，AI 不当规范用） |

> 总原则：先读 `Docs/architecture/00-overview.md` 定位模块，再读对应架构文档，最后对照 `XX-xxx-test.md` 验收。

> 模块清单（17 个）+ 依赖图 + 关键实现决策速查：`.codebuddy/skills/arpg-constraints/references/architecture-index.md`。开发新模块前先在此定位模块归属，再按上表读文档。



# 测试要求

本项目**不做自动化测试**：不写单元测试，不建测试程序集，不要求 AI 运行验证命令。

验证方式：AI 交付时附「验收清单」（`Docs/architecture/0X-xxx-test.md`），用户在 Unity 中手动逐条验收。

> 注意：`0X-xxx-test.md` 里的 `-test` 指**手动验收清单**，不是自动化测试。禁止据此编写测试代码或测试程序集。

# 文档冲突处理

已知历史冲突：`Docs/architecture/` 中 01/04/05/06/07 曾与最新决策不一致。

- **需求与数值的唯一权威 = `Docs/策划案.md`**（关键实现决策另见 `Docs/实现指导.md`）
- 文档之间、或文档与代码冲突 → **停下报告，列出冲突点让用户裁定，不自行选边**
- 缺失的文档或字段 → **停下询问，不臆造**

# 规范维护纪律（AI 必读）

本项目的完整规范**只存在于本文件**（`CLAUDE.md`），是唯一事实源：

- `CODEBUDDY.md` 只记 WorkBuddy 环境差异（MCP 工具等），**不复制本文件内容**
- `.codebuddy/skills/arpg-constraints/SKILL.md` 只是引导入口，**不复制本文件内容**
- 修改规范**只改本文件**。若发现上述两处与本文件不一致，说明副本已过期，报告用户而非自行同步
