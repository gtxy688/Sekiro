
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
- 状态类命名 `<区域><动作>State`（GroundStunnedState/AirStunnedState）

## 文件组织
- 状态机：`Assets/Scripts/FrameWork/States/<区域>/`
- 战斗：`Assets/Scripts/Combat/`
- 配置：`Assets/Scripts/Configs/`
- 大脑：`Assets/Scripts/Player/Brain/`、`Assets/Scripts/Boss/`
- 表现：`Assets/Scripts/UI/`、`Assets/Scripts/Audio/`、`Assets/Scripts/Camera/`
- 管理器：`Assets/Scripts/Mgr/`
- 命名空间不强制，保持现有风格（无命名空间）

## 数据结构
- 配置数据用 ScriptableObject（AttackConfig/CharacterConfig），带 `[CreateAssetMenu]`
- 命令/Hit 数据用 struct（值类型，避免引用语义）
- 状态是纯 C# 类（非 MonoBehaviour），CharacterBody 是 MonoBehaviour

## 注释
- 中文注释，解释"为什么"而非"是什么"
- 占位动画名（如 "Hurt_Ground"）注明「M8 接动画前为占位名」

# 工作流（AI 实现 → 用户验收）

1. 收到任务 → 按「文档加载指引」读对应架构文档 + spec，只读需要的章节
2. 实现代码 → 不自己宣称"完成"
3. 在交付说明中附「验收清单」：操作步骤 + 预期结果（供用户在 Unity 中逐条验证）
4. 用户验收通过 → 提交；验收不通过 → 修正后重新交付

# 文档访问纪律（硬规则）

1. 收到任务 → 在下方加载指引表定位对应架构文档，**只读该文件**，禁止通读 `Docs/` 下所有文档
2. 禁止跨模块引用其他文档（改弹刀就读 01-states，不读 06-presentation）
3. **动手前先声明**：先说出"本任务要读哪份文档"，再开始读取，让用户确认找对了文档
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
| 用户决策记录 | `Docs/total.md`（用户视角，AI 不当规范用） |

> 总原则：先读 `Docs/architecture/00-overview.md` 定位模块，再读对应架构文档，最后对照 `XX-xxx-test.md` 验收。



# 测试要求

本项目不做自动化测试。验证方式：AI 交付时附「验收清单」（`Docs/architecture/0X-xxx-test.md`），用户在 Unity 中手动逐条验收。
