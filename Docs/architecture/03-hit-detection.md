# 03 命中判定（M3 BoxCast + Hurtbox + CombatManager + M17 危字攻击）

> 模块：M3, M17
> 前置：M1, M2
> 验收：`03-hit-detection-test.md`

## 一、技术方案

**不用 OnTrigger**（框架红线）。用动画事件驱动 + Physics.BoxCast/SphereCast。

```
攻击动画播放
  ├─ AttackState 读本招动画时间 t
  ├─ t 进入判定窗 → EnableWeaponHit
  ├─ 每帧 BoxCast（武器上一帧位置 → 当前帧位置）→ 扫到 Hurtbox 或对方 Hitbox
  └─ t 离开判定窗 / 退出状态 → DisableWeaponHit
```

BoxCast 用"上一帧位置 → 当前帧位置"扫一段，防止高速挥砍穿透。

### 场景接线（漏判的常见根因，必做）

| 对象 | 要求 |
|------|------|
| 玩家 `Hitbox` | 挂在**武器中央**（刃中段的子空物体，随刀动）。挂在根/柄/手骨上，第一刀碰巧扫到、第二刀弧线不同就会漏。 |
| Boss 刀 `Hitbox` | 挂在刀刃中段（`sword_joint` 子空物体）。拖到 `CharacterBody.weaponHitbox`，或留空由 Awake 自动找刀。 |
| Boss 肘/拳 `Hitbox` | `Elbow` 段是**拳头**攻击。挂在拳/指关节子空物体（随手骨动），**不要挂肘关节**。拖到 `CharacterBody.elbowHitbox`。`castRadius` 0.15～0.25。 |
| Boss `Hurtbox` 碰撞体 | **包住身体**（胶囊/盒与体型相当）。过小则只有部分挥砍能扫到，连招第二刀尤其明显。 |
| `Hitbox.targetLayers` | 含对方 Hurtbox 所在层 |
| 刀/肢体网格 | **不要**另挂会挡扫描的 Collider；Hitbox 只是扫描点 |

**多 Hitbox：** 招式数据写 `AttackHitboxSlot`（`AttackConfig.HitboxSlot` / `BossMoveWindow.hitboxSlot`）。`EnableWeaponHit` 按槽打开对应引用，**同时只亮一把**；缺肘引用则回退刀并 Warning。`Weapon` 永远是刀身份，不改成肘。`Slash_SpinElbow` 的 `Slash_Spin` 用刀，`Elbow` 段用肘（危字 Grab 不变）。

代码侧：到 `HitStartTime` 才开判定，到 `RecoveryWindowStart` 关判定（时钟是本招动画时间 `t`，可取消 = 判定段结束；退出再兜底关）。开判定时已重叠的目标先不算，等刀离开再扫进（连招防秒中）；每帧 SphereCast + OverlapSphere。

Boss **一条 Clip 多段出伤**：`hitPulses` 非空时，`AttackState` 按每段 `[start,end)` 脉冲开关刀。每次 `Enable` 清空 `hitTargets`，所以每段对同一目标只结算一次。空数组仍走上面的一对开关。玩家通常长度为 1（一刀）；用 `ARPG/攻击时间轴` 对着动画拖。多段连刀（飞舟 `Boat1` 等）在最后一刀结束前都会转向玩家，避免默认 `rotateEnd=0.35s` 导致后面几刀打空。

**段/刀/箭伤害：** 默认用招式行 `baseDamage` / `postureDamage` / `knockback` / `hitGrade`。`BossMoveWindow.overrideCombat` 覆盖整段；`HitPulse.overrideCombat` 覆盖这一刀；`ArrowSpawnCue.overrideCombat` 覆盖这一箭（优先于段）。解析顺序：刀或箭 → 段 → 招。弓段无近战红条，`ARPG/招式伤害` 下列出每支出箭。垫步/短位移段不配伤害。等级枚举 Light / Mid / Heavy 决定打到玩家时的受击/格挡/弹反。默认数字：箭 Light 10/10、Mid 15/15、Heavy 20/20；刀 Light 10/10、Mid 15/15、Heavy 25/25。`knockback` 仍给 Boss 被打用。`Bow_Air5` / `Kengeki_Air5` 前四箭 Light，最后一箭 Mid。

`ReportHit` 传入 `AttackConfig.HitGrade` 且 `isProjectile=false`；`ReportProjectileHit` 传入解析出的等级且 `isProjectile=true`。玩家受击读等级；Boss 受击仍读 knockback。

**无近战判定（弓段 / 垫步）：** 写成 `hitStartTime == recoverStart == comboWindowEnd == stateDuration`，`hitPulses` 空。`AttackState` / `EnableWeaponHit` 全程不开刀。**不要把红条缩成 0～0.01s**：进招第 0 帧 `animTime=0`，`0 >= 0 && 0 < 0.01` 仍会亮刀一帧。时间轴点「关闭近战判定」再保存。弓 Clip 上不要加 `EnableWeaponHit` 动画事件；箭走投射物。

### 射箭（投射物）

箭 Prefab 挂 `ArrowProjectile`（匀速直线 + 上一帧→当前帧 SphereCast）。**不要** `Hitbox`、**不要** Collider。出箭走招式表 `arrowCues`（相对本段动画 0 点），`BossAttackBaker` 烤进临时 `AttackConfig`，`AttackState` 读动画时间 `t` 到点调一次 `CharacterBody.SpawnArrow`。和 ♪ 音效同一套时间轴，**不要**在 Clip 上加 `SpawnArrow` 动画事件。方向锁开火瞬间指向玩家 `projectileAimPoint`（空则 Hurtbox 中心），不追踪。伤害用招式表：`AttackCombatResolve.Resolve(entry, window, cue)`（招默认，段覆盖，该支出箭 `overrideCombat` 可盖），**不**用弓段烘焙的 NoHit `AttackConfig`。命中 `CombatManager.ReportProjectileHit` → `ReceiveHit`（可弹反/格挡，垫步可躲，非危字）。**弹反箭不涨 Boss 架势、不把 Boss 弹进硬直。**

Boss 拖：`arrowSpawn`、`arrowPrefab`、`arrowSpeed`（约 32）、`arrowCastRadius`（约 0.08）、`arrowLifetime`（约 2）、`arrowTargetLayers`（与刀相同）。时间轴对弓段点「加出箭」；`Bow_Air5` 插 5 个点。「关闭近战判定」不会清出箭点。

## 二、组件拆分

### Hitbox（挂在武器中央）

```csharp
public class Hitbox : MonoBehaviour
{
    private CharacterBody owner;
    private Vector3 lastCastPos;
    private bool isActive;
    private HashSet<CharacterBody> hitTargets; // 已命中记录，防止一次挥砍多次伤害
    private AttackConfig config;               // 当前招式的伤害数据（来自 SO）

    public void Initialize(CharacterBody owner, AttackConfig config);

    public void Enable()   { isActive = true; lastCastPos = transform.position; }
    public void Disable()  { isActive = false; hitTargets.Clear(); }
}
```

### Hurtbox（挂在角色身体上）

```csharp
public class Hurtbox : MonoBehaviour
{
    private CharacterBody owner;
    public void Initialize(CharacterBody owner);
}
```

### CombatManager（中间层结算，用户决策：方案 B）

```csharp
public class CombatManager : MonoBehaviour
{
    public static CombatManager Instance;

    // Hitbox 每帧调这个，报告 "A 的武器扫到 B 的 Hurtbox"
    public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
    {
        // 1. 排除打到自己
        if (hitbox.Owner == hurtbox.Owner) return;

        // 2. 查全局规则（后续扩展：减伤 Buff、全场无敌等）
        // 3. 拼刀检测：扫到的是对方 Hitbox → 双方武器相交 → 触发拼刀
        // 4. 否则 → 调 target.ReceiveHit(...)
        CharacterBody target = hurtbox.Owner;
        CharacterBody attacker = hitbox.Owner;
        target.ReceiveHit(attacker, config.BaseDamage, config.PostureDamage, hitPoint);
    }

    // 拼刀：双方 Hitbox 相交
    public void ReportClash(Hitbox a, Hitbox b, Vector3 point);
}
```

**决策：Hit 扫到 Hurtbox → 报告 CombatManager → CombatManager 调 target.ReceiveHit**（低耦合，全局规则集中一处）。

## 三、判定流程

```
进入 AttackState → 等到 HitStartTime 再 EnableWeaponHit
  → Hitbox.isActive = true
  → 开判定时已重叠的目标先屏蔽，等刀离开再扫进才算新的一刀
  → 每帧:
      SphereCast(从 lastCastPos 到 当前 position) + OverlapSphere（内部重叠 SphereCast 会漏）
        → 命中 Hurtbox → CombatManager.ReportHit
        → 命中 Hitbox → CombatManager.ReportClash (拼刀)
  → RecoveryWindowStart → DisableWeaponHit（可取消 = 判定结束）
退出 AttackState → DisableWeaponHit → isActive = false（兜底）
```

## 四、M17 危字攻击

- `AttackConfig.Perilous`（PerilousType：None/Thrust/Grab；`Sweep` 枚举保留但已删除——未实现跳踩反制，`JumpThrust` 枚举保留但招式表不再标）
- Boss AI 选到危字招式 → 发事件 `CombatEventBus.TriggerPerilousAttack(type)` → UI 弹"危" + 警示音
- 危字标记随 `HitData.isPerilous/perilousType` 传递（CombatManager 从 AttackConfig 读出传入 ReceiveHit）
- **段级危字**：`BossMoveWindow.perilous` 优先于招式的 `entry.perilous`，支持一招多段中仅某段危字
  （如 `Slash_SpinElbow` 的 `Elbow` 段 = Grab；烘焙时 `BossAttackBaker` 按 段级→招式级 回退）
- **NoHit 段不弹危**：`canHit == false` 时烤成 `Perilous = None`，即使招式级标了危字
- **危字应对配对（按类型，勿让危字类型串线）**：
  - `Thrust` 突刺 → 识破（Mikiri）或 **弹反窗口内弹开**；**普通格挡等于没防**（全伤 + 受击）
  - `Grab` 抓取（Elbow 投技）→ **弹反窗口内弹开** 或 垫步躲避；不可识破；**普通格挡等于没防**。打中玩家后双方播 `Elbow_Danger`（成对投技，不瞬移，水平对视），播完回 Idle。扣血仍走招式表。
  - `JumpThrust`（招式 ID，不是危字类型）：起跳段 NoHit、无危字；**落地始终突刺**（横扫已删除，不再按命数分叉）。危字只在落地那一段 `AttackState.OnEnter` 弹出。落地突刺按 `Thrust` 应对（可识破）
  - `DeflectState`：危字先看弹反窗口；窗外 `OnHitReceived` 返回 false，走裸受击
- **Boss 危字 / 飞舟 / JumpThrust 全段不会被抓前摇打断**：挨打仍结算，招继续（见 `01-states.md` AttackState 霸体）
- **识破（Mikiri）**：仅 `Thrust` + **无方向键垫步** → `DodgeState.OnHitReceived` 拦截 → 切 `MikiriCounterState`
  （播踩刀动画、涨攻击者架势 `Config.MikiriPostureGain`、Perfect 打铁事件）→ 回 Idle。
  带方向垫步（后/左/右/前）即使踩中突刺也只走无敌帧，不识破。

## 涉及文件

- 新建：`Assets/Scripts/Combat/Hitbox.cs`
- 新建：`Assets/Scripts/Combat/Hurtbox.cs`
- 新建：`Assets/Scripts/Combat/CombatManager.cs`
- 新建：`Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`
- 修改：`Assets/Scripts/SO/AttackConfig.cs`（Perilous、HitboxSlot、HitPulse 刀伤害覆盖、arrowCues）
- 修改：`Assets/Scripts/SO/AttackWindowSync.cs`（NoHit / 假红条下限；不清 arrowCues）
- 新建：`Assets/Scripts/SO/AttackCombatResolve.cs`（刀 → 段 → 招）
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（多 Hitbox 槽位、SpawnArrow）
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`（arrowCues 到点出箭）
- 新建：`Assets/Scripts/Combat/ArrowProjectile.cs`
- 修改：`Assets/Scripts/Boss/BossMoveWindow.cs`（段级危字、hitboxSlot、段伤害覆盖、arrowCues）
- 修改：`Assets/Scripts/Boss/BossAttackBaker.cs`（拷 arrowCues，与 canHit 无关）
- 修改：`Assets/Editor/AttackTimelineWindow.cs`（出箭轨道）
- 修改：`Assets/Editor/BossMoveDamageWindow.cs`（招式伤害表）
- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（HitData 危字字段）
- 新建：`Assets/Scripts/FrameWork/States/Ground/GrabThrowState.cs`（Elbow 投技成对 `Elbow_Danger`）
- 修改：`Assets/Scripts/Combat/CombatManager.cs`（`TryStartGrabThrow`）
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`（受击中再吃 Grab 进投技）
