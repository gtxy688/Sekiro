
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

# 架构约束（可判定红线，违反即打回）

1. **战斗参数必须用 ScriptableObject**。`.cs` 中不得出现：`new AttackData{...}`、`private const` 战斗数值、`static readonly` 权重表、内联伤害/时长数值。参数一律来自 `.asset` 资产。
2. **状态机继承 `StateMachine` 基类**。禁止在 `MonoBehaviour.Update` 中写 switch-case 状态机。
3. **模块间通信走 `CombatEvents` 事件总线**。`Player/` 与 `Boss/` 目录类禁止直接引用对方类型，禁止 `FindObjectOfType` 跨模块获取。
4. **命中判定用 `Physics.OverlapSphere`**。禁止 `OnTriggerEnter/OnTriggerStay/OnCollisionEnter` 做攻击判定。
5. **单一输入系统**。禁止新旧输入系统混用（新 Input System 资产 + `UnityEngine.Input.*` 不得并存）。

# 代码规范

- 类名/方法名 `PascalCase`，私有字段 `_camelCase`
- 每个公开方法必须有 `/// <summary>` XML 注释
- 单文件 ≤ 300 行
- 一次提交只做一件事（原子提交）

# 工作流（AI 实现 → 用户验收）

1. 收到任务 → 按「文档加载指引」读对应 spec，只读需要的章节
2. 实现代码 → 不自己宣称"完成"
3. 在交付说明中附「验收清单」：操作步骤 + 预期结果（供用户在 Unity 中逐条验证）
4. 用户验收通过 → 提交；验收不通过 → 修正后重新交付

# 文档访问纪律（硬规则）

1. 收到任务 → 在下方加载指引表定位对应 spec，**只读该文件**，禁止通读 `Docs/` 下所有文档
2. 禁止跨模块引用其他 spec（改弹刀就读 deflect 相关，不读 boss/ui）
3. 文档与代码冲突 → **停下报告，不自行选边**（如：specs 说 HFSM 而代码是 FSM）
4. 遇到缺失的 spec 或字段 → 停下询问用户，不臆造

# 文档加载指引

当 Agent 接到任务时，根据任务类型加载对应文档（每文件 15-30 行，只读精确需要的）：

| 任务类型 | 必读文档 |
|---------|---------|
| 弹刀判定 | `Docs/specs/deflect/deflect-mechanics.md` |
| 抖刀惩罚/加成链 | `Docs/specs/deflect/deflect-penalties.md` |
| 弹刀参数/验收 | `Docs/specs/deflect/deflect-params.md` |
| 架势条规则 | `Docs/specs/posture/posture-rules.md` |
| 架势参数/验收 | `Docs/specs/posture/posture-params.md` |
| Boss 状态机 | `Docs/specs/boss/boss-state-machine.md` |
| Boss AI 决策+权重表 | `Docs/specs/boss/boss-ai-decision.md` |
| Boss 招式表 | `Docs/specs/boss/boss-attacks.md` |
| Boss 弹刀 AI | `Docs/specs/boss/boss-deflect.md` |
| Boss 数值/验收 | `Docs/specs/boss/boss-params.md` |
| 危字类型+提示 | `Docs/specs/danger/danger-types.md` |
| 识破+踩头判定 | `Docs/specs/danger/mikiri-stomp.md` |
| 危字参数/验收 | `Docs/specs/danger/danger-params.md` |
| 输入优先级+打断 | `Docs/specs/input/input-priority.md` |
| 输入缓冲 | `Docs/specs/input/input-buffer.md` |
| 帧冻结 | `Docs/specs/input/hitstop.md` |
| 回血系统 | `Docs/specs/input/healing.md` |
| 动画策略+事件 | `Docs/specs/animation/animation-strategy.md` |
| 动画参数/验收 | `Docs/specs/animation/animation-params.md` |
| UI 布局+元素 | `Docs/specs/ui/ui-overview.md` |
| UI 事件映射 | `Docs/specs/ui/ui-events.md` |
| 伤害数字 | `Docs/specs/ui/ui-damage-numbers.md` |
| HFSM 框架 | `Docs/architecture/state-machine.md` |
| 数据层 | `Docs/architecture/data-layer.md` |
| 代码结构 | `Docs/architecture/code-structure.md` |
| 测试计划 | `Docs/testing/test-plan.md` |

# 测试要求

- 新增数值逻辑 → 必须配套 EditMode 单元测试（Assets/Tests/EditMode）
- 新增状态转换 → 必须配套 PlayMode 集成测试（Assets/Tests/PlayMode）
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 测试运行方式：Unity Editor → Window → General → Test Runner
