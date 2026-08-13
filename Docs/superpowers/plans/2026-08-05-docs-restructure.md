# 文档体系重构实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 重构文档体系，使 CLAUDE.md 规则可判定、specs 带人工验收清单、消除大小写分裂与文档-代码冲突。

**架构：** 按 L0-L4 分层（规则/架构/规格/参考/计划）。CLAUDE.md 改为短硬规则 + 文档访问纪律；22 份 specs 中 21 份保留补验收、2 份重写；统一目录大小写为 `Docs`。

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
| `Docs/README.md` | 总导航：模块→spec→验收清单 总表 | 新建 |
| `Docs/architecture/*` | L1 架构层 | 保留（tech-stack.md 已 untracked，任务 0 一并纳入） |
| `Docs/specs/**` | L2 规格层，每份末尾带验收清单 | 补验收 / 重写 |
| `Docs/references/sekiro-genichiro-ai.md` | L3 参考层（原始 Lua 分析） | 自 `Docs/md/` 移动 |
| `Docs/testing/test-plan.md` | 验收清单总表 | 保留 |
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

AI 辅助开发，**用户负责测试与验收（按模块）**。AI 每完成一个模块，交付「验收清单」供用户逐条验证，验收通过才算完成。

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

## 工作流（AI 实现 → 用户验收）

1. 收到任务 → 按「文档加载指引」读对应 spec，只读需要的章节
2. 实现代码 → 不自己宣称"完成"
3. 在交付说明中附「验收清单」：操作步骤 + 预期结果（供用户在 Unity 中逐条验证）
4. 用户验收通过 → 提交；验收不通过 → 修正后重新交付

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

复刻只狼弦一郎 Boss 战。AI 辅助开发，用户按模块验收。

## 文档层级

| 层 | 位置 | 用途 |
|----|------|------|
| L0 规则 | `CLAUDE.md` | 架构红线 + 代码规范 + 访问纪律 |
| L1 架构 | `Docs/architecture/` | 技术选型与设计决策 |
| L2 规格 | `Docs/specs/` | 各模块机制 + 参数 + 验收清单 |
| L3 参考 | `Docs/references/` | 原始素材（天守阁弦一郎 AI 分析） |
| L4 计划 | `Docs/superpowers/` | 设计规格 + 实现计划 |

## 模块 → 规格 → 验收清单 总表

| 模块 | 规格文档 | 验收清单 |
|------|---------|---------|
| 弹刀 | `specs/deflect/deflect-mechanics.md` | `deflect-params.md` 末尾 |
| 架势 | `specs/posture/posture-rules.md` | `posture-params.md` 末尾 |
| Boss 状态机 | `specs/boss/boss-state-machine.md` | 各状态 Enter/Execute 验收 |
| Boss AI | `specs/boss/boss-ai-decision.md` | `boss-params.md` 末尾 |
| 危字 | `specs/danger/danger-types.md` | `danger-params.md` 末尾 |
| 输入 | `specs/input/input-priority.md` | 各 input spec 末尾 |
| 动画 | `specs/animation/animation-strategy.md` | `animation-params.md` 末尾 |
| UI | `specs/ui/ui-overview.md` | `ui-damage-numbers.md` 末尾 |

## 验收流程

1. AI 交付模块时附「验收清单」
2. 用户在 Unity 打开场景，逐条操作验证
3. 验收通过 → AI 提交；不通过 → 返回修正

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

## 验收清单

- [ ] 操作：打开 GameScene，按 Play
      预期：场景中存在 Boss 对象，Boss 进入 Idle，不报错
- [ ] 操作：把玩家移动到 Boss 攻击距离内
      预期：Boss 从 Idle 切到 Attack，播放攻击动画
- [ ] 操作：观察 Boss 状态（Debug 或 Inspector）
      预期：Attack 内完成 Startup→Active→Recovery 三段后回到 Idle
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

## 验收清单

- [ ] 操作：攻击前摇中按弹刀键
      预期：立即中断攻击进入弹刀（4 > 1）
- [ ] 操作：攻击判定帧内按闪避
      预期：攻击判定不可被打断，闪避不生效或等后摇
- [ ] 操作：Boss 架势归零后按攻击键
      预期：触发忍杀（9），而非普通攻击（1）
````

- [ ] **步骤 3：Commit**

```bash
git add Docs/specs/input/input-priority.md
git commit -m "docs: 重写 input-priority 修正优先级数字与打断矩阵"
```

---

### 任务 6：deflect 模块补验收清单

**文件：** 修改 `Docs/specs/deflect/deflect-mechanics.md`、`Docs/specs/deflect/deflect-penalties.md`、`Docs/specs/deflect/deflect-params.md`

- [ ] **步骤 1：在 deflect-mechanics.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：打开 GameScene，玩家按住右键（Deflect）
      预期：播放格挡动画，进入格挡状态（DeflectSystem.IsBlocking = true）
- [ ] 操作：Boss 普通攻击判定前 12 帧内点右键
      预期：PerfectDeflect，0 伤害，HitStop 生效，Boss 架势条涨
- [ ] 操作：Boss 攻击时保持按住右键
      预期：NormalBlock，HP×0.4 减伤，双方架势稍增
- [ ] 操作：攻击从玩家背后方向来袭（夹角 >90°）
      预期：DeflectResult.None，全额伤害
````

- [ ] **步骤 2：在 deflect-penalties.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：0.5s 内连续点右键 ≥2 次（未成功弹刀）
      预期：抖刀计数增加，弹刀窗口逐步缩小至 1 帧下限
- [ ] 操作：连续成功弹刀 2/3/4 次
      预期：对 Boss 架势伤害倍率 1.2/1.4/1.5
- [ ] 操作：停止按键或成功弹刀后等 0.5s
      预期：抖刀计数归零
````

- [ ] **步骤 3：在 deflect-params.md 的「验收标准」小节后追加「验收清单」**

````markdown
## 验收清单

- [ ] 操作：新右键轻点 → 弹刀窗口 0.2s 内受击
      预期：PerfectDeflect（对应"验收标准"第1-2条）
- [ ] 操作：连续快速点右键 5 次
      预期：窗口缩至最小 0.016s（第4条）
- [ ] 操作：攻击夹角 >90°
      预期：None（第7条）
````

- [ ] **步骤 4：Commit**

```bash
git add Docs/specs/deflect/
git commit -m "docs: deflect 模块补充验收清单"
```

---

### 任务 7：posture 模块补验收清单

**文件：** 修改 `Docs/specs/posture/posture-rules.md`、`Docs/specs/posture/posture-params.md`

- [ ] **步骤 1：在 posture-rules.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：玩家普攻命中 Boss
      预期：Boss 架势条增加 postureDamage
- [ ] 操作：玩家完美弹刀
      预期：玩家自身架势 -8% max，Boss 架势大幅增加
- [ ] 操作：玩家普通格挡
      预期：玩家自身架势稍增（×0.3）
- [ ] 操作：脱战 2s 后观察架势条
      预期：架势以 15%/s 恢复
- [ ] 操作：把玩家/ Boss 架势打到满
      预期：触发 OnPostureBroken，玩家进 StunState，Boss 进 CollapseState
````

- [ ] **步骤 2：在 posture-params.md 的「验收标准」小节后追加「验收清单」**

````markdown
## 验收清单

- [ ] 操作：把玩家架势打到满
      预期：玩家进入 StunState，硬直 1.5s（对应验收标准第5-6条）
- [ ] 操作：把 Boss 架势打到满
      预期：Boss 进入 CollapseState 2s，可被忍杀
````

- [ ] **步骤 3：Commit**

```bash
git add Docs/specs/posture/
git commit -m "docs: posture 模块补充验收清单"
```

---

### 任务 8：animation 模块补验收清单

**文件：** 修改 `Docs/specs/animation/animation-strategy.md`

- [ ] **步骤 1：在 animation-strategy.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：打开 GameScene，玩家按 WASD
      预期：角色平滑移动，Blend Tree 在 moveX/moveZ/speed 间平滑过渡
- [ ] 操作：玩家按左键攻击
      预期：攻击动画用 Root Motion，位移符合招式
- [ ] 操作：玩家按右键弹刀
      预期：弹刀动画 + 可被完美弹刀打断
- [ ] 操作：玩家闪避/识破/喝药
      预期：对应动画为 Root Motion
- [ ] 操作：观察 Animator 面板
      预期：EnableHitbox/OnAttackHit 等 Animation Event 在正确帧触发
````

- [ ] **步骤 2：Commit**

```bash
git add Docs/specs/animation/animation-strategy.md
git commit -m "docs: animation 模块补充验收清单"
```

---

### 任务 9：input 模块补验收清单

**文件：** 修改 `Docs/specs/input/input-buffer.md`、`Docs/specs/input/hitstop.md`、`Docs/specs/input/healing.md`

- [ ] **步骤 1：在 input-buffer.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：攻击动画播放中快速按下一次弹刀
      预期：弹刀输入进入缓冲队列（150ms 窗口），动画到可取消点后取出执行
- [ ] 操作：等 150ms 后再按
      预期：陈旧输入被丢弃
- [ ] 操作：缓冲队列中同时有多个输入
      预期：取出最高优先级（Deathblow > Deflect > Attack...）
````

- [ ] **步骤 2：在 hitstop.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：玩家普攻命中 Boss
      预期：画面定格约 0.033s，期间 UI 继续渲染
- [ ] 操作：玩家完美弹刀
      预期：帧冻结约 0.05s
- [ ] 操作：识破成功
      预期：帧冻结约 0.067s
````

- [ ] **步骤 3：在 healing.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：玩家按 E 喝药（有剩余次数）
      预期：扣除 1 次，播放喝药动画 0.8s，播完回复 30% MaxHP
- [ ] 操作：喝药动画中受击
      预期：动画打断，次数不退还，生命不恢复
- [ ] 操作：剩余次数为 0 时按 E
      预期：无反应，UI 药葫芦计数为 0
````

- [ ] **步骤 4：Commit**

```bash
git add Docs/specs/input/
git commit -m "docs: input 模块补充验收清单"
```

---

### 任务 10：boss 模块补验收清单

**文件：** 修改 `Docs/specs/boss/boss-ai-decision.md`、`Docs/specs/boss/boss-attacks.md`、`Docs/specs/boss/boss-deflect.md`、`Docs/specs/boss/boss-params.md`

- [ ] **步骤 1：在 boss-ai-decision.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：玩家距离 Boss 3m 内
      预期：Boss 在横斩/上挑/三连斩/突刺/下劈/跳跃劈/扫击间按权重随机选招
- [ ] 操作：玩家距离 Boss >4m
      预期：Boss 大概率选择射箭
- [ ] 操作：玩家连续攻击 3 段+
      预期：Boss 有较高概率进入弹刀姿态
- [ ] 操作：玩家喝药
      预期：Boss 大概率突进攻击
````

- [ ] **步骤 2：在 boss-attacks.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：Boss 使出横斩/上挑/下劈/跳跃劈
      预期：对应动画播放，判定帧 OverlapSphere 命中玩家
- [ ] 操作：Boss 使出突刺
      预期：触发 Thrust 危字，玩家可识破（Shift 无方向）
- [ ] 操作：Boss 使出扫击
      预期：触发 Sweep 危字，玩家可跳跃踩头
- [ ] 操作：Boss 射箭（isRanged）
      预期：播放射箭动画（注：当前代码射箭无投射物无伤害，属待实现，见代码注释）
````

- [ ] **步骤 3：在 boss-deflect.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：玩家普攻命中 Boss
      预期：Boss 有 40% 概率进入弹刀姿态
- [ ] 操作：玩家连招 2 段+
      预期：Boss 弹刀概率升至 60%
- [ ] 操作：Boss HP <30%
      预期：弹刀概率降至 25%
- [ ] 操作：Boss 弹刀成功
      预期：玩家架势 ×1.5、硬直 0.3s，Boss 获得反击窗口
````

- [ ] **步骤 4：在 boss-params.md 的「验收标准」小节后追加「验收清单」**

````markdown
## 验收清单

- [ ] 操作：观察 Boss AI 决策（Debug 日志）
      预期：按距离和权重动态选招，覆盖 9 种招式（验收标准第1条）
- [ ] 操作：对比玩家与 Boss 弹刀窗口
      预期：Boss 9 帧 < 玩家 12 帧（第2条）
- [ ] 操作：Boss 架势归零
      预期：崩溃 → 忍杀（第6条）
````

- [ ] **步骤 5：Commit**

```bash
git add Docs/specs/boss/
git commit -m "docs: boss 模块补充验收清单"
```

---

### 任务 11：danger 模块补验收清单

**文件：** 修改 `Docs/specs/danger/danger-types.md`、`Docs/specs/danger/mikiri-stomp.md`、`Docs/specs/danger/danger-params.md`

- [ ] **步骤 1：在 danger-types.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：Boss 使出突刺危
      预期：屏幕出现红色↓ 危字提示，持续整个前摇+判定帧
- [ ] 操作：Boss 使出扫击危
      预期：红色→ 危字提示
- [ ] 操作：Boss 使出投技（如有）
      预期：红色↑ 危字提示
- [ ] 操作：危字攻击后摇开始
      预期：危字提示隐藏
````

- [ ] **步骤 2：在 mikiri-stomp.md 末尾追加**

````markdown
## 验收清单

- [ ] 操作：突刺危判定帧内按 Shift（无方向）
      预期：识破成功：踩刀动画 + 音效 + 帧冻结 0.067s + 敌方架势+50 + 硬直0.5s
- [ ] 操作：突刺危判定帧内按 Shift（带方向）
      预期：普通闪避，无识破
- [ ] 操作：突刺危不按 Shift
      预期：被刺中，受伤+架势上升
- [ ] 操作：下段扫击时跳跃（下落经过敌人头顶，Y偏移<0.5m、水平<1.5m）
      预期：踩头成功，敌方架势+30，帧冻结 0.05s
````

- [ ] **步骤 3：在 danger-params.md 的「验收标准」小节后追加「验收清单」**

````markdown
## 验收清单

- [ ] 操作：触发突刺危后按 Shift 识破
      预期：对应参数生效（距离<3m + 角度<60°，验收标准第3-4条）
- [ ] 操作：下段扫击跳跃踩头
      预期：敌方架势+30（第2条）
- [ ] 操作：非突刺按 Shift
      预期：普通闪避无惩罚（第5条）
````

- [ ] **步骤 4：Commit**

```bash
git add Docs/specs/danger/
git commit -m "docs: danger 模块补充验收清单"
```

---

### 任务 12：ui 模块补验收清单

**文件：** 修改 `Docs/specs/ui/ui-overview.md`、`Docs/specs/ui/ui-events.md`、`Docs/specs/ui/ui-damage-numbers.md`

- [ ] **步骤 1：在 ui-overview.md 末尾追加**

````markdown
## 验收清单

> 注：UI 系统当前未实现（无 UI 脚本），以下为验收目标。实现后逐条验证。

- [ ] 操作：玩家受伤
      预期：左上角血条缩减，0.3s 后白色残影条跟随
- [ ] 操作：玩家架势 >80%
      预期：架势条变红闪烁预警
- [ ] 操作：Boss 架势归零
      预期：Boss 血条上方忍杀提示闪烁
- [ ] 操作：玩家喝药
      预期：右下角药葫芦计数更新
- [ ] 操作：Boss 使出危字攻击
      预期：屏幕中央红色危字提示
````

- [ ] **步骤 2：在 ui-events.md 末尾追加**

````markdown
## 验收清单

> 注：UI 事件订阅当前未实现，以下为验收目标。

- [ ] 操作：完美弹刀
      预期：伤害数字(黄"弹") + 屏幕闪光
- [ ] 操作：普通格挡
      预期：伤害数字(灰"挡")
- [ ] 操作：识破成功
      预期：伤害数字(蓝"识破") + 屏幕震动
- [ ] 操作：忍杀
      预期：全屏闪白 + 隐藏忍杀提示
````

- [ ] **步骤 3：在 ui-damage-numbers.md 的「验收」小节后追加「验收清单」**

````markdown
## 验收清单

> 注：伤害数字系统当前未实现，以下为验收目标。

- [ ] 操作：普攻命中
      预期：白色 24pt 数字在命中点弹出，上飘 2单位/s，1.0s 淡出
- [ ] 操作：弹刀
      预期：黄色 28pt 数字
- [ ] 操作：识破
      预期：蓝色 32pt 数字
- [ ] 操作：多个数字同时弹出
      预期：随机偏移避免重叠（对象池复用）
````

- [ ] **步骤 4：Commit**

```bash
git add Docs/specs/ui/
git commit -m "docs: ui 模块补充验收清单（标注待实现）"
```

---

### 任务 13：最终验证

**文件：** 全局检查

- [ ] **步骤 1：验证大小写无残留**

```bash
grep -rn 'DOCS/' --include="*.md" CLAUDE.md Docs/ | head
```
预期：无输出（或仅设计文档内的历史描述）。

- [ ] **步骤 2：验证每份 spec 有验收清单**

```bash
for f in $(find Docs/specs -name "*.md"); do
  if ! grep -q "验收清单" "$f"; then echo "缺少验收清单: $f"; fi
done
```
预期：无输出（22 份 specs 全部含验收清单）。

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
