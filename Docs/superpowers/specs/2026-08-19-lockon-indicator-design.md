# 锁定 / 忍杀图标跟随 设计

日期：2026-08-19  
状态：已确认，按此实现

## 目标

锁定白点（`FocusOn`）和忍杀标（`Finsher`）跟着 Boss **身体体积走**，不焊在根节点固定偏移上。Boss 转身、播动画时点仍在人身上。图标钉在 Spine1 世界坐标上，URP Overlay 相机画在最前，不被角色、刀、特效挡住。

## 不做
- 不按面向过滤锁定（背后仍可锁定）
- 不改 `SetLocked` / `SetFinisherReady` 事件接口
- 不改锁定键、相机锁定逻辑

## 规则

1. **跟踪点**：Boss 的 `Spine1`（没有则 `Spine`）世界坐标。不用胸口朝外偏移。正对、背对、侧对，点都在躯干中心。
2. **绘制**：Play 时从 Screen Overlay Canvas 拆出，改 World Space，位置每帧等于 `Spine1`。单独 Overlay 相机只画 `LockOnMarker` 层，叠在主相机上。
3. **显示**：未锁定且未崩解 → 两个都藏；锁定且未崩解 → 只显示 `FocusOn`；架势崩解可忍杀 → 只显示 `Finsher`。
4. **相机背后**：跟踪点在相机后方（`WorldToScreenPoint` 的 z ≤ 0）时隐藏。
5. **LateUpdate 例外**：`CombatUIController` 仍不轮询数值。仅 `LockOnIndicatorView` 每帧更新屏幕位置（跟随必须每帧做）。

## 实现落点

- `LockOnIndicatorView.cs`：跟 `Spine1`；`WorldToScreenPoint`；去掉挂 Boss 上的 billboard
- `CombatUIController.cs`：注入 Boss `CharacterBody` 给 View（或 View 从已有 `bossBody` 取骨骼）
- `CombatHUDBuilder.cs`：不再把锁定点建在 Boss 身上
- `Docs/architecture/05-input-lockon.md`、`06-presentation.md`、`Docs/验收清单.md`：世界空间挂 Boss → 屏幕 UI 跟随骨骼

## 你在 Unity 里要做的（代码改完后）

1. Hierarchy：把 Boss 下的 `LockOnIndicator`（带 `FocusOn` / `Finsher`）拖到 `CombatCanvas` 下面。
2. 该物体 RectTransform：锚点 (0.5, 0.5)，Pivot (0.5, 0.5)，Scale 改成 (1,1,1)。不要再用世界空间的 0.01。
3. 进 Play 前先调 `FocusOn` / `Finsher` 的屏幕尺寸（从世界 Canvas 挪过来会显得很小或很大）。
4. `CombatHUD` 上 `Lock On Indicator View` 仍指向这个物体。
5. Boss Prefab 上那份旧 `LockOnIndicator` 关掉或删掉，避免两套。

## 验收

| 操作 | 预期 |
|---|---|
| 不锁定 | 没有白点、没有忍杀标 |
| 锁定，Boss 正对 / 背对 / 侧对 | 都能锁；白点在人身上，不被模型挡住 |
| Boss 播攻击、弓、倒地 | 白点跟着躯干走，不留在原地 |
| 架势崩解 | 白点换成 `Finsher`，同样跟身体、不被挡 |
| 处决结束 / 解锁 | 图标按规则关掉或回到 `FocusOn` |
