 复刻只狼战斗系统 | 一场经典boss战：苇名弦一郎
 具体内容看 DOCS/ARPG战斗Demo_项目策划案_只狼.md
 现在的内容是，帮我完成人物模型，动画等不可或缺的基础设置

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

---

## Harness 工程配置

> 本节定义 AI Agent 在本项目中必须遵守的架构约束、代码规范和工作流。

### 架构约束（不可违反）

- 所有战斗参数必须用 `ScriptableObject`，禁止硬编码数值
- 状态机使用 HFSM 模式（继承 `StateMachine` 基类），禁止在 `MonoBehaviour.Update` 中写 switch-case
- 模块间通信走 `CombatEvents` 事件系统，禁止直接引用其他模块
- 弹刀判定用 `Physics.OverlapSphere` 每帧检测，不用 `OnTriggerEnter`
- `Player/` 和 `Boss/` 模块不能直接互相引用，只能通过事件通信

### 代码规范

- 命名规则：类名 `PascalCase`，方法名 `PascalCase`，私有字段 `_camelCase`
- 每个公开方法必须有 XML 文档注释（`/// <summary>`）
- 单个脚本文件不超过 300 行
- 一次提交只做一件事（原子性）

### 测试要求

- 新增数值逻辑 → 必须配套 EditMode 单元测试
- 新增状态转换 → 必须配套 PlayMode 集成测试
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 测试运行方式：Unity Editor → Window → General → Test Runner

### 文档加载指引

当 Agent 接到任务时，根据任务类型加载对应文档：

| 任务类型 | 必读文档 | 可选文档 |
|---------|---------|---------|
| 弹刀系统 | `DOCS/specs/deflect-system.md` | `DOCS/architecture/code-structure.md` |
| 架势条 | `DOCS/specs/posture-system.md` | `DOCS/architecture/data-layer.md` |
| Boss AI | `DOCS/specs/boss-ai.md` | `DOCS/architecture/state-machine.md` |
| 危字/识破 | `DOCS/specs/danger-system.md` | `DOCS/specs/animation-system.md` |
| 雷电反击 | `DOCS/specs/lightning-system.md` | `DOCS/architecture/state-machine.md` |
| 输入系统 | `DOCS/specs/input-system.md` | `DOCS/architecture/code-structure.md` |
| 动画集成 | `DOCS/specs/animation-system.md` | — |
| UI/HUD | `DOCS/specs/ui-hud.md` | — |
| HFSM 框架 | `DOCS/architecture/state-machine.md` | — |
| 数据层 | `DOCS/architecture/data-layer.md` | — |

### 工作流（TDD 强制）

1. 读取相关规格文档（参考上方指引表）
2. 编写测试（定义"完成"的标准）
3. 编写实现代码（遵循架构约束和代码规范）
4. 运行测试，全部通过后提交
5. 运行验证（游戏能否启动，有无编译错误）
