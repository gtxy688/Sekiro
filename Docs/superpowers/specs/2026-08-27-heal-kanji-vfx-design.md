# 治愈「治」字 Billboard 特效 设计

日期：2026-08-27  
状态：已确认，按此实现

关系：覆盖 M16 葫芦的**表现**。不改喝药判定、扣次数、回血、可打断规则。事件仍走 `CombatEventBus.OnGourdUsed`。

视觉参考：用户提供的绿色「治」字 + 外圈绿雾 + 火星截图。结构对齐已落地的危字 Billboard（`PerilousWarningView`），颜色与层次换成治愈绿。

## 目标

玩家按 E 喝药（`UseGourd` 成功）时，在 **玩家头顶** 弹出只狼式绿色「治」字：世界空间 Billboard、始终朝向相机、笔画芯近白薄荷绿、边翠绿、外圈绿光晕，背后有缓慢旋转的软绿雾，四周几点绿火星。约 0.8 秒后淡出。贴图用白字黑底 `Assets/Sekiro/FX/治.png`。

## 不做

- 不改 `OnGourdUsed` 签名、不改 `HealState` / `UseGourd` 时机与数值
- 不依赖 URP Bloom（白芯靠加法叠亮）
- 不把手绘体积烟做成独立雾气贴图（没有那张图；雾用程序径向光晕近似）
- 不把「治」做成 Screen Overlay 中央字，不新增 Overlay 相机
- 不把危字和治字抽成通用基类（两套预制体、两套 View，避免为两个实例做抽象）
- 不改危字跟随目标、颜色、判定

## 规则

1. **触发**：`CombatUIController` 已订阅 `OnGourdUsed`。仅当 `c == playerBody` 时播治愈特效（同时保留现有葫芦数字刷新）。没药导致 `UseGourd` 失败不会发事件，不播。
2. **跟随**：绑到 **玩家**。优先骨骼 `Head`；没有则 `Spine1` / `Spine`；再没有则玩家根。世界坐标再沿 `Vector3.up` 抬 `headOffset`（默认 **0.55m**，可在 View 上调）。不跟 Boss。
3. **绘制**：世界空间多层 Quad + 一个 ParticleSystem，不挂 Screen Overlay Canvas。`LateUpdate` 位置跟骨骼、旋转 `LookRotation(主相机.forward)`。这是跟随，不是轮询战斗数值。
4. **遮挡**：字形 / 光晕 / 雾 Shader 均 `ZTest Always`、`ZWrite Off`、加法混合。头盔挡不住。
5. **相机背后**：跟踪点在主相机后方时隐藏 Renderer 并停粒子。
6. **时间**：弹出（约 0.12s OutBack 放大）→ 保持 → 总时长约 **0.8s** 后淡出并 `Hide`。新的喝药事件 `DOKill` 后重开。喝药被受击打断时特效仍播完（它绑在 `OnGourdUsed`，不绑 `HealState.OnExit`）。
7. **与危字并存**：Boss 危字仍在 Boss 头上；玩家喝药只出治字。两套互不抢同一 View。

## 贴图与 Shader

贴图：`Assets/Sekiro/FX/治.png`（白字黑底当亮度遮罩）。Builder 先按该路径加载；找不到再在 `Assets/Sekiro/FX` 和 `Assets/Art/FX` 里按文件名含「治」搜索。

字形 / 外圈字光晕：复用已有 `ARPG/FX/PerilousKanji`（不新建第二套字形 Shader）。材质换绿、换贴图：

| 属性 | 字形 Core | 字光晕 Glow |
|---|---|---|
| `_CoreColor` | `(1.00, 1.00, 0.92)` 近白薄荷 | `(0.35, 0.95, 0.45)` |
| `_EdgeColor` | `(0.08, 0.72, 0.28)` 翠绿 | `(0.02, 0.32, 0.12)` |
| `_Thickness` | `0.55` | `0.80` |
| `_Cutoff` | `0.08` | `0.06` |
| 世界宽度 | 约 0.60m | 约 0.78m |

绿雾：新建 `ARPG/FX/HealAura`。无贴图。UV 径向衰减（中心亮、边缘 0）+ 轻微角向扰动，输出加法绿。默认 `_Color (0.04, 0.55, 0.22)`、`_Intensity` 可被 View 的 MPB 带动画。Quad 宽约 **1.35m**，`LateUpdate` 里绕相机前向慢转（约 25 deg/s），做出图里那圈转雾。

火星：预制体子物体 `Sparks` 挂 `ParticleSystem`。Additive、绿色小点、Burst 约 8～12 颗、寿命 0.4～0.8s、轻微上浮与径向散开。`Show` 时 `Play`，`Hide` 时 `Stop`+清。材质可用 `ARPG/FX/AdditiveSpark` 或粒子默认加法，不依赖 URP Bloom。

导入 `治.png`：Default、sRGB、黑底当遮罩、Clamp、Bilinear、不开 mip、不打 Sprite Atlas。与危字 Builder 的 `PrepareTexture` 相同。

## 实现落点

- 新建 `Assets/Shaders/FX/HealAura.shader`
- 新建 `Assets/Scripts/UI/Views/HealKanjiView.cs`：照 `PerilousWarningView` 的弹出/淡出/Billboard；额外驱动 Aura Renderer 旋转和 Sparks
- 新建 `Assets/Editor/HealKanjiBuilder.cs`：菜单 **Tools/战斗/生成治愈特效**。输出 `Assets/Prefabs/FX/Heal/`（`HealKanji.prefab`、Core/Glow/Aura 材质）。场景实例名 `HealKanji`，挂到 `CombatUIController.healKanjiView`
- 改 `CombatUIController.cs`：`HandleGourdUsed` 在刷新葫芦数字后 `BindFollowTarget(playerBody)` + `ShowHeal()`；`Start` 里 `BindHealKanjiView`（按名字 `HealKanji` 查找，与危字同一套路）
- 改 `Docs/architecture/06-presentation.md`：补治愈 Billboard 小节
- 改 `Docs/architecture/06-presentation-test.md`：#15 旁加治字视觉项
- 不改 `02-combat-data.md` 的葫芦数值规则

## 你在 Unity 里要做的（代码改完后）

1. 确认 `Assets/Sekiro/FX/治.png` 在工程里（或文件名含「治」）。
2. 菜单 **Tools/战斗/生成治愈特效**。
3. Play：挨一刀后按 E，玩家头顶出绿「治」。
4. 高低调 `HealKanjiView.headOffset`；大小调 Core/Glow/Aura 的 Scale。
5. Ctrl+S 保存场景 / Prefab。

## 验收

| 操作 | 预期 |
|---|---|
| 受伤后按 E 喝药 | 绿「治」出现在 **玩家头顶**（不在 Boss 头上、不在屏幕正中）；芯近白、边绿、外圈绿雾在转、有几点绿火星；始终朝向相机 |
| 绕到玩家侧面再喝 | 字仍正对相机 |
| 弹出后约 0.8 秒 | 淡出消失 |
| 连续两次喝药（有剩余次数） | 当前 `HealState` 禁止喝药中再喝，两次特效通常不重叠；若 0.8s 内再次收到 `OnGourdUsed`，第一次被打断并重新弹出 |
| 没药时按 E | 不出现治字 |
| Boss 放危字 | 仍只在 Boss 头顶出红「危」，玩家头上不出现「治」 |
| 喝药中被打断 | 治字继续播完淡出，不跟受击一起立刻消失 |
| 右下角葫芦 | 次数仍减少、数字仍闪烁（旧 #15 不变） |

## 错误处理

- 找不到 `治.png`：Builder 弹窗，提示放到 `Assets/Sekiro/FX` 或在 Project 里选中后再点菜单。
- 找不到 Head/Spine：退到玩家根 + `headOffset`，不报错中断战斗。
- View 引用丢失：`BindHealKanjiView` 按名字查找；找不到则这次不显示，不抛异常。
- 没有 ParticleSystem：只播字和雾，不报错。
