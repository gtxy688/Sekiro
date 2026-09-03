# 项目长期笔记（苇名之刃 ARPG）

> 只记跨会话有效的**约定与坑**。日常流水见 `YYYY-MM-DD.md`。

## 🔴 安全约定（血泪换来的，必须遵守）

1. **动手改 `Assets/` 或 `Docs/` 之前，先 commit 一版安全点。**
   本项目已发生三次文件被清空事故（2026-09-01 18:50 `Docs/` 全丢；09-02 09:46 三个 Docs 文件；09-02 09:52 `Assets/Editor/` 整个目录 5367 行）。前两次能 100% 恢复，全靠事前 commit。
   **未跟踪文件丢了就是真丢了**，新建文件要尽快 `git add`。

2. **不要和用户的 git 操作并发跑。**
   出现 `.git/index.lock` 说明有并发 git 进程。判断死锁：文件 0 字节 + 几分钟没动 + 查不到 git 进程 → 可删；否则等待。
   教训：09-02 两次并发冲突后都紧接着发生文件删除。

3. **Bash 里 `git commit -F -`（heredoc）在本环境会被 SIGTERM**，改用 `git commit -m "x" -m "y"`。

## 🔴 项目配置问题（待用户修）

**`.gitignore` 第 70 行 `*.meta` 是错的。** 整个项目的 `.meta` 都没入库。Unity 引用全靠 `.meta` 里的 GUID，不入库 = 项目完全依赖本机磁盘状态；贴图 / prefab / 场景 / 动画 clip 的 meta 一旦丢失或重建，**引用会全崩**。
修法：删掉这一行 → `git add` 一次。

## 架构约定：不要删的东西

- **`GenichiroMoveCatalog.cs`（299 行）是招式表的唯一备份，别删。**
  它在运行时**零调用点**，只走 Editor 链路（`BossMoveTable.asset` ←→ Catalog 双向同步）。
  `BossMoveTable.asset` 只有一份，把几十小时调出来的判定时间全压在上面，Catalog 是唯一冗余副本。
  曾误判为「双真值源」建议砍掉——**该判断已作废**。

- **`BatchRootTransformSettings` 保留**（92 行）——手动批量入口，作为 `ArpgModelImportEnforcer` 自动接管的补充。
  不要因为它和 Enforcer 数值一样就判断"冗余"——用户要的就是**手动批量和自动接管两套入口都在**。

- **生成式 Builder + 两个工序工具全部已删**（8 个 + `KanjiTexUtil`，共 2416 行），不归档、不留底。`Assets/Editor/Archive/` 目录本身也删了。
  包含：6 个生成式 Builder + `InPlaceClipGenerator` + `ClipRenamer` + `KanjiTexUtil`（被三个 KanjiBuilder 独占引用，跟着删）。
  产物（prefab / 场景物体）已固化在场景里，删脚本不影响运行。
  **代价**：
  - 换 Boss 或改特效外观时，**只能手改 prefab**——或重写工具（无备份）
  - 加新攻击动画时，**得手动写原地 clip 处理**（剔除 RootT/RootQ 根骨骼曲线）或重写工具
  这是用户明确选择的"生成一遍就废"。

## 编辑器模块现状（2026-09-02 清理后）

主目录 13 个 / 2951 行：Guard 四件套（`ArpgValidationRules` `ArpgValidatorWindow` `ArpgBuildValidator` `ArpgModelImportEnforcer`）、时间轴四件套（`AttackTimelineWindow` `Preview` `ClipFinder` `Prefs`）、`BatchRootTransformSettings`、`BossMoveDamageWindow`、`BossMoveTableEditor`、`AttackConfigEditor`、`GenichiroMoveCatalogExporter`。

审查结论与遗留项见 `Docs/editor-review/editor-code-review.md`，验收步骤见 `Docs/editor-review/guard-验收清单.md`。

## 工具

- `Temp/brace_check.py` —— 括号平衡检查（跳过注释/字符串，纯词法扫描）。文件出事故恢复后用来验证脚本没写坏：
  `"C:/Users/20637/.workbuddy-ai/binaries/python/versions/3.13.12/python.exe" Temp/brace_check.py Assets/Editor`
  注意 `Temp/` 可能被 Unity 清理，丢了可重建。

## 协作纪律（来自 `CLAUDE.md`）

- AI 实现 → 用户按模块验收。交付必须附**验收清单**（操作步骤 + 预期结果），**不得自己宣称"完成"**。
- 文档先查索引表定位，**只读那一份**；文档与代码冲突 → 停下报告，不自行选边；缺失字段 → 停下询问，不臆造。
- **用户说"删一些 / 改一些 / 优化一下"等模糊指令时，先用 AskUserQuestion 问"具体指哪些"，再给方案**。
  不要把判断塞进 A/B/C/D 选项里——尤其"推荐"选项不能包装得像"唯一正解"，否则用户会顺着选而没察觉自己的真实意图。

## AI 自检纪律（我自己的坑，2026-09-02）

**「迁移」不等于「修复」。** 把一处坏代码从 A 类搬到 B 类，病根还在，只是换了位置。
改完必须 **grep 病根本身** 验证它真的消失了（`!= playerRef`、`Time.timeScale =` 这类模式），
而不是验证「我改动过哪几行」。

真实教训：把 `if (initiator != PlayerRef)` 从 `CombatManager` 搬到 `DuelDirector`，判断一字未改，
还给新类配了句「本类不认识玩家」的注释——事实上它通过 `playerRef` 字段认识。
而就在同一轮，我还在跟用户讲「这类不该迁移、该消灭」。

**推论 1：给自己的重构写注释时，注释描述的是目标状态不是当前状态，会骗到自己。**
写完回头读一遍，确认注释里宣称的性质在代码里真的成立。
实例：`DuelDirector` 注释写着「本类不认识玩家」，但它有 `playerRef` 字段。

**推论 2：注释里写死统计数字（「41 处调用点」「17 个文件」）必然随代码漂移。**
要么只写性质不写数字，要么附上重算命令让别人能自己核。
例：`grep -rn "CombatManager\.Instance" --include=*.cs Assets/Scripts`
实例：09-02 总结时发现 `CombatManager` 注释里的「41 处 / 17 文件」实际已降到 32 处。

**推论 3：给函数定性（「这是兜底，走不到」）之前，先 grep 调用点。**
实例（09-03）：我断言 `AttackCombatResolve.DefaultCombat` 是「运行时兜底，正常流程走不到」。
grep 后 8 个调用点全在编辑器链路（`GenichiroMoveCatalog` / `GenichiroMoveCatalogExporter` / `BossMoveDamageWindow`），
它是**创建招式表条目时填初始值的工厂函数**，不是运行时兜底。定性错了，危害评估也跟着错。
修正结论：它是模板常量，值已烘焙进资产，改它对现有资产零影响 → 危害比「运行时兜底」更小，不必动。

## 架构事实：招式时间轴有两套表示（09-03 确认）

**双重表示只发生在「判定窗」这一件事上，不是整条时间轴都有双份。**（09-03 小金追问后澄清）

`AttackConfig` 上「什么时候有判定」这个事实同时存在两种写法：
- **A 单窗字段**：`HitStartTime` / `RecoveryWindowStart`（玩家用）
- **B 脉冲数组**：`hitPulses[] {start, end}`（Boss 一招多刀用）

其余字段是**招式级**时间，pulses 表达不了、也从来只有一份，不存在双表示问题：
`ComboWindowEnd` / `StateDuration` / `RotationWindowEnd` / `sfxCues` / `arrowCues`。
`AttackWindowSync.ApplyPulses` 只回写 A 的两个字段，`CoverDuration` 只修正 `StateDuration` / `RotationWindowEnd`。

**优先级（`AttackWindowSync.CanMeleeHit` / `Hitbox.cs:61`）：pulses 非空 → 只信 pulses，此时改 A 字段无效；pulses 为空 → 才信 A。**
最小判定跨度 `MinMeleeHitSpan = 0.02f`（短于一帧的红条视为假窗）。

`AttackWindowSync`（205 行）存在的唯一理由就是在 A、B 之间搬运同步 + 保证 `StateDuration` 盖住所有窗口。
**调 Boss 招式时的踩坑点：只在 Inspector 改 `HitStartTime` 而 pulses 有元素 → 改动无声失效。**
