# 教学笔记（NOTES.md）

## 用户偏好与约束（已确认）

- **事实来源**：用户明确要求"只根据项目实际代码来"。`Docs/` 下的复习文档（`Docs/项目复习/`、`旧的 Docs/CODEX_TEACHING_GUIDE.md（已删除）`）**不作为架构事实来源**，仅可作索引或对照。所有结论必须能指到 `文件路径:行号`。
- **学习目标（2026-09 起）**：秋招面试讲清架构（第一优先）+ 能看懂源码继续开发（第二优先）。
- **自评基础较弱**：默认 Unity / 设计模式 / 状态机概念没有真正理解，需要"没有它会怎样 → 简单写法的问题 → 项目方案"的推导顺序，不能直接下定义。
- **纠错态度**：用户要求发现自己理解错误时直接纠正，不要顺着说"差不多"。
- 不要一次讲完整个项目：一节课一个核心问题，短、可完成。
- 每节课结尾给少量"为什么"型自测题，并在下一节开始前逐题判对错。

## 节奏偏好（重要，2026-09 用户纠正）

- **先整体、后细节**。用户明确反馈"太快了，我需要的是整体架构 + 各功能方面的技术栈，而不是这种细节"。第一轮我直接给出了调用链、true/false 分支、逐行 file:line 与自测题——**这是越级**。
- 正确顺序：① 整体地图（五层结构 + 各功能方面技术栈 + 依赖方向，不用行号、不展开分支）→ ② 链路级流程图 → ③ 源码精读。
- 具体纪律：
  - 一次交付**一个层级**的东西，不要同一轮里既给总览又给链路又给练习题。
  - 细节材料可以先做好但**必须明确标注"细节层，现在不用看"**，并让总览页成为入口。
  - 不用每轮都塞自测题；用户没要练习时不主动加。
  - 少用 `文件:行号` 堆密度——总览层只给类名与职责，行号留给源码精读阶段。

## 方法纠正（2026-09，用户第二次否定，重要）

- 用户对"简历 claim → 实现要点 → 30 秒答法"这类映射表明确否定：**"我只能背诵，找不到逻辑关联"**。简历/文档/模块编号都是**外部清单**，不能用来组织结构——用它们当大纲，产出的必然是记忆材料。
- 正确做法：**从项目本身入手**——先讲清"一场 Boss 战实际在发生什么"，再用因果链推导出每个模块**为什么必须存在**（没有它会怎样 → 最简单的写法为什么不够 → 项目怎么做 → 代价）。逻辑通了，简历上的话自然说得出来，不需要背。
- **产物不要前置**：先在对话里把逻辑讲通、等用户认可这个框架，再落成 HTML 课程。用户连续两轮否定的都是"过早产出文档"，不是内容本身。
- 收到"写得不好 / 只能背诵"这类反馈时：直接换方法，不辩解、不补充说明旧产物。

## 表达方式（2026-09，用户第三次纠正）

- **禁止向用户提问、禁止反问句**。用户原话："为什么你老是喜欢提出疑问或者反问句呢？你好好陈述不就好了"。
- 具体禁令：
  - 不用"你觉得呢 / 认不认 / 要不要我… / 两个选择"这类征求式结尾；
  - 不用反问句做论述（"那为什么不是……呢？"）；
  - 不把"提问引导"当成教学法——用户要的是**陈述清楚**，不是被提问；
  - 标题也用陈述式（"状态机这一环的由来"），不用"为什么要有状态机"。
- 替代做法：把判断、结论、下一步直接陈述出来。需要用户决策时，说明我的默认选择和我将怎么做，而不是发问。
- 这条与 superpowers/教学模板的默认习惯（确认理解、给出选项、用问题引导）相反：**以用户偏好为准**。

## 课程形态（2026-09 定下）

- 页面正文全部用陈述句，标题也陈述式；页面内**不放自测题**（用户不需要被考，需要被讲清楚）。
- 深度按需展开：细节放进 `<details class="disc">`（组件 `teach-assets/disclosure.css`），默认收起，页面读起来短、想深挖再点开。
- 每课附一列"与简历的关系"：标出这一段属于简历哪条主线、或标注"未写、一句话带过"。用户明确关心面试主线边界。
- 结论一律给代码位置（`文件:行号`），因为用户要求以真实源码为准。
- 结尾给一段连续陈述的"口述版"，把本课的因果顺成一遍可直接讲的话；不给提问、不给选项。
- 课程编号递增：`lessons/000N-<dash-case>.html`；学习记录：`learning-records/000N-<dash-case>.md`。
- 参考文档里的术语以 `reference/glossary.html` 为准，课程中出现的术语必须与之一致。

## 教学环境约定

- 教学工作区就在项目根目录：`MISSION.md` / `RESOURCES.md` / `NOTES.md` / `lessons/` / `reference/` / `teach-assets/` / `learning-records/`。
- ⚠️ **教学资源目录必须叫 `teach-assets/`，绝不能叫 `assets/`**：Windows 文件系统大小写不敏感，`assets/` 与 Unity 的 `Assets/` 是同一个目录，会把样式表写进工程里。已因此把 `lesson.css`/`diagram.css`/`disclosure.css`/`quiz.js` 从 `Assets/` 迁出到 `teach-assets/`，全站引用同步改写。
- 课程与参考文档全部是 HTML，共用 `teach-assets/lesson.css`、`teach-assets/diagram.css`、`teach-assets/disclosure.css`、`teach-assets/quiz.js`。**新增课程必须复用，不要另写样式或组件。**

## ⚠️ 文本文件编码纪律（血的教训，务必遵守）

- 本机 shell 是 **Windows PowerShell 5.1**，`[System.Text.Encoding]::Default` = **gb2312**，不是 UTF-8。
- 用 `Get-Content -Raw`（不带 `-Encoding`）读**无 BOM 的 UTF-8 文件**会按 gb2312 解码，再用 `Set-Content -Encoding UTF8` 写回，就会把中文**永久损坏**：GBK 往返有损，解码失败的字节被替换成 `?`，无法逆转换还原（已实测验证）。
- **禁止用 PowerShell 的 `Get-Content` / `Set-Content` / `-replace` 批量改写本工作区的文本文件。** 一律用编辑器工具（read / edit / write）。确需脚本时，必须显式走 .NET 且指定编码：`[System.IO.File]::ReadAllBytes` + 显式 `UTF8Encoding` + `WriteAllBytes`。
- **事故记录**：一条批量替换脚本（把 `../assets/` 改成 `../teach-assets/`）损坏了 5 个文件 —— `lessons/0001-architecture-map.html`、`reference/glossary.html`、`reference/architecture-overview.html`、`reference/flowcharts.html`、`reference/tech-stack.html`。这些文件**均未被 git 跟踪**，无法从历史还原，只能重写。损坏只吃掉中文散文，HTML 结构与 `<code>` 内的类名/文件路径完好，可据此重建。
- 未受影响（已逐个验证）：`lessons/0002`、`lessons/0003`、`MISSION.md`、`NOTES.md`、`RESOURCES.md`、`learning-records/*`、`teach-assets/*`。

## 重建记录（2026-09）

**结论：5 个损坏文件全部重建完成，全站 8 个 HTML 通过终检**（UTF-8 合法、0 替换符、0 乱码特征字、0 断链）。

| 文件 | 结果 |
| --- | --- |
| `lessons/0001-architecture-map.html` | 203 行，10 节 3 表。结构照骨架还原，但**移除了原文件的 24 个 quiz 按钮**——与「页面内不放自测题」的既定偏好冲突，按现行偏好改为纯陈述 |
| `reference/architecture-overview.html` | 437 行，8 表，SVG 六层数据流（viewBox 760×560） |
| `reference/glossary.html` | 404 行，7 表，**59 条术语**。**质量最高**：用 GBK 逆转换把原文散文恢复约 86%，措辞是原作者原话 |
| `reference/tech-stack.html` | 306 行，4 表，73 处源码引用 |
| `reference/flowcharts.html` | **5 张图**（viewBox 760×690 / 730 / 880 / 800 / 380）。用 GBK 逆转换恢复：SVG 几何坐标**零损坏**，仅文字坏 243 处，已按源码补回 |
| `lessons/0004-where-things-live.html` | 132 行，**新增**代码地图课（见下） |

**关键经验：GBK 往返逆转换可恢复约 86% 原文。** 做法是 `[System.IO.File]::ReadAllBytes` → 严格 UTF-8 解码 → 用 `Encoding.GetEncoding(936)` 重新编码 → 宽松 UTF-8 解码。恢复出的字符串里 `\uFFFD?` 基本等于「丢失的 1 个汉字 + 紧随的 `<`」，可据上下文逐字补回。**下次再遇到同类损坏，先做逆转换，不要直接照骨架重写。**

**新增 `lessons/0004-where-things-live.html`**：用户自述"完全没看过项目代码，连文件都不知道"，所以补一节纯导航课。内容＝十个目录各自职责（带文件数）+ 按功能查文件的 19 行对照表 + 读代码前三条约定 + 第一次读代码的顺序（`Command.cs → BaseState.cs → HierarchicalState.cs → GroundedState.cs → IdleState.cs → CharacterBody.cs`）。不解释设计，只解决"走得到"。

**骨架行号有漂移，不能盲信。** `glossary` 与 `tech-stack` 两轮分别核出 4 处和 13 处行号与源码不符（例：`IsGrounded` 物理接地在 `CharacterBody.cs:93` 而非 293；`PostureBreakSource` 在 `Command.cs:22-27` 而非 39-50；`manifest.json:3` 是 MCP 插件而非 Animator 模块，Animator 在 `:20`）。已逐条核对修正，并保留骨架原行号并列显示。

## ⚠️ 待用户裁定的三处「文档与代码冲突」（只报告，未改代码）

1. **存在测试文件，与「本项目不做自动化测试」冲突**：`Assets/Tests/Editor/CharacterBodyHfsmFlagTests.cs` 共 108 行，用 NUnit 测 `CharacterBody` 的 HFSM 派生标志，并通过反射调私有 `Awake`；同时全工程 **0 个 `.asmdef`**，未接入测试程序集。面试口径建议："一个编辑器侧的单文件测试脚本，没接入测试体系"。
2. **`Bow_Heavy` 永远不会被选中**：`BossMoveLayer.cs:8` 定义了 `Interrupt = 2`，`GenichiroMoveCatalog.cs:153` 把 `Bow_Heavy` 归入该层，但全工程 `BossMovePicker.Pick` 只有 2 个调用点（`BT_PickActive.cs:72` 传 `Active`、`BT_Kengeki.cs:64` 传 `Kengeki`），**没有任何调用点传 `Interrupt`**。该招权重从未参与抽招。
3. **伤害数值有代码侧硬编码**：`SO/AttackCombatResolve.cs:8-25` 的 `DefaultCombat` 直接写死 15/15、20/25、10/10；另有兜底值在 `AttackStateBase.cs:29-32`（`fallbackDamage` 等）。与「战斗数值全部走 SO」的规范不完全一致，属"配置未覆盖时的兜底档位"。

以上三条已写进 `reference/flowcharts.html` 的图 3、图 4 备注，作为"主动交代"的面试素材。

## 待办 / 观察

- 用户尚未表态是否愿意加入社区；`RESOURCES.md` 的 Wisdom 段暂按"待确认"记。
- 每节课结束后观察：用户是否真的能复述调用链（而不是复述类名）——这决定下一节是继续推进还是回补。
- 项目不做自动化测试，练习设计成"读代码 + 口头复述 + 源码定位"，不要设计跑测试的练习。
