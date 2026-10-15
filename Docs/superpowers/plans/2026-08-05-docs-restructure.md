# 文档体系重构实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 重构文档体系，使 CLAUDE.md 规则可判定、消除大小写分裂与文档-代码冲突。

**架构：** 按 L0-L4 分层（规则/架构/规格/参考/计划）。CLAUDE.md 改为短硬规则 + 文档访问纪律；22 份 specs 中 21 份保留整理、2 份重写；统一目录大小写为 `Docs`。

**技术栈：** Markdown、git、Unity 项目文档。

**执行前置条件：**
- 设计规格已批准：`docs/superpowers/specs/2026-08-05-docs-restructure-design.md`
- 本计划所有文件路径以 `Docs/` 为准（任务 0 完成改名后生效）
- 本任务不修改任何代码，仅改文档 + git 元数据

---

## 文件结构（锁定分解决策）

| 文件 | 职责 | 处理 |
|------|------|------|
| `CLAUDE.md` | L0 规则层：可判定架构约束 + 代码规范 + 文档访问纪律 + 加载指引 | 重写 |
| `Docs/README.md` | 总导航：模块→spec 总表 | 新建 |
| `Docs/architecture/*` | L1 架构层 | 保留（tech-stack.md 已 untracked，任务 0 一并纳入） |
| `Docs/specs/**` | L2 规格层 | 整理 / 重写 |
| `Docs/references/sekiro-genichiro-ai.md` | L3 参考层（原始 Lua 分析） | 自 `Docs/md/` 移动 |
| `.gitignore` | 美术资源忽略 | 已完成（commit 75a041b） |

**git 目录改名注意事项（Windows 大小写不敏感）：**
- `git mv docs Docs` 会报 "destination exists"（同目录）。正确流程：
  `git rm -r --cached docs/` → `mv docs Docs`（物理改名）→ `git add Docs/` → commit

---

### 任务 0：目录大小写统一 docs → Docs

**文件：** 整个 `docs/` 目录

- [ ] **步骤 1：解除 git 跟踪 + 物理改名**

```bash
cd "e:\Unity\Projects\DemoProjects\ARPG"
git rm -r --cached docs/
mv docs Docs
git add Docs/
```

- [ ] **步骤 2：验证**

```bash
git ls-files | grep -cE '^Docs/'
git ls-files | grep -cE '^docs/'
```
预期：`Docs/` 计数 > 0，`docs/` 计数 = 0。磁盘上只存在 `Docs` 一个目录。

- [ ] **步骤 3：Commit**

```bash
git add -A
git commit -m "refactor: 统一文档目录大小写为 Docs"
```

---

### 任务 1：重写 CLAUDE.md（规则可判定 + 文档访问纪律）

**文件：** 修改 `CLAUDE.md`（全文替换）

- [ ] **步骤 1：用以下内容替换 CLAUDE.md 全文**

````markdown
# ARPG 战斗 Demo — 只狼弦一郎 Boss 战

AI 辅助开发。

## 架构约束（可判定红线，违反即打回）

1. **战斗参数必须用 ScriptableObject**。`.cs` 中不得出现：`new AttackData{...}`、`private const` 战斗数值、`static readonly` 权重表、内联伤害/时长数值。参数一律来自 `.asset` 资产。
2. **状态机继承 `StateMachine` 基类**。禁止在 `MonoBehaviour.Update` 中写 switch-case 状态机。
3. **模块间通信走 `CombatEvents` 事件总线**。`Player/` 与 `Boss/` 目录类禁止直接引用对方类型，禁止 `FindObjectOfType` 跨模块获取。
4. **命中判定用 `Physics.OverlapSphere`**。禁止 `OnTriggerEnter/OnTriggerStay/OnCollisionEnter` 做攻击判定。
5. **单一输入系统**。禁止新旧输入系统混用（新 Input System 资产 + `UnityEngine.Input.*` 不得并存）。

## 代码规范

- 类名/方法名 `PascalCase`，私有字段 `_camelCase`
- 每个公开方法必须有 `/// <summary>` XML 注释
- 单文件 ≤ 300 行
- 一次提交只做一件事（原子提交）

## 文档访问纪律（硬规则）

1. 收到任务 → 在下方加载指引表定位对应 spec，**只读该文件**，禁止通读 `Docs/` 下所有文档
2. 禁止跨模块引用其他 spec（改弹刀就读 deflect 相关，不读 boss/ui）
3. 文档与代码冲突 → **停下报告，不自行选边**（如：specs 说 HFSM 而代码是 FSM）
4. 遇到缺失的 spec 或字段 → 停下询问用户，不臆造

## 文档加载指引

| 任务类型 | 必读文档 |
|---------|---------|
| 弹刀判定 | `Docs/specs/deflect/deflect-mechanics.md` |
| 抖刀惩罚/加成链 | `Docs/specs/deflect/deflect-penalties.md` |
| 弹刀参数 | `Docs/specs/deflect/deflect-params.md` |
| 架势条规则 | `Docs/specs/posture/posture-rules.md` |
| 架势参数 | `Docs/specs/posture/posture-params.md` |
| Boss 状态机 | `Docs/specs/boss/boss-state-machine.md` |
| Boss AI 决策+权重表 | `Docs/specs/boss/boss-ai-decision.md` |
| Boss 招式表 | `Docs/specs/boss/boss-attacks.md` |
| Boss 弹刀 AI | `Docs/specs/boss/boss-deflect.md` |
| Boss 数值 | `Docs/specs/boss/boss-params.md` |
| 危字类型+提示 | `Docs/specs/danger/danger-types.md` |
| 识破+踩头判定 | `Docs/specs/danger/mikiri-stomp.md` |
| 危字参数 | `Docs/specs/danger/danger-params.md` |
| 输入优先级+打断 | `Docs/specs/input/input-priority.md` |
| 输入缓冲 | `Docs/specs/input/input-buffer.md` |
| 帧冻结 | `Docs/specs/input/hitstop.md` |
| 回血系统 | `Docs/specs/input/healing.md` |
| 动画策略+事件 | `Docs/specs/animation/animation-strategy.md` |
| 动画参数 | `Docs/specs/animation/animation-params.md` |
| UI 布局+元素 | `Docs/specs/ui/ui-overview.md` |
| UI 事件映射 | `Docs/specs/ui/ui-events.md` |
| 伤害数字 | `Docs/specs/ui/ui-damage-numbers.md` |
| HFSM 框架 | `Docs/architecture/state-machine.md` |
| 数据层 | `Docs/architecture/data-layer.md` |
| 代码结构 | `Docs/architecture/code-structure.md` |
| 测试计划 | `Docs/testing/test-plan.md` |

## 测试要求

- 新增数值逻辑 → 必须配套 EditMode 单元测试（Assets/Tests/EditMode）
- 新增状态转换 → 必须配套 PlayMode 集成测试（Assets/Tests/PlayMode）
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 测试运行方式：Unity Editor → Window → General → Test Runner
````

- [ ] **步骤 2：验证**

```bash
grep -c 'DOCS/' CLAUDE.md
```
预期：输出 `0`（无大写 DOCS 残留）。所有路径为 `Docs/`。

- [ ] **步骤 3：Commit**

```bash
git add CLAUDE.md
git commit -m "docs: 重写 CLAUDE.md 为可判定规则 + 文档访问纪律"
```

---

### 任务 2：新建 Docs/README.md 总导航

**文件：** 创建 `Docs/README.md`

- [ ] **步骤 1：创建文件**

````markdown
# ARPG 战斗 Demo 文档导航

复刻只狼弦一郎 Boss 战。AI 辅助开发。

## 文档层级

| 层 | 位置 | 用途 |
|----|------|------|
| L0 规则 | `CLAUDE.md` | 架构红线 + 代码规范 + 访问纪律 |
| L1 架构 | `Docs/architecture/` | 技术选型与设计决策 |
| L2 规格 | `Docs/specs/` | 各模块机制 + 参数 |
| L3 参考 | `Docs/references/` | 原始素材（天守阁弦一郎 AI 分析） |
| L4 计划 | `Docs/superpowers/` | 设计规格 + 实现计划 |

## 美术资源

美术资源（`Assets/Resources/`、`Assets/Scenes/简洁/`）不上传 git，通过网盘分享。详见 `.gitignore`。
````

- [ ] **步骤 2：验证**

```bash
test -f Docs/README.md && echo "OK"
```

- [ ] **步骤 3：Commit**

```bash
git add Docs/README.md
git commit -m "docs: 新增 README 总导航"
```

---

### 任务 3：移动天守阁弦一郎 AI 分析 → references/

**文件：** 移动 `Docs/md/天守阁弦一郎AI.md` → `Docs/references/sekiro-genichiro-ai.md`

- [ ] **步骤 1：移动 + 添加参考头注释**

```bash
mkdir -p Docs/references
git mv "Docs/md/天守阁弦一郎AI.md" "Docs/references/sekiro-genichiro-ai.md"
```
然后在文件顶部（`# 天守阁弦一郎AI` 之前）插入：

```markdown
> **参考来源**：只狼游戏原始 Lua 行为分析（反编译）。这是 Boss AI 设计的**素材来源**，不是实现规格。实现以 `Docs/specs/boss/boss-ai-decision.md` 为准。
>
```

- [ ] **步骤 2：删除空目录**

```bash
rmdir Docs/md 2>/dev/null || true
```

- [ ] **步骤 3：Commit**

```bash
git add -A
git commit -m "docs: 天守阁AI分析移入 references/ 作为参考素材"
```

---

### 任务 4：重写 boss-state-machine.md（消除 HFSM/RangedState 冲突）

**文件：** 修改 `Docs/specs/boss/boss-state-machine.md`（全文替换）

- [ ] **步骤 1：确认代码事实**（先读代码再写文档）

```bash
cat Assets/Scripts/Boss/BossStateMachine.cs
cat Assets/Scripts/Boss/States/BossAttackState.cs
```
确认：状态机是带优先级的 FSM（非 HFSM），无独立 RangedState，射箭走 BossAttackState。

- [ ] **步骤 2：替换全文为**

````markdown
# Boss 状态机（一阶段）

> 与代码一致性说明：本项目为**带优先级的 FSM**（非 HFSM）。`tech-stack.md` 已澄清。无独立 RangedState，射箭由 AttackState 处理（`isRanged=true` 时跳过近战判定）。

## 状态流转

```
Idle → Move → Attack(Startup→Active→Recovery) / Stagger → Collapse → Executed
```

| 状态 | 说明 | 驱动方式 |
|------|------|---------|
| BossIdleState | 待机，计时后决策下一行为 | BossStateMachine |
| BossMoveState | 靠近玩家到攻击距离 | BossStateMachine |
| BossAttackState | 攻击（前摇/判定/后摇），射箭也走此状态 | BossStateMachine |
| BossStaggerState | 被弹刀硬直 | BossStateMachine |
| BossCollapseState | 架势崩溃，等待忍杀 | BossStateMachine |
| BossExecutedState | 忍杀演出 | BossStateMachine |

## 实现要求

- 状态继承 `State` 基类，`BossStateMachine` 继承 `StateMachine`
- 必须有 MonoBehaviour 驱动器（`BossStateMachineDriver`）负责 `new BossStateMachine()` + `Initialize(context)` + 每帧 `Update()`
- 状态间通过 `_stateMachine.TransitionTo<T>()` 切换
- Boss 与玩家不直接引用，通过 `CombatEvents` 通信

````

- [ ] **步骤 3：Commit**

```bash
git add Docs/specs/boss/boss-state-machine.md
git commit -m "docs: 重写 boss-state-machine 为与代码一致的 FSM 描述"
```

---

### 任务 5：重写 input-priority.md（修正优先级顺序）

**文件：** 修改 `Docs/specs/input/input-priority.md`（全文替换）

- [ ] **步骤 1：确认代码事实**

```bash
grep -n "Priority =>" Assets/Scripts/Player/StateMachine/*.cs
```
预期确认：Deathblow(9) > Stun(8) > Hit(7) > Heal(5) > Deflect(4) > Dodge(3) > Mikiri(2) > Attack(1) > Grounded/Airborne(0)。

- [ ] **步骤 2：替换全文为**

````markdown
# 输入优先级与打断规则

> 与代码一致性说明：优先级数字与 `PlayerBaseState.Priority` 一致（PlayerStateMachine.cs）。

## 操作优先级（从高到低）

`忍杀(9) > 硬直(8) > 受击(7) > 回血(5) > 弹刀/格挡(4) > 闪避(3) > 识破(2) > 攻击(1) > 移动(0)`

- 高优先级可打断低优先级
- 相同优先级不可互打断
- 忍杀最高：Boss 架势归零时按攻击键优先触发忍杀

## 打断规则矩阵

| 当前状态 | 可被谁打断 |
|---------|-----------|
| 攻击前摇 (Attack 1) | 弹刀(4)、闪避(3)、识破(2)、回血(5) |
| 攻击判定 | ❌（不可打断） |
| 攻击后摇 | 弹刀(4)、闪避(3)、识破(2)、回血(5) |
| 闪避 (3) | 弹刀(4)、回血(5) |
| 跳跃/移动 (0) | 任意战斗状态 |
| 回血 (5) | 受击(7)、硬直(8) |
| 弹刀 (4) | 受击(7)、硬直(8) |
| 识破 (2) | 受击(7)、硬直(8) |
| 受击硬直 (7) | ❌（不可打断） |
| 架势崩溃/硬直 (8) | ❌ |
| 忍杀 (9) | ❌ |

````

- [ ] **步骤 3：Commit**

```bash
git add Docs/specs/input/input-priority.md
git commit -m "docs: 重写 input-priority 修正优先级数字与打断矩阵"
```

---

### 任务 13：最终验证

**文件：** 全局检查

- [ ] **步骤 1：验证大小写无残留**

```bash
grep -rn 'DOCS/' --include="*.md" CLAUDE.md Docs/ | head
```
预期：无输出（或仅设计文档内的历史描述）。

- [ ] **步骤 3：验证 git 工作区干净**

```bash
git status --porcelain
```
预期：无未提交改动（`??` 未跟踪文件除外，如美术资源不应出现）。

- [ ] **步骤 4：验证美术资源未被跟踪**

```bash
git ls-files | grep -cE 'Resources/只狼|简洁'
```
预期：输出 `0`。

- [ ] **步骤 5：Commit（如有遗漏）**

```bash
git add -A
git commit -m "docs: 文档重构完成"
```

---

## 执行交接

计划已完成并保存到 `docs/superpowers/plans/2026-08-05-docs-restructure.md`。两种执行方式：

1. **子代理驱动（推荐）** — 每个任务调度一个新的子代理，任务间进行审查
2. **内联执行** — 在当前会话中使用 executing-plans 执行任务，批量执行并设有检查点
