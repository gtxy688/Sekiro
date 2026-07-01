# ScriptableObject 数据层设计

## 设计意图

所有战斗参数通过 ScriptableObject 配置，实现数据驱动。禁止在代码中硬编码数值。

## 数据资产类型

### PlayerStats

玩家属性配置，每个玩家实例引用一份。

```csharp
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Combat/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("基础属性")]
    public float maxHealth = 1000f;
    public float maxPosture = 300f;
    public float postureRecoveryRate = 15f;      // 每秒恢复15%
    public float attack = 100f;
    public float defense = 50f;
    public float moveSpeed = 5f;
    public float dodgeSpeed = 10f;
    
    [Header("弹刀参数")]
    public float baseDeflectWindow = 0.2f;        // 12帧@60fps
    public float minDeflectWindow = 0.016f;       // 1帧@60fps
    public float spamResetTime = 0.5f;
    public float windowReductionPerSpam = 0.015f;
    public float deflectPostureRecovery = 0.08f;  // 弹刀恢复8%架势
    
    [Header("连续弹刀加成")]
    public float[] deflectChainMultipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };
    
    [Header("回血")]
    public int maxHealingCharges = 10;
    public float healPercent = 0.3f;
    public float healDuration = 0.8f;
}
```

### BossStats

Boss 属性配置，支持两阶段。

```csharp
[CreateAssetMenu(fileName = "BossStats", menuName = "Combat/BossStats")]
public class BossStats : ScriptableObject
{
    [Header("一阶段")]
    public float phase1MaxHealth = 1000f;
    public float phase1MaxPosture = 300f;
    public float phase1Attack = 100f;
    public AttackData[] phase1Attacks;
    
    [Header("二阶段")]
    public float phase2MaxHealth = 1200f;
    public float phase2MaxPosture = 400f;
    public float phase2Attack = 120f;
    public AttackData[] phase2Attacks;
    public float attackSpeedMultiplier = 1.2f;    // 攻速+20%
    public float startupReduction = 0.85f;        // 前摇-15%
    
    [Header("阶段转换")]
    public float phaseTransitionHealthPercent = 0.5f;
}
```

### AttackData

攻击动作数据，可复用。

```csharp
[System.Serializable]
public class AttackData
{
    public string attackName;         // 招式名称（调试用）
    public string animName;           // 动画名称
    public float damage;              // 伤害值
    public float postureDamage;       // 对敌方架势伤害
    public AttackType attackType;     // 普通/突刺/扫击/投技/雷电
    public bool canBeDeflected;       // 是否可弹刀
    
    [Header("帧数据")]
    public float startupFrames;       // 前摇帧数
    public float activeFrames;        // 判定帧数
    public float recoveryFrames;      // 后摇帧数
    public float deflectWindowFrames; // 弹刀窗口帧数
    
    [Header("判定")]
    public float hitboxRadius;        // 攻击判定范围
    public Vector3 hitboxOffset;      // 攻击判定偏移
    
    [Header("特效")]
    public string vfxName;            // 攻击特效
    public string sfxName;            // 攻击音效
    public bool isRanged;             // 是否远程
}

public enum AttackType
{
    Normal,       // 普通（可弹刀）
    Thrust,       // 突刺（需识破）
    Sweep,        // 扫击（需跳跃）
    Grab,         // 投技（需闪避）
    Lightning     // 雷电（需雷电反击）
}
```

### DeflectConfig

弹刀系统全局配置。

```csharp
[CreateAssetMenu(fileName = "DeflectConfig", menuName = "Combat/DeflectConfig")]
public class DeflectConfig : ScriptableObject
{
    [Header("玩家弹刀")]
    public float baseDeflectWindow = 0.2f;        // 12帧@60fps
    public float minDeflectWindow = 0.016f;       // 1帧@60fps
    public float spamResetTime = 0.5f;
    public float windowReductionPerSpam = 0.015f;
    
    [Header("Boss弹刀")]
    public float bossDeflectWindow = 0.15f;       // 9帧@60fps
    public float bossBlockWindow = 0.3f;
    public float bossStanceDuration = 0.4f;
    
    [Header("AI弹刀概率")]
    public float deflectChanceOnAttack = 0.4f;
    public float deflectChanceInCombo = 0.6f;
    public float deflectChanceLowHealth = 0.25f;
    public float phase2Bonus = 0.1f;
}
```

### CombatConfig

战斗系统全局参数。

```csharp
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Combat/CombatConfig")]
public class CombatConfig : ScriptableObject
{
    [Header("帧冻结")]
    public float normalHitStop = 0.033f;          // 2帧
    public float deflectHitStop = 0.05f;          // 3帧
    public float mikiriHitStop = 0.066f;          // 4帧
    public float lightningHitStop = 0.1f;         // 6帧
    
    [Header("架势")]
    public float postureRecoveryDelay = 2f;       // 脱战2秒后开始恢复
    public float postureRecoveryRate = 15f;       // 每秒15%
    
    [Header("输入缓冲")]
    public float inputBufferWindow = 0.15f;       // 150ms
}
```

## 使用规范

### 创建数据资产

1. Unity Editor → 右键 → Create → Combat → [类型]
2. 命名：`PlayerStats_Default.asset`、`BossStats_Genichiro.asset`
3. 存放在 `Assets/ScriptableObjects/` 对应子目录

### 在代码中引用

```csharp
public class PlayerController : MonoBehaviour
{
    [SerializeField] private PlayerStats _stats;
    
    public void TakeDamage(float damage)
    {
        float actualDamage = damage - _stats.defense;
        // ...
    }
}
```

### 禁止行为

| 禁止 | 原因 |
|------|------|
| `float damage = 100f;` | 应该从 `AttackData.damage` 读取 |
| `float window = 0.2f;` | 应该从 `DeflectConfig.baseDeflectWindow` 读取 |
| 在运行时修改 ScriptableObject | 使用运行时数据结构（如 `PostureSystem`） |

## 数据资产清单

| 资产 | 路径 | 说明 |
|------|------|------|
| PlayerStats_Default | `ScriptableObjects/Player/` | 玩家默认属性 |
| BossStats_Genichiro | `ScriptableObjects/Boss/` | 弦一郎属性 |
| DeflectConfig_Default | `ScriptableObjects/Combat/` | 弹刀默认配置 |
| CombatConfig_Default | `ScriptableObjects/Combat/` | 战斗全局配置 |
| AttackData_* | `ScriptableObjects/Boss/Attacks/` | 弦一郎各招式数据 |
