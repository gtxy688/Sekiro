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
public CharacterConfig Config;

// 运行时状态
public int CurrentHP { get; private set; }
public float CurrentPosture { get; private set; }
public int GourdRemaining { get; private set; }
public int ReviveRemaining { get; private set; }

void InitCombat() // Awake 里调用
{
    CurrentHP = Config.maxHP;
    CurrentPosture = 0;
    GourdRemaining = Config.gourdCount;
    ReviveRemaining = Config.reviveCount;
}

public void TakeDamage(int healthDmg, float postureDmg)
{
    CurrentHP -= healthDmg;
    AccumulatePosture(postureDmg);
    CombatEventBus.TriggerTakeDamage(this, healthDmg, CurrentHP);
    if (CurrentHP <= 0) HandleDeath();
}

public void AccumulatePosture(float amount)
{
    CurrentPosture = Mathf.Min(CurrentPosture + amount, Config.maxPosture);
    CombatEventBus.TriggerPostureChanged(this);
    if (CurrentPosture >= Config.maxPosture) OnPostureBroken();
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
- 崩解 = 切 EndureState（M10）
- 架势自然回复写在 CharacterBody.Update 里：距上次受击超过 `postureDecayDelay` 则每秒减 `postureDecayRate`

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

## 六、事件（新增）

```csharp
CombatEventBus:
  static event Action<CharacterBody> OnPostureChanged
  static event Action<CharacterBody> OnPostureBroken
  static event Action<CharacterBody> OnGourdUsed
  static event Action<CharacterBody> OnDeath
  static event Action<CharacterBody> OnReviveAvailable
```

## 涉及文件

- 新建：`Assets/Scripts/Configs/CharacterConfig.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
- 新建：`Assets/Scripts/Mgr/`（事件触发方法）
