# AI 规范评审（苇名之刃 ARPG）

评审日期：2026-09-01
本次更新：2026-09-01 18:50 — P0-1、P0-2 已修复；A 组四项、C 组已修复；D 组部分暂停（原因见文末事故记录）

> **本文曾随 `Docs/` 目录整体消失而丢失（未跟踪文件无法从 git 恢复），此为重建版。**

评审对象：`CLAUDE.md`、`CODEBUDDY.md`、`.codebuddy/skills/arpg-constraints/`、`Docs/architecture/`
评审方式：读规范 → 用 grep 反查代码实际执行情况（111 个 .cs）

---

## 一句话结论

**规范内容本身质量高（8 条红线硬且具体，代码也确实守住了），但规范的"工程化"是零：一份内容抄三遍、内部自相矛盾、没有任何机器校验，能守住全靠人盯。**

---

## 一、做得好的（保留，不要改）

### 1. 硬红线真的被执行了 —— 超出平均水准

| 检查项 | 结果 |
|--------|------|
| 全局命名空间类型（红线：禁止） | **0 处** / 111 个 .cs |
| `OnTriggerEnter/Stay`（红线 3：禁止） | **0 处** |
| `CharacterBody` 封装 API 是否真存在 | 存在：`IsGroundedTop`(L442) / `IsInGroundedSubState<T>`(L445) / `TryChangeGroundedSubState`(L452) / `ForceChangeGroundedSubState`(L460) |
| 业务代码直接判顶层状态类型（红线 2：禁止） | **0 处违规**。全项目仅 `CharacterBody.cs` 内部 + `AirState.cs:38`（判自己的子状态，合规） |

「禁止直接判顶层类型」这条最容易被人偷懒绕过。它写的是"禁止"不是"建议"，还把替代 API 逐个列出 —— 这是规范该有的写法。

### 2. 文档分级做得对

17 个模块 + 依赖图 + 索引表；每个模块 `0X-xxx.md` + `0X-xxx-test.md` 成对；「文档加载指引表」把任务类型映射到文档，控制上下文污染。

### 3. 针对 AI 天性的约束方向正确

「不自己宣称完成，交验收清单」「文档与代码冲突时停下报告，不自行选边」「缺失字段停下询问，不臆造」—— 分别针对 AI 的**过度自信、和稀泥、幻觉填充**，是真正懂 AI 协作才会写的规则。

---

## 二、已修复项

### P0-1｜superpowers-zh 与项目规范在同一份文件里互斥 ✅

`CLAUDE.md` 顶部注入块要求 TDD + 跑验证命令，同文件「测试要求」又写「本项目不做自动化测试」。

**修法（方案 B：保留框架 + 显式覆盖）**：在 `<!-- superpowers-zh:end -->` **之后**（L46）新增覆盖声明章节。

> **关键设计**：superpowers 重装只覆盖 `begin/end` 标记之间的内容。覆盖声明必须写在标记**之外**，否则重装即失效。声明内写明「若重装后此段消失，必须补回」。

内容：TDD / verification 不适用；「1% 也调用 skill」收窄为 4 个可用（brainstorming / systematic-debugging / requesting-code-review / using-git-worktrees），其余点名禁止。

顺带解决：`0X-xxx-test.md` 的 `-test` 明确为**手动验收清单**，不是自动化测试，禁止据此写测试代码。

### P0-2｜一份内容抄三份，且已漂移 ✅

| 文件 | 修复后 | 行数 |
|------|--------|------|
| `CLAUDE.md` | **唯一完整规范** + 新增「规范维护纪律」 | 130 → 182 |
| `CODEBUDDY.md` | 只剩 WorkBuddy 差异（MCP unityMCP、验收流程） | 43 → 26 |
| `SKILL.md` | 只剩触发条件 + 四步引导，明写「不复制任何条款」 | 100+ → 32 |

漂移证据（修复前）：SKILL.md 有「冲突以 `Docs/策划案.md` 为准」，CLAUDE.md 完全没有；CODEBUDDY.md 说策划案是唯一权威，但 CLAUDE.md 的加载指引表里策划案根本没进表。

**收敛时从副本抢救回 `CLAUDE.md` 的信息**（否则会丢）：
- 「冲突以 `Docs/策划案.md` 为准」→ 新增「文档冲突处理」章节
- `Docs/实现指导.md` 是关键决策源 → 同章节标注
- `architecture-index.md`（17 模块清单 + 依赖图）入口 → 补进「文档加载指引」

「规范维护纪律」写死：改规范只改 `CLAUDE.md`；发现副本不一致时**报告用户，不自行同步**。

### A 组｜规范与代码脱节 ✅

| 项 | 修复内容 |
|----|---------|
| A1 命名规范 | 改为**双轨制，以代码为准**：父状态无前缀（`GroundedState`/`AirState`/`DeadState`/`StunnedState`）；`Ground/` 叶子不加前缀（`IdleState`/`MoveState`…）；`Air/` 叶子加 `Air` 前缀。标注 `GroundStunnedState` 是历史例外、规范里曾出现的 `AirStunnedState` 代码中不存在；澄清顶层 `StunnedState` 与 `GroundStunnedState` 是两个不同类 |
| A2 文件组织表 | 补 `Assets/Scripts/SO/`（AttackConfig/CharacterConfig 实际在这）、`Configs/` 改为"枚举与轻量数据"、补 `Player/Input/`、`Player/Control/`、`UI/Views/`、`Assets/Editor/`；标注 `StunnedState` 在 `States/` 根 |
| A3 文档纪律第 3 条 | 「动手前先声明」→ **「读完后声明」**。原条款把一次交互变两次往返且零信息增量 |
| A4 加载指引表 | 新增一行：`Docs/实现指导.md`（弹反模型 / 攻击接线 / hitstop / 射箭 / 架势回复） |

### C 组｜Editor 代码规范 ✅

新增「编辑器扩展（`Assets/Editor/`）」小节，6 条，**每条都对应实际踩过的坑**：

1. 改序列化字段走 `SerializedObject` + `SerializedProperty`（否则 Undo 失效、不持久化）
2. **临时修改必须还原** —— 历史教训：`isReadable` 每次跑工具都设 true 且永不还原
3. UI 状态用 `[SerializeField]`（否则 domain reload 就重置）
4. `AssetPostprocessor` 只在首次导入（`importSettingsMissing`）接管，不无条件覆写
5. `OnGUI` 不做重活，耗时操作给进度条/Log
6. 校验规则统一写进 `ArpgValidationRules.cs`，**禁止另起一套**（两套规则必然漂移）

---

## 三、我判断错了一条（重要更正）

**原判**：`BossMoveEntry.cs:16 postureDamage=15f` 与 `AttackConfig.cs:81 PostureDamage=15f` 是重复定义，改一处漏一处必然出 bug。

**事实**：这是设计良好的**三级覆盖链**，不是重复定义：

```
招默认   BossMoveEntry(baseDamage/postureDamage/knockback/hitGrade) ← 真正的源，在 BossMoveTable SO
   ↓ 段覆盖     BossMoveWindow.overrideCombat ? w.xxx : entry.xxx
   ↓ 刀/箭覆盖  HitPulse / ArrowSpawnCue.overrideCombat ? pulse.xxx : fallback
   ↓ 烘焙进     AttackConfig（运行时只读产物）
```

两处 `15f` 是链路上不同环节的 C# 字段初值，运行时由 `BossAttackBaker` 单向烘焙，`GenichiroMoveCatalog` 用 `DefaultCombat` 填初值。**改数值应改 BossMoveTable SO，不是改 C# 默认值。**

**教训**：看到"两处同样的数字"不能直接定性为重复定义 —— 要先追数据流方向。grep 到字段就下结论、没查读取方，是静态分析的典型误判。

**真违反只有一处**：`AttackCombatResolve.DefaultCombat()` 按 HitGrade 硬编码战斗数值（Light=10/10、Mid=15/15、Heavy=25/25、箭 20/20），违反红线 5。但改它会影响 `GenichiroMoveCatalog` 生成的数值，需重跑烘焙 + 全招式回归，**与秋招时间账相比不划算，建议不动**。

---

## 四、剩余问题

### B 组（本次未做）

| # | 问题 | 说明 |
|---|------|------|
| 5 | 8 条红线零自动化检查，无 `Tools/spec-check.py`、无 `.codebuddy/settings.json` | 唯一能让红线真正自动生效的投入（约 1 小时）。可机械检查：全局命名空间 / OnTrigger / 伤害值归属 / UI 轮询 |
| 6 | `AttackCombatResolve.DefaultCombat()` 硬编码数值 | 见上，建议不动 |

### D 组（本次暂停，原因见文末）

| # | 问题 | 状态 |
|---|------|------|
| 8 | `Docs/architecture/a.md` 0 字节空文件 | ⏸ 暂停 |
| 9 | 仓库根 3 个 png 共约 20MB，应挪到 `Docs/references/pics/` | ⏸ 暂停（已确认无任何文档引用，可安全移动） |
| 10 | `Tools/__pycache__/` 未 gitignore | ⏸ 暂停 |

### C 组遗留

Editor 规范文档已补，但 `Assets/Editor/` 新增的 Guard/Validator/ModelImportEnforcer 等代码**尚未在 Unity 中编译验证**。

### 历史债（不在本次评审范围）

41 处 `CombatManager.Instance.*` 待迁移；`HitReactionUtil.IsPlayer` 用引用比较判阵营、缺 Faction 概念（会卡住多 Boss）；多 Boss + 复战是否真做未决。

---

## 五、事故记录：`Docs/` 目录整体消失（18:50）

**现象**：执行 `git mv` 将 3 个 png 移入 `Docs/references/pics/` 后，紧接着发现**整个 `Docs/` 目录从磁盘消失**（`ls: cannot access 'Docs/': No such file or directory`）。

`git status` 显示 60+ 个 ` D `（工作区删除、索引未变），涵盖 `Docs/architecture/` 全部、`Docs/references/` 全部参考图、`Docs/superpowers/` 全部 plans/specs、`Docs/total.md`、`Docs/策划案.md`、`Docs/实现指导.md` 等。

**诊断**：
- 磁盘剩余 187G，非空间不足
- 无 `.git/index.lock`，无并行 git 进程
- 索引完好 → 所有已跟踪文件可无损恢复

**恢复操作**（未使用 `git checkout -- .`，避免覆盖 `CLAUDE.md` 的未提交修改）：
```
cp CLAUDE.md /tmp/CLAUDE.md.bak      # 先备份
git reset -q HEAD -- .               # 取消暂存，索引回 HEAD
git checkout -- Docs/                # 只恢复 Docs
git checkout -- "boss架势条满—红点高亮.png" ...  # 单独恢复根目录 png
```
恢复后 `git status` 干净，60+ 个 ` D ` 全部消失，CLAUDE.md 修改完好（182 行，5 处改动全在）。

**损失**：`Docs/editor-review/ai-spec-review.md`（本文件）与 `guard-验收清单.md` 是**未跟踪文件**，无法从 git 恢复，已丢失。本文为重建版。

**原因未查明**，两种可能：
1. 有并行的 AI 会话 / 用户手动操作正在动这个项目的 `Docs/`
2. 杀毒软件或云同步（OneDrive/坚果云等）在扫描 E: 盘时误删

**建议排查**：确认是否有其他 WorkBuddy / Claude Code 窗口同时开着本项目的会话；确认 E: 盘是否有实时同步或杀软 quarantine 记录。

---

## 六、总体判断

按「能不能防住 AI 瞎写」这个唯一标准：

| 维度 | 修复前 | 现在 |
|------|--------|------|
| 规范**内容**质量 | 好 | 好 |
| 规范**执行**情况 | 好 | 好 |
| 规范**一致性** | 差（三副本漂移、命名脱节） | **已解决** |
| 规范**强制性** | 差（零自动化） | 仍差（B 组未做） |
| 规范**覆盖度** | 有盲区（5150 行 Editor 零规范） | **已补** |

内容层面做到了大多数人做不到的程度；剩下的只有一件事——把规范当**代码**来工程化：可自动校验（B 组第 5 项）。
