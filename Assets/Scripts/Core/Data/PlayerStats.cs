using UnityEngine;

/// <summary>
/// 玩家属性 ScriptableObject 配置。
/// 包含基础属性、弹刀参数、连续弹刀加成和回血配置。
/// 创建方式：Unity Editor → Create → Combat → PlayerStats
/// </summary>
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Combat/PlayerStats")]
public class PlayerStats : ScriptableObject
{
    [Header("基础属性")]

    /// <summary>最大生命值</summary>
    public float maxHealth = 1000f;

    /// <summary>最大架势值</summary>
    public float maxPosture = 300f;

    /// <summary>架势恢复速率（每秒恢复百分比）</summary>
    public float postureRecoveryRate = 15f;

    /// <summary>攻击力</summary>
    public float attack = 100f;

    /// <summary>防御力</summary>
    public float defense = 50f;

    /// <summary>移动速度</summary>
    public float moveSpeed = 5f;

    /// <summary>闪避速度</summary>
    public float dodgeSpeed = 10f;

    [Header("弹刀参数")]

    /// <summary>基础弹刀窗口时间（秒），12帧@60fps</summary>
    public float baseDeflectWindow = 0.2f;

    /// <summary>最小弹刀窗口时间（秒），1帧@60fps</summary>
    public float minDeflectWindow = 0.016f;

    /// <summary>连按重置时间（秒）</summary>
    public float spamResetTime = 0.5f;

    /// <summary>每次连按减少的窗口时间（秒）</summary>
    public float windowReductionPerSpam = 0.015f;

    /// <summary>弹刀成功后架势恢复比例</summary>
    public float deflectPostureRecovery = 0.08f;

    [Header("连续弹刀加成")]

    /// <summary>连续弹刀倍率数组，索引对应连击次数</summary>
    public float[] deflectChainMultipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };

    [Header("回血")]

    /// <summary>最大回血次数</summary>
    public int maxHealingCharges = 10;

    /// <summary>每次回血恢复的生命百分比</summary>
    public float healPercent = 0.3f;

    /// <summary>回血动画持续时间（秒）</summary>
    public float healDuration = 0.8f;
}
