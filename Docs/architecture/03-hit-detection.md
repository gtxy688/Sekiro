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

Boss **一条 Clip 多段出伤**：`hitPulses` 非空时，`AttackState` 按每段 `[start,end)` 脉冲开关刀。每次 `Enable` 清空 `hitTargets`，所以每段对同一目标只结算一次。空数组仍走上面的一对开关。玩家通常长度为 1（一刀）；用 `ARPG/攻击时间轴` 对着动画拖。

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

- `AttackConfig.Perilous`（PerilousType：None/Thrust/Sweep/Grab/JumpThrust）：招式带危字标记
- Boss AI 选到危字招式 → 发事件 `CombatEventBus.TriggerPerilousAttack(type)` → UI 弹"危" + 警示音
- 危字标记随 `HitData.isPerilous/perilousType` 传递（CombatManager 从 AttackConfig 读出传入 ReceiveHit）
- **段级危字**：`BossMoveWindow.perilous` 优先于招式的 `entry.perilous`，支持一招多段中仅某段危字
  （如 `Slash_SpinElbow` 的 `Elbow` 段 = Grab；烘焙时 `BossAttackBaker` 按 段级→招式级 回退）
- **危字应对配对（按类型，勿让 杠 z字类型串线）**：
  - `Thrust` 突刺 → 识破（Mikiri）或 弹反（弹反窗口内弹开 / 窗口外格挡，防御系有效）
  - `Sweep` 横扫 → 起跳踩头（空中被 Sweep 命中自动反制）或 垫步无敌帧躲避；**不可防御、不可识破**
  - `Grab` 抓取 → 弹反或垫步躲避；不可识破
  - `JumpThrust` 跳跃突刺 → 弹反或垫步躲避；**不可识破**（与地面突刺区分，独立枚举值）
  - `DeflectState` 仅对 Sweep 失效，Thrust/JumpThrust/Grab 正常走弹反/格挡
- **识破（Mikiri）**：仅 `Thrust` + 玩家垫步 → `DodgeState.OnHitReceived` 拦截 → 切 `MikiriCounterState`
  （播踩刀动画、涨攻击者架势 `Config.MikiriPostureGain`、Perfect 打铁事件）→ 回 Idle

## 涉及文件

- 新建：`Assets/Scripts/Combat/Hitbox.cs`
- 新建：`Assets/Scripts/Combat/Hurtbox.cs`
- 新建：`Assets/Scripts/Combat/CombatManager.cs`
- 新建：`Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`
- 修改：`Assets/Scripts/SO/AttackConfig.cs`（Perilous、HitboxSlot）
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（多 Hitbox 槽位）
- 修改：`Assets/Scripts/Boss/BossMoveWindow.cs`（段级危字、hitboxSlot）
- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（HitData 危字字段）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`（识破触发）
