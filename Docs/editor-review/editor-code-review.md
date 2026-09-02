# 编辑器模块代码审查

> **说明**：本文件曾在 18:50 的 `Docs/` 事故中丢失（未跟踪文件，git 无法恢复），这是重建版，**已同步到当前代码状态**。
> **教训：新建文档要尽快 `git add`。**

审查范围：`Assets/Editor/` 全部脚本，约 4800 行
审查依据：项目 `CLAUDE.md` 架构约束与代码规范、Unity Editor 扩展工程实践

结论一句话：**会写 Editor API，但只有"帮你生成"的工具，没有"拦住你犯错"的机制；重复代码和状态持久化是两颗明确的地雷。**

---

## 修复状态总表（截至 2026-09-01 21:40）

| 编号 | 问题 | 状态 |
|------|------|------|
| P0-1a | `isReadable` 泄漏 | ✅ **已修**（`KanjiTexUtil`） |
| P0-1b | 覆写源 PNG | ❌ 未修 — 决定不做，见下 |
| P0-2 | 零 Guard 机制 | ✅ **已修**（校验规则集 + 窗口 + 构建拦截 + 导入规范） |
| P0-3 | 窗口状态不持久化 | ✅ **已修**（两个 Window 加 `[SerializeField]`） |
| P0-4 | 三份 KanjiBuilder 重复 | ⚠️ **部分修** — 见下 |
| P0-5 | 无 asmdef | ❌ 未修 — 决定不做 |
| P1-1~6 | 规范与性能 | ❌ 全部未修 |
| P2-1~3 | 可选改进 | ❌ 全部未修 |
| 死代码 ×3 | `SekiroFx` 死常量、空 for 循环、注释与实现不符 | ✅ **已修** |
| 清理 | 一次性 Builder 归档 + 冗余工具删除 | ✅ **已做** — 见第七节 |

**关于 P0-4 只修了一半**：完整重构（合并三个 Builder）**没做，也不该做**——它们是一次性脚本，已跑完、产物已固化为 prefab，重构零收益还担着把 prefab 搞坏的风险。
但**修 `isReadable` 必须同时改三个文件里的同一段代码**，不改一处漏两处是迟早的事，所以把重复最狠的两个方法（`PrepareTexture` / `BakeLuminanceMaskPng`）抽成了 `KanjiTexUtil`。三个 Builder 剩下的重复（`CreateMat` / `CreateQuad` / `PlaceInScene`）**原样留着**。

**关于 P0-1b 覆写源 PNG 不做**：工具已跑完、产物已固化，不重跑就没有损失；改它反而有动到已生成 prefab 的风险。

**关于 P0-5 asmdef 不做**：项目已完成，编译隔离收益不显著。

---

## 一、现状盘点（审查时）

| 项目 | 数值 |
|------|------|
| 编辑器脚本 | 20 个 / 约 4800 行 |
| 最大文件 | `AttackTimelineWindow.cs` 1044 行 |
| 程序集定义（.asmdef） | **0 个** |
| `[SerializeField]`（窗口状态持久化） | **0 处** |
| `EditorUtility.DisplayProgressBar` | **0 处** |
| `AssetPostprocessor` | **0 个** |
| `IPreprocessBuildWithReport` | **0 个** |
| `PropertyDrawer` | **0 个** |
| `AssetDatabase.StartAssetEditing` | **0 处** |

工具分两类，性质完全不同：

| 类别 | 文件 | 性质 |
|------|------|------|
| **A. 一次性搭建 Builder** | `CombatHUDBuilder`(786) `LockOnCameraBuilder`(222) `DeflectSparkBuilder`(354) `AttackSlashBuilder`(137) `HealKanjiBuilder`(268) `PerilousKanjiBuilder`(282) `ReviveKanjiBuilder`(230) `BatchRootTransformSettings`(92) `ClipRenamer`(69) `InPlaceClipGenerator`(115) | 跑一两次就完事 |
| **B. 持续使用 Window** | `AttackTimelineWindow`(1044) `BossMoveDamageWindow`(421) `GenichiroMoveCatalogExporter`(459) `AttackTimelinePreview`(150) `AttackTimelineClipFinder`(59) `AttackTimelinePrefs`(45) | 调参循环里反复打开 |

**关键观察**：A 类约占 53% 代码量但使用频次极低，**投入产出比倒挂**。

---

## 二、P0 项（原判必修）

### P0-1 汉字贴图处理 —— 部分已修

**原问题**：三份 KanjiBuilder 各有一份相同的 `BakeLuminanceMaskPng` + `PrepareTexture`，后者把贴图设成 `isReadable = true` 且**从不还原**；前者直接 `File.WriteAllBytes` 覆写源 PNG。

**已修（a）**：抽出 `KanjiTexUtil`，`isReadable` 只在读像素期间临时开启，用完连同压缩设置一起还原（提前 return 的分支也会走到还原）。

**未修（b）**：覆写源 PNG —— 决定不做，理由见状态总表。

### P0-2 零 Guard 机制 —— ✅ 已修

**原问题**：20 个工具全是「帮你生成」，**没有一个会拦住你犯错**。招式表里动画状态名写错（`Kengeki_Thrust` vs `Kengeki_Thrust2`），Play 之后不报错，只是招式不触发。

**已修**：补了三样 + 一个共用规则集

- `ArpgValidationRules.cs` —— 规则集，窗口与构建校验**共用同一份**（两套规则必然漂移）
- `ArpgValidatorWindow.cs` —— `Tools/战斗/战斗配置体检`，结果可点击跳转
- `ArpgBuildValidator.cs` —— `IPreprocessBuildWithReport`，有 Error 抛 `BuildFailedException`
- `ArpgModelImportEnforcer.cs` —— `AssetPostprocessor`，`Assets/Sekrio/` 下 FBX 首次导入自动套 Root Transform

**校验规则**：动画状态名在 controller 中存在 / 判定窗口时序 / **假红条** / hitPulses 区间 / cue 时间越界 / id 空或重复 / minRange>maxRange / 伤害为负。

> **假红条规则是从小金自己踩的坑里提炼的**：调箭矢招式时把判定条缩成 0~0.01 想关掉判定，结果运行时莫名其妙被打（进招第 0 帧 `animTime=0`，`0>=0 && 0<0.01` 亮一帧刀）。现在它会主动拦，并给出正确写法。

**Postprocessor 安全设计**：只在 `importSettingsMissing`（首次导入/meta 丢失）时接管，**现有 855 个 FBX 一个都不会被动**。

### P0-3 窗口状态不持久化 —— ✅ 已修

**原问题**：`AttackTimelineWindow` 的 `playerConfig`/`bossTable`/`moveIndex` 等无 `[SerializeField]`，每次改代码触发 domain reload 就清空，得重新拖。讽刺的是同文件 `previewHeight` 反而用了 `EditorPrefs`。

**已修**：两个 Window 的编辑目标加 `[SerializeField]`。`BossMoveDamageWindow` 的 `fold` 字典不持久化——Unity 序列化不了 Dictionary，全展开对用户无损失，不值得改数据结构。

### P0-4 三份 KanjiBuilder 近乎全量重复 —— 部分修

见状态总表说明。约 780 行里六组方法逐行雷同（`PrepareTexture` / `BakeLuminanceMaskPng` / `CreateMat` / `CreateQuad` / `PlaceInScene` / `EnsureFolder`）。

顺带发现 `EnsureFolder` 有**三种不同实现**——`AttackSlashBuilder` 是递归版，`HealKanjiBuilder` 是只支持两级路径的简化版，深层路径在简化版下会静默失效。这个**未修**（属于"不好看但不咬人"）。

### P0-5 无 asmdef —— 未修

所有代码挤在 `Assembly-CSharp` / `Assembly-CSharp-Editor` 两个预定义程序集里，编辑器代码可引用任何运行时代码，无编译期隔离。**决定不做**：项目已完成，收益不显著。

---

## 三、P1 项（原判建议，全部未修）

| # | 问题 | 位置 |
|---|------|------|
| 1 | 批量导入无进度条、无 `StartAssetEditing` 包裹 | `BatchRootTransformSettings` `ClipRenamer` `InPlaceClipGenerator` —— 每个文件都 `SaveAndReimport()`，百来个 FBX 会卡死编辑器几十秒且无反馈 |
| 2 | 布局/相机/特效参数硬编码 | `CombatHUDBuilder`（约 40 处）、`LockOnCameraBuilder`、`DeflectSparkBuilder` |
| 3 | MenuItem 入口混乱 | 根目录 `ARPG/` 与 `Tools/` 两套；`ARPG/` 下中英混杂（`ARPG/攻击时间轴` vs `ARPG/Sync GenichiroMoveCatalog from Move Table`） |
| 4 | 工具方法在两个 Window 间逐字重复 | `SegmentCount` / `FirstSequence` / `CurrentSequence` / `SequenceLabel` / `StateName` |
| 5 | 预览每帧重采样 + 无条件 Repaint | `AttackTimelineWindow` —— 每帧 `animator.Play()` + `Update(0f)`，`MouseDrag` 时无条件 `Repaint()` 导致 PreviewRenderUtility 每帧离屏渲染 |
| 6 | 批量改动无 Undo 兜底 | `ClipRenamer` `BatchRootTransformSettings` —— ModelImporter 改动不进 Undo 系统 |

---

## 四、P2 项（原判可选，全部未修）

| # | 问题 | 说明 |
|---|------|------|
| 1 | `GenichiroMoveCatalog` 双真值源 | **⚠️ 原判断已修正，见第七节**。运行时只读 SO，Catalog 在运行时**零调用点**，不构成双真值源。原建议「砍掉 C# 那份」**作废**——它是招式表的唯一备份 |
| 2 | `AttackConfigEditor` 只有 19 行 | 只放了跳转按钮，可加判定窗口可视化条、时序校验、动画名校验 |
| 3 | Builder 未防 Play 模式 | `AttackSlashBuilder` / `DeflectSparkBuilder` 在 Play 模式下跑，退出后一切消失，用户以为配好了 |

---

## 五、审查报告自身的错误修正

原 P1 里有一条：*「`LoadFx` 的 fallback 路径 `Assets/Sekrio/` 与 `SekrioFx` 同义（冗余判断）」* —— **这条判断是错的**。

实际是两条不同路径，fallback 有意义：

| 表达式 | 实际路径 |
|--------|---------|
| `SekrioFx + "/" + name` | `Assets/Sekrio/FX/xxx.png`（FX 子目录） |
| `"Assets/Sekrio/" + name` | `Assets/Sekrio/xxx.png`（根目录） |

**若照原报告去"删掉冗余 fallback"，会破坏查找功能。此条作废。**

---

## 六、遗留事项

1. **验收尚未完成** —— 见 `guard-验收清单.md`。代码已编译通过（小金反馈"没报错"），但体检菜单是否出现、窗口行为是否正确，未确认。
2. **P1/P2 全部未动** —— 都是"不好看但不咬人"的类别。若继续做多 Boss，建议至少处理 P1-3（菜单入口混乱）和 P1-4（两 Window 间重复方法）。
3. **编辑器模块在架构文档体系中仍无条目** —— `CLAUDE.md` 的文档加载指引表（01-07）没有编辑器项。目前编辑器规范写在 `CLAUDE.md` 的「代码规范 → 编辑器扩展」章节（6 条），是否要单开 `08-editor-tools.md` 待定。

---

## 七、编辑器代码清理（2026-09-02）

### 做了什么

| 动作 | 文件 | 行数 |
|------|------|------|
| **删除** | `CombatHUDBuilder` `DeflectSparkBuilder` `LockOnCameraBuilder` `AttackSlashBuilder` `ReviveKanjiBuilder` `HealKanjiBuilder` `PerilousKanjiBuilder` `KanjiTexUtil` | 2236 |
| **删除** | `InPlaceClipGenerator` `ClipRenamer` | 180 |
| **恢复** | `BatchRootTransformSettings` | 92（11:23 从 HEAD 恢复） |

**为什么直接删而不是归档**：用户原话（11:15 截图）——「生成一遍就行了，留着干啥」。这 6 个生成式 Builder（产出 HUD/相机/特效 prefab 已固化）和 1 个被它们独占依赖的工具类 `KanjiTexUtil`，跑完就废，留着只会让人怀疑"是不是还得跑一次"。

**为什么删 `InPlaceClipGenerator` / `ClipRenamer`**：用户 11:23 最新指令——三个动画类菜单「只保留批量设置」。这两个虽然在原 9 个生成式截图之外，但既然用户的判断标准是"工序工具也用不上几次"，就一起删了。**代价**：下次加新攻击动画，得手写原地 clip 处理流程（剔除 RootT/RootQ 根骨骼曲线）或重写工具。

**为什么删 `KanjiTexUtil`**：grep 确认它的 5 处引用全在三个 KanjiBuilder 里。三个 KanjiBuilder 删了，它就成了孤儿工具类。

**为什么恢复 `BatchRootTransformSettings`**：用户 11:23 明确指定「只保留批量设置」。我之前误判它为冗余（数值与 Enforcer 逐行一致），但用户实际期望**保留手动入口作为 Enforcer 的补充**——已改回 `ArpgModelImportEnforcer` 里两处引用它的注释。

清理后：主目录 **13 个脚本 / 2951 行**（原 23 个 / 5367 行；删 9 个共 2416 行，恢复 1 个 92 行）。

### 没删的，以及为什么

| 文件 | 保留理由 |
|------|---------|
| `GenichiroMoveCatalogExporter`(459) + `GenichiroMoveCatalog`(299，运行时) | **招式表的唯一备份**，见下 |
| `BatchRootTransformSettings`(92) | **11:23 用户最终指令保留**。手动入口作为 Enforcer 自动接管的补充，已恢复 |
| Guard 四件套 + 两个 Window + 2 个 CustomEditor | 持续使用 |

### ⚠️ 关键发现：`GenichiroMoveCatalog` 是招式表的唯一备份

原 P2-1 把它判成「双真值源」，建议砍掉 C# 那份。**这个判断错了**，实证如下：

```
BossMoveTable.asset   ← 运行时唯一数据源（BT 节点全部引用它）
   │
   ├─(菜单 ARPG/Sync GenichiroMoveCatalog)──►  GenichiroMoveCatalog.cs（299 行硬编码快照）
   │
   └─(Inspector「填入弦一郎默认招式表」/ 新建 asset)◄── GenichiroMoveCatalog.Apply()
```

- 在 `Assets/Scripts/` 里 grep `GenichiroMoveCatalog`，**运行时零调用点**，只有 `BossMoveTableEditor` 在用
- 所以它不是"第二份真值"，而是 **Editor 侧的默认值快照**
- 而这份快照，是当前招式表**唯一的冗余副本**——`BossMoveTable.asset` 只有一份，没有别的备份

那几十个小时一帧帧对出来的判定时间，全压在这一份 asset 上。删 Catalog = 拆掉保险。

**建议动作**：跑一次 `ARPG/Sync GenichiroMoveCatalog from Move Table`，然后 `git diff`。如果 Catalog.cs 有变化，说明你后来调过的数值还没进备份。

### 事故记录：文件系统三次清空（根因未查明）

本次清理过程中文件反复被清空，是拖慢进度的主要原因：

| 时间 | 现象 | 恢复方式 |
|------|------|---------|
| 09-01 18:50 | `Docs/` 整体消失 | 未跟踪文件，git 救不回来，三份文档全部重建 |
| 09-02 09:46 | 3 个 Docs 文件消失 | 已跟踪，`git checkout HEAD --` 恢复 |
| 09-02 09:52 | `Assets/Editor/` **整个目录**被清空 | 已跟踪，`git checkout HEAD --` 恢复 |

**同期线索**：`.git/index.lock` 反复出现（有并发 git 进程）；我的 `git` 命令两次被 SIGTERM 中断，**每次中断后都紧跟着发生文件删除**。

**两条硬结论**：

1. **开工前先提交** —— 这次能 100% 恢复，全靠清理前那次「安全提交」。没有它，5367 行编辑器代码就没了。
2. **`.gitignore` 第 70 行 `*.meta` 是错的** —— 整个项目的 `.meta` 都没入库。Unity 的引用全靠 `.meta` 里的 GUID，不入库意味着项目完全依赖本机磁盘状态。Editor 脚本的 meta 丢了影响小（不挂 prefab），但**贴图 / prefab / 场景 / 动画 clip 的 meta 一旦丢失或重建，引用会全崩**。建议把这条从 gitignore 删掉并提交一次。
