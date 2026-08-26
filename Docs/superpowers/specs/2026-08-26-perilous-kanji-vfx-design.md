# 危字 Billboard 特效 设计

日期：2026-08-26  
状态：已确认，按此实现

关系：覆盖 M17 危字提示的**表现**。不改危字战斗判定（识破 / 跳踩 / 不可防规则）。事件仍走 `CombatEventBus.OnPerilousAttack`。

## 目标

Boss 放出危字招式时，在 **Boss 头顶** 弹出只狼式红色「危」字：世界空间 Billboard、始终朝向相机、笔画中心更亮、外圈红光晕。约 0.8 秒后淡出。用已有白字黑底 `危.png`，不把字做成白芯描红边。

纠正现状：危字不得出现在玩家头顶或屏幕正中。

## 不做

- 不改 `OnPerilousAttack` 签名（仍只带 `PerilousType`）
- 不改 AttackState 发事件时机、不改危字 Hit 结算
- 不做墨迹粒子、不依赖 URP Bloom
- 不把危字做成 Screen Overlay 中央大红字
- 不新增 Overlay 相机（不复用 `LockOnOverlayCamera`）

## 规则

1. **跟随**：`CombatUIController` 把目标绑到 Boss。优先骨骼 `Head`；没有则 `Spine1`（再没有则 `Spine`）。世界坐标再沿 `Vector3.up` 抬 `headOffset`（默认 **0.35m**，可在 View 上调）。
2. **绘制**：世界空间两层 Quad（底光晕 + 面字形），不挂在 Screen Overlay Canvas 下。`LateUpdate` 里位置跟骨骼、旋转 `LookRotation(主相机.forward)`。这是跟随，不是轮询战斗数值。
3. **遮挡**：Shader `ZTest Always`、`ZWrite Off`、加法混合。头盔和身体挡不住字。不用单独 Overlay 层。
4. **相机背后**：跟踪点在主相机后方（到相机的 z ≤ 0）时隐藏。
5. **时间**：弹出（约 0.12s OutBack 放大）→ 保持 → 总时长约 **0.8s** 后淡出并 `Hide`。新事件来了 `DOKill` 后重开。
6. **谁触发都跟 Boss**：本战只有 Boss 危字。Controller 已有 `bossBody`，不跟玩家、不跟事件发送者。

## 贴图与 Shader

资源：白字黑底 `危.png`，放进 `Assets/Art/FX`。

沿用 `ARPG/FX/AdditiveSpark` 的遮罩思路（亮度当 alpha，黑不画），新建 `ARPG/FX/PerilousKanji`：

- 采样贴图，`mask = max(R,G,B,A)`，再 `_Cutoff` 压掉底噪
- 输出加法红：`rgb = lerp(_EdgeColor, _CoreColor, mask) * mask`
- `_EdgeColor` 默认深红，`_CoreColor` 默认偏粉的亮红（整字仍是红，不是白字）
- `Blend One One`，`Cull Off`，`ZTest Always`

两层共用一张贴图、同一个材质变体即可：

| 层 | 世界宽度 | 颜色倾向 | 作用 |
|---|---|---|---|
| 光晕 | 约 0.78m（字形 × 1.3） | `_EdgeColor`，亮度更低 | 外圈红光 |
| 字形 | 约 0.60m | 芯亮边深 | 可认的「危」 |

导入：Default 贴图、不打 Sprite Atlas；黑底当遮罩，不必单独 Alpha 通道。

## 实现落点

- 新建 `Assets/Shaders/FX/PerilousKanji.shader`
- 新建 `Assets/Editor/PerilousKanjiBuilder.cs`：菜单 **Tools/战斗/生成危字特效**（对标格挡火花 Builder：找贴图、改导入、生成材质和预制体、挂到 `CombatUIController.perilousWarningView`）
- 改 `PerilousWarningView.cs`：去掉对屏幕中央 TMP/Image 的依赖；驱动两层 Renderer 的弹出/淡出；`BindFollowTarget(CharacterBody boss)`；Billboard
- 改 `CombatUIController.cs`：`HandlePerilousAttack` 时 `BindFollowTarget(bossBody)`，不再假定 HUD 中央字
- 改 `Docs/architecture/06-presentation.md`：危字 = Boss 头顶 Billboard，不是 Overlay 中央字
- 改 `Docs/architecture/06-presentation-test.md`：恢复危字视觉验收（#14），与「M17 已移除」注释脱钩
- 不改 `03-hit-detection.md` 的判定规则；仅表现层文档

场景里旧的 HUD `PerilousWarning`（中央「危」字）关掉或删掉，避免两套。

## 你在 Unity 里要做的（代码改完后）

1. 把 `危.png` 放进 `Assets/Art/FX`（文件名含「危」即可，Builder 会找）。
2. 菜单 **Tools/战斗/生成危字特效**。
3. Play 一次：Boss 放突刺或横扫危字，看头顶红字。
4. 字偏高/偏低：调 `PerilousWarningView.headOffset`。偏大/偏小：调两层 Quad 的本地 Scale。
5. Ctrl+S 保存场景 / Prefab。

## 验收

| 操作 | 预期 |
|---|---|
| Boss 放突刺危字 | 红「危」出现在 Boss 头顶，不在玩家头上、不在屏幕正中 |
| Boss 放横扫危字 | 同样在 Boss 头顶弹出 |
| 绕到 Boss 侧面 / 背后 | 字始终正对相机 |
| 字与头盔重叠的机位 | 字仍完整可见 |
| 弹出后约 0.8 秒 | 淡出消失 |
| 连续两次危字 | 第一次被打断，重新弹出 |
| 玩家普通攻击 | 不出现危字 |

## 错误处理

- 找不到 `危.png`：Builder 弹窗，提示放到 `Assets/Art/FX` 或在 Project 里选中后再点菜单。
- 找不到 Head/Spine：退到 Boss 根坐标 + `headOffset`，不报错中断战斗。
- View 引用丢失：沿用现有 `BindPerilousView` 按名字查找；找不到则这次不显示，不抛异常。
