# 编辑器改进的验收清单

> **说明**：本文件曾在 18:50 的 `Docs/` 事故中丢失（未跟踪文件，git 无法恢复），这是重建版。
> **教训：新建文档要尽快 `git add`，否则同样的事故还会再丢一次。**

> **编译状态**：截至最后一次沟通，小金反馈「没报错」→ 代码已编译通过。若菜单看不到，多半是窗口位置问题而非代码问题。

涉及改动：

| 文件 | 类型 | 说明 |
|------|------|------|
| `Assets/Editor/KanjiTexUtil.cs` | 新增 | 汉字贴图处理，`isReadable` 临时开启 + 用后必还原 |
| `Assets/Editor/ArpgValidationRules.cs` | 新增 | 校验规则集（窗口与构建校验共用同一份） |
| `Assets/Editor/ArpgValidatorWindow.cs` | 新增 | 校验窗口 `Tools/战斗/战斗配置体检` |
| `Assets/Editor/ArpgBuildValidator.cs` | 新增 | 构建前校验，有 Error 直接中断 |
| `Assets/Editor/ArpgModelImportEnforcer.cs` | 新增 | 新导入 FBX 自动套用 Root Transform |
| `HealKanjiBuilder.cs` / `PerilousKanjiBuilder.cs` / `ReviveKanjiBuilder.cs` | 修改 | 删掉各自的 `PrepareTexture`/`BakeLuminanceMaskPng`，改调 `KanjiTexUtil`；删除死常量 `SekiroFx` |
| `AttackSlashBuilder.cs` / `DeflectSparkBuilder.cs` | 修改 | 删除死常量 `SekiroFx` |
| `ClipRenamer.cs` | 修改 | 删空 for 循环；修正与实现不符的注释 |
| `AttackTimelineWindow.cs` / `BossMoveDamageWindow.cs` | 修改 | 编辑目标字段加 `[SerializeField]` |

---

## 第 0 步：找到体检窗口

**操作**：顶部菜单栏 **Tools → 战斗 → 战斗配置体检**

**如果点不出来**（窗口可能开在屏幕外）：`Window → Layouts → Default` 重置布局，**不影响项目数据**，只是把窗口摆回默认位置。

**如果菜单里根本没有这一项**：说明 Unity 没识别到新文件，检查 `Assets/Editor/` 下是否有上表 5 个新增 .cs。

---

## 第 1 项：`isReadable` 不再泄漏

**操作**：

1. 菜单 `Tools/战斗/生成治愈特效` → 跑一次，弹窗点确定
2. Project 里选中 `Assets/Prefabs/FX/Heal/治.png`
3. Inspector 里看 **Read/Write Enabled**

**预期**：**未勾选**。

4. 同样跑 `生成危字特效`、`生成回生特效`，验证 `Assets/Prefabs/FX/Perilous/危.png` 和 `Assets/Prefabs/FX/Respawn/回生.png`

**预期**：两张都是**未勾选**。

**注意**：这三张图现在本来就是关的，所以这一项验的是「**下次跑工具不会再弄脏**」，不是「修复了当前问题」。改之前每跑一次工具就会设成 true 且永不还原，贴图常驻一份 CPU 副本（包体和内存翻倍）。

---

## 第 2 项：Guard 三件套

### 2.1 校验窗口能打开

**操作**：`Tools/战斗/战斗配置体检`

**预期**：窗口打开并**自动跑一次**，顶部显示「错误 N 条，警告 M 条」。

> 第一次跑大概率会有若干条——**这是好事**，说明校验器在工作。结果发我，我判断哪些是真问题、哪些是规则写太严。

### 2.2 能抓到「动画状态名写错」（最值钱的一条）

1. 打开 `Assets/SO/Boss/GenichiroMoveTable.asset`
2. 随便挑一个招式，把 `sequences → 分支0 → states` 里第一个动画名改成 `NotExist_Test123`
3. 回校验窗口点「重新校验」

**预期**：出现错误，类似
`GenichiroMoveTable / 招式[xxx]：Animator 里找不到状态「NotExist_Test123」，这个招不会触发。`

4. 点该条的「定位」→ Project 应高亮选中 `GenichiroMoveTable`
5. **改回原值**（别忘）

### 2.3 能抓到「假红条」（小金自己踩过的坑）

1. 还是那张表，找一个段窗口（`windows` 或 `sequences[x].windows`）
2. `hitStartTime` 设 `0`，`recoverStart` 设 `0.01`
3. 点「重新校验」

**预期**：出现错误，含「判定窗口只有 0.01s……这是假红条——第 0 帧会亮一帧刀」，并提示正确写法（四个字段写成相等）。

4. **改回原值**

> 规则来源：调箭矢招式时把判定条缩成 0~0.01 偷懒关判定，结果运行时莫名其妙被打。现在这条被固化成自动检查。

### 2.4 构建前会拦住

1. 先人为造一个错误（如 2.2 的 `NotExist_Test123`，保持不改）
2. `File → Build Settings → Build`

**预期**：构建**失败**，Console 红色日志 `战斗配置校验未通过，构建已中止：` 并列出具体条目。

3. **改回原值**再 Build

**预期**：构建正常进行。

### 2.5 新导入 FBX 自动套规范

1. 挑一个 `Assets/Sekrio/` 下的 FBX，**删掉它的 `.meta`**（或复制一个 FBX 进去当测试，验完删掉）
2. 回 Unity 等它重新导入

**预期**：Console 出现
`[导入规范] Assets/Sekrio/xxx.fbx：首次导入，已自动套用 Root Transform（N 个片段）。手工改过之后不会再被自动覆盖。`

**重要**：现有 855 个 FBX **一个都不会被动**——Postprocessor 只在 `importSettingsMissing`（首次导入/meta 丢失）时接管，已手工调过的配置绝不覆盖。

---

## 第 3 项：窗口状态不再丢

1. 菜单 `ARPG/攻击时间轴` 打开窗口
2. 拖入 `GenichiroMoveTable`，选到第 3 个招式、切到第 2 个动画分支
3. **随便找一个 .cs 加个空行保存**，触发重新编译
4. 等编译完成

**预期**：窗口**还停在**第 3 个招式第 2 个分支，**不需要重新拖招式表**。

5. 同样验证 `ARPG/招式伤害`：拖入招式表 → 触发编译 → 招式表还在

**说明**：`BossMoveDamageWindow` 的折叠状态（`fold` 字典）重载后会全部展开——Unity 序列化不了 Dictionary，全展开对用户无实际损失，不值得为此改数据结构。

---

## 第 4 项：死代码清理（无可见行为变化，仅核对）

这一项是清理性的，**跑起来看不出任何差别**，只需确认没弄坏东西：

| 位置 | 改动 | 核对方式 |
|------|------|---------|
| `AttackSlashBuilder.cs:13` | 删除 `SekiroFx = SekrioFx` 死常量 | 菜单 `Tools/战斗/生成挥刀刀光` 能正常跑完并弹窗 |
| `DeflectSparkBuilder.cs:13` | 同上 | `Tools/战斗/生成格挡火花` 能正常跑完 |
| `ClipRenamer.cs` | 删空 for 循环；头部注释改为「只有第一个改成 fbx 名，其余保留原名」 | `Tools/动画/Clip 重命名为 fbx 文件名` 行为不变 |

**背景**：`Assets/Sekiro/` 目录**根本不存在**（只有拼错的 `Assets/Sekrio/`），所以那个"正确拼写 fallback"天然失效——它跟 `SekrioFx` 是同一个值，等于没写。已加注释防止后人再补。

---

## 已知风险

| 项 | 说明 |
|---|------|
| `Assets/Sekrio/` 下若有非动画 FBX | Postprocessor 也会套 Root Transform。已确认 855 个 FBX 都在动画目录，风险低；误伤则排除对应子目录 |
| 校验规则可能偏严 | 第一次跑若出现大量警告，把结果给我判断该不该降级 |
| 段窗口可能被报两次 | 招式级 `windows` 与分支级 `sequences[].windows` 都会校验，路径不同属正常 |
