# 02 战斗数据（M2 属性 + M9 架势 + M14 复活 + M16 葫芦）

> 模块：M2, M9, M14, M16
> 前置：M1
> 验收：`02-combat-data-test.md`

## 一、CharacterConfig（SO，M2）

角色和 Boss 共用 CharacterBody 逻辑，数值各自配置。**数据全部走 SO，不硬编码。**

```csharp
[CreateAssetMenu(menuName = "Combat/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("血量")]
    public int maxHP;

    [Header("架势")]
    public float maxPosture;
    public float postureDecayRate;     // 架势自然回复速度（每秒）
    public float postureDecayDelay;    // 停止受击多少秒后开始回复

    [Header("受击")]
    public float stunDuration;         // 硬直时长

    [Header("移动")]
    public float moveSpeed;
    public float rotationSpeed;

    [Header("葫芦/复活（玩家专属）")]
    public int gourdCount;             // 初始葫芦次数 = 10
    public int reviveCount;            // 复活次数 = 1
}
```

CharacterBody 引用 `CharacterConfig config`，运行时从 config 读值初始化。

## 二、CharacterBody 战斗属性（M2）

```csharp
public CharacterConfig Config;   // 从 Inspector 拖入

// 运行时状态
public int CurrentHP { get; private set; }
public float CurrentPosture { get; private set; }
public int GourdRemaining { get; private set; }
public int ReviveRemaining { get; private set; }
public bool IsPostureBroken { get; private set; }  // 崩解中不累计/不回复
public float StunDuration => Config != null ? Config.StunDuration : 0.5f;  // 容错默认值

void InitCombat() // Awake 里调用
{
    CurrentHP = Config != null ? Config.MaxHP : 0;
    CurrentPosture = 0;
    GourdRemaining = Config != null ? Config.GourdCount : 0;
    ReviveRemaining = Config != null ? Config.ReviveCount : 0;
}

public void TakeDamage(int healthDmg, float postureDmg)
{
    if (CurrentHP <= 0) return;  // 已死不再结算
    CurrentHP -= healthDmg;
    AccumulatePosture(postureDmg);
    CombatEventBus.TriggerTakeDamage(this, healthDmg, Mathf.Max(CurrentHP, 0));
    CombatEventBus.TriggerHPChanged(this, Mathf.Max(CurrentHP, 0), Config.MaxHP);
    if (CurrentHP <= 0) HandleDeath();
}

public void AccumulatePosture(float amount)
{
    if (IsPostureBroken) return;  // 崩解中不累计
    CurrentPosture = Mathf.Min(CurrentPosture + amount, Config.MaxPosture);
    lastHitTime = Time.time;      // 重置架势回复延迟计时
    CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
    if (CurrentPosture >= Config.MaxPosture)
    {
        IsPostureBroken = true;
        CombatEventBus.TriggerPostureBroken(this);  // M10 接 EndureState
    }
}
```

## 三、架势系统（M9）

**规则（只狼）：**
- 受到伤害 → 架势 + 伤害量（防御也涨架势）
- 弹反成功 → 架势 + 少量（好的弹反不加）
- 架势满 → 崩解硬直 → 处决窗口
- 处决 → 清一条命 + 架势归零
- 一段时间不受击 → 架势缓慢回复

**实现：**
- 崩解 = 切 EndureState（M10），M2 先置 `IsPostureBroken` + 触发事件
- 架势自然回复写在 `CharacterBody.Update`（`UpdatePostureDecay`）：
  距上次受击超过 `PostureDecayDelay` 则每秒减 `PostureDecayRate`；崩解中不回复
- `ClearLife()` 供 M10 处决后调用：架势归零 + 崩解解除

## 四、葫芦（M16）

```csharp
public bool UseGourd()
{
    if (GourdRemaining <= 0 || CurrentHP >= Config.maxHP) return false;
    GourdRemaining--;
    CurrentHP = Mathf.Min(CurrentHP + healAmount, Config.maxHP);
    CombatEventBus.TriggerGourdUsed(this);
    return true;
}
```

- HealCommand 由 GroundedState 拦截 → 调 UseGourd + 播喝药动画
- 葫芦使用中不可移动/攻击（一个短喝药状态）

## 五、复活（M14）

```csharp
void HandleDeath()
{
    if (ReviveRemaining > 0)
    {
        ReviveRemaining--;
        // 弹复活提示（M13），按 R 回满血复活
        // 等待 reviveConfirm 输入，或超时死亡
    }
    else
    {
        // 真正死亡
        CombatEventBus.TriggerDeath(this);
    }
}

public void Revive()
{
    CurrentHP = Config.maxHP;
    CurrentPosture = 0;
    // 回满血 + 架势清零
}
```

- 复活直接回满血（用户决策）
- 复活后有短暂无敌（可选）

## 六、事件（新增，携带完整数据）

表现层不读 CharacterBody 内部字段，事件直接带数值（M2 未实现时表现层也能独立编译）。

```csharp
CombatEventBus:
  static event Action<CharacterBody, int, int> OnHPChanged      // (角色, 当前HP, 最大HP)
  static event Action<CharacterBody, float, float> OnPostureChanged  // (角色, 当前架势, 最大架势)
  static event Action<CharacterBody> OnPostureBroken
  static event Action<CharacterBody, int> OnGourdUsed           // (角色, 剩余次数)
  static event Action<CharacterBody> OnDeath
  static event Action<CharacterBody> OnReviveAvailable
```

## 涉及文件

- 新建：`Assets/Scripts/Configs/CharacterConfig.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
- 新建：`Assets/Scripts/Mgr/`（事件触发方法）
