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
      BoxCast(从 lastCastPos 到 当前 position)
        → 命中 Hurtbox → CombatManager.ReportHit
        → 命中 Hitbox → CombatManager.ReportClash (拼刀)
  → DisableHitbox (动画事件) → isActive = false
```

## 四、M17 危字攻击

### AttackConfig 增加

```csharp
public enum PerilousType
{
    None = 0,
    Thrust,  // 突刺 → 玩家可识破(Mikiri)，不可防御
    Sweep,   // 横扫 → 玩家必须起跳，不可防御
    Grab     // 抓取 → 玩家必须闪避，不可防御/弹反
}

// AttackConfig 加字段
public PerilousType perilousType = PerilousType.None;
```

### 危字标记的作用

- Boss AI 选到危字招式 → 发事件 `CombatEventBus.TriggerPerilousAttack(type)` → UI 弹"危"
- 玩家对突刺：朝敌人方向按闪避 → 触发 MikiriCounterState（识破踩刀）
- 玩家对横扫：跳起（跳跃本身有下段判定无敌）
- 玩家对抓取：闪避

### MikiriCounterState（识破）

- 叶子状态，放 GroundedState.SubStateMachine
- 触发：锁定中 + 敌人突刺 + 玩家朝敌方向闪避
- 播踩刀动画 → 动画事件检测 → 成功踩到则大幅涨敌架势 → 回 Idle
- 失败（没踩到）→ 取消闪避，照常受伤

### 危字伤害通过 HitData 传递

```csharp
// HitData 增加
public bool isPerilous;
public PerilousType perilousType;

// ReceiveHit 里：
// 危字攻击走 OnHitReceived 之前先检查玩家的应对
// 防御（DeflectState）对危字无效 —— 弹反不了突刺/横扫/抓取
```

## 涉及文件

- 新建：`Assets/Scripts/Combat/Hitbox.cs`
- 新建：`Assets/Scripts/Combat/Hurtbox.cs`
- 新建：`Assets/Scripts/Combat/CombatManager.cs`
- 修改：`Assets/Scripts/Configs/AttackConfig.cs`（PerilousType）
- 修改：`Assets/Scripts/FrameWork/States/Command.cs`（HitData 加危字字段）
- 新建：`Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`
