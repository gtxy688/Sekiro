# 肘击 Hitbox Slot 设计

日期：2026-08-26  
状态：已确认，按此实现

关系：补 `03-hit-detection.md` 的肢体判定挂点。不改 CombatManager 结算，不改 Grab 危字规则，不做射箭投射物。

## 目标

`Slash_SpinElbow` 的 `Elbow` 段用**拳头**上的 Hitbox 扫描，不再开刀刃那一把。旋转横砍段仍用刀。Grab 危字（弹反/格挡有效、不可识破）保持现状。槽位名仍叫 Elbow（招式/动画名），挂点是拳不是肘。

## 不做

- 不实现射箭投射物
- 不新增 Grab 状态或抓取动画
- 不改 `CombatManager.ReportHit` / `ReportClash` 规则
- 不把 `Kick` 接到脚上（枚举预留 `Kick`，本需求不拖引用、不改招式表）
- 不给 Hitbox 加 Collider，不用 OnTrigger
- 不把 `CharacterBody.Weapon` 改成肘（刀的身份不变；刀光/打铁默认仍认刀）

## 规则

1. **配置选槽，场景拖引用。** 招式数据只写枚举，不存场景对象。`CharacterBody` 上拖各槽的 `Hitbox`。
2. **同一时刻只亮一把。** 进入判定窗 Enable 目标槽，先 Disable 上一把。退出 `AttackState` 仍走 `DisableWeaponHit` 关当前那把。
3. **缺槽回退刀。** `Elbow` 未拖、或槽位未知 → 用 `Weapon`（刀），并打一次 Warning。旧场景挥砍不坏。
4. **玩家永远用刀。** 玩家 `AttackConfig.HitboxSlot` 保持默认 `Weapon`。时间轴编辑玩家招式时不显示槽位。
5. **扫描组件不变。** 肘用同一个 `Hitbox`（SphereCast + Overlap）。只换挂点和半径。

## 数据

新建 `Assets/Scripts/Configs/AttackHitboxSlot.cs`：

```csharp
public enum AttackHitboxSlot
{
    Weapon = 0, // 刀（默认）
    Elbow = 1,
    Kick = 2    // 预留，本需求不接线
}
```

| 位置 | 字段 | 默认 |
|------|------|------|
| `AttackConfig` | `HitboxSlot HitboxSlot` | `Weapon` |
| `BossMoveWindow` | `AttackHitboxSlot hitboxSlot` | `Weapon` |

`BossAttackBaker.Bake` 把 `w.hitboxSlot` 拷到 `cfg.HitboxSlot`。`NoHit` 段（不开判定）槽位无意义，保持默认即可。

`GenichiroMoveCatalog.Hit(...)` 增加可选参数 `slot`。仅这一处改调用：

```csharp
Hit(1.4f, perilous: PerilousType.Grab, slot: AttackHitboxSlot.Elbow)
```

`Slash_Spin` 段不写 slot。已用时间轴调过的 `GenichiroMoveTable.asset` **只改 Elbow 窗口的 `hitboxSlot`**，不要整表 `Apply()` 覆盖判定时间。

## 运行时

`CharacterBody`：

```csharp
[SerializeField] Hitbox weaponHitbox; // 可空：空则 Awake 自动找刀
[SerializeField] Hitbox elbowHitbox;  // Boss 肘击用；玩家不拖
[SerializeField] Hitbox kickHitbox;   // 预留，本需求不拖

public Hitbox Weapon { get; private set; }           // 永远是刀
public Hitbox ActiveHitbox { get; private set; }     // 当前亮着的那把；未开判定为 null
```

Awake：

1. 初始化所有非空 Hitbox 的 `owner`。
2. `Weapon = weaponHitbox != null ? weaponHitbox : 自动找`。
3. **自动找刀**：`GetComponentsInChildren<Hitbox>()`，跳过已指定的 `elbowHitbox` / `kickHitbox`，取第一把剩下的。禁止再 `GetComponentInChildren<Hitbox>()` 一把了事（挂上肘之后会认错）。

`EnableWeaponHit(AttackConfig config)`：

1. `ResolveHitbox(config.HitboxSlot)` → 目标。
2. 若当前 `ActiveHitbox` 不是目标，先 `Disable` 旧的。
3. 目标 `SetConfig` + `Enable`。
4. `ActiveHitbox = 目标`。
5. 仍发 `TriggerAttackSwingStart`（玩家刀光逻辑不变，FXManager 已过滤非玩家）。

`DisableWeaponHit()`：关 `ActiveHitbox`（没有则关 `Weapon`），然后 `ActiveHitbox = null`，仍发 `TriggerAttackSwingEnd`。

`ResolveHitbox`：`Elbow` 且引用非空 → 肘；否则 → `Weapon`。`Kick` 本需求走回退。

`AttackState.ApplyHitbox` 不改开关时机，只继续调这两个方法。

打铁火花：`CombatFxPoint.BetweenWeapons` 攻击者优先用 `ActiveHitbox`，没有再用 `Weapon`。这样弹反肘击时火花在肘附近，而不是刀尖。

## 场景接线（用户在 Unity 做）

代码合入后：

1. 播 `Elbow`，确认出拳的那只**手骨 / 指关节**（不要用肘关节）。
2. 该骨下建空物体，放到拳头中心（随手动）。
3. 挂 `Hitbox`，**不要加 Collider**。`targetLayers` 与刀相同。`castRadius` 0.15～0.25，`shaftRadius` 0.2～0.25。
4. 把该物体拖到 Boss `CharacterBody.elbowHitbox`。刀拖到 `weaponHitbox`（或留空走自动找，但必须排除拳）。
5. 时间轴打开 `Slash_SpinElbow` 第 2 段：槽位 = Elbow；按**拳面接触帧**再对红条（表里现有 1.12s–1.23s 是按刀标的，对完骨骼后重校）。

## 时间轴

`AttackTimelineWindow` 编辑 Boss 段时，在「第几段动画」下方加 `hitboxSlot` 枚举。保存写回 `BossMoveWindow`，不要被 `ApplyPulses` 清掉。玩家招式不显示此项。

## 文档

- `Docs/architecture/03-hit-detection.md`：场景接线表增加 Boss 肢体 Hitbox；写明「配置 slot + CharacterBody 引用；同时只亮一把；缺槽回退刀」。
- `Docs/architecture/03-hit-detection-test.md`：补 Elbow 验收（见下）。

## 验收

| 操作 | 预期 |
|------|------|
| 普通挥砍（玩家或 Boss 刀） | 仍用刀 Hitbox，手感与现在一致 |
| `Slash_SpinElbow` 第一段 `Slash_Spin` | 刀扫到才结算；肘采样点不开 |
| 第二段 `Elbow`，贴身让肘撞玩家 | 结算一次；危字 Grab；可弹反/格挡；垫步可躲；识破不触发 |
| `Elbow` 段刀从身侧刮过、肘没碰到 | 不结算（证明切到了肘槽） |
| Boss 未拖 `elbowHitbox` 仍放肘击 | 回退刀 + Console Warning，不报错 |
| 退出攻击 / 被弹开 | 肘 Hitbox 关闭，不会残留扫描 |

## 错误处理

- `config == null`：现有逻辑，不开判定。
- 槽位是 Elbow 但引用空：Warning，回退 `Weapon`。
- `Weapon` 也空：Enable/Disable 直接 return（与现在一致）。
- 切槽时必须先关旧再开新，避免两把同时 `isActive`。
