# 03 命中判定（M3 BoxCast + Hurtbox + CombatManager + M17 危字攻击）

> 模块：M3, M17
> 前置：M1, M2
> 验收：`03-hit-detection-test.md`

## 一、技术方案

**不用 OnTrigger**（框架红线）。用动画事件驱动 + Physics.BoxCast/SphereCast。

```
攻击动画播放
  ├─ 动画事件 "EnableHitbox" → 开启判定
  ├─ 每帧 BoxCast（武器上一帧位置 → 当前帧位置）→ 扫到 Hurtbox 或对方 Hitbox
  └─ 动画事件 "DisableHitbox" → 关闭判定
```

BoxCast 用"上一帧位置 → 当前帧位置"扫一段，防止高速挥砍穿透。

## 二、组件拆分

### Hitbox（挂在武器骨骼上）

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
EnableHitbox (动画事件)
  → Hitbox.isActive = true
  → 每帧:
      SphereCast(从 lastCastPos 到 当前 position)
        → 命中 Hurtbox → CombatManager.ReportHit
        → 命中 Hitbox → CombatManager.ReportClash (拼刀)
  → DisableHitbox (动画事件) → isActive = false
```

## 四、M17 危字攻击

- `AttackConfig.Perilous`（PerilousType：None/Thrust/Sweep/Grab）：招式带危字标记
- Boss AI 选到危字招式 → 发事件 `CombatEventBus.TriggerPerilousAttack(type)` → UI 弹"危" + 警示音
- 危字标记随 `HitData.isPerilous/perilousType` 传递（CombatManager 从 AttackConfig 读出传入 ReceiveHit）
- 防御（DeflectState）对危字无效
- **识破（Mikiri）**：突刺（Thrust）+ 玩家垫步 → `DodgeState.OnHitReceived` 拦截 → 切 `MikiriCounterState`
  （播踩刀动画、涨攻击者架势 `Config.MikiriPostureGain`、Perfect 打铁事件）→ 回 Idle

## 涉及文件

- 新建：`Assets/Scripts/Combat/Hitbox.cs`
- 新建：`Assets/Scripts/Combat/Hurtbox.cs`
- 新建：`Assets/Scripts/Combat/CombatManager.cs`
- 新建：`Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`
- 修改：`Assets/Scripts/SO/AttackConfig.cs`（Perilous）
- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（HitData 危字字段）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`（识破触发）
