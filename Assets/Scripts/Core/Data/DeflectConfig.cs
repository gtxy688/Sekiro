using UnityEngine;

/// <summary>
/// 弹刀系统全局配置 ScriptableObject。
/// 包含玩家弹刀参数、Boss 弹刀参数和 AI 弹刀概率。
/// 创建方式：Unity Editor → Create → Combat → DeflectConfig
/// </summary>
[CreateAssetMenu(fileName = "DeflectConfig", menuName = "Combat/DeflectConfig")]
public class DeflectConfig : ScriptableObject
{
    [Header("玩家弹刀")]

    /// <summary>基础弹刀窗口时间（秒），12帧@60fps</summary>
    public float baseDeflectWindow = 0.2f;

    /// <summary>最小弹刀窗口时间（秒），1帧@60fps</summary>
    public float minDeflectWindow = 0.016f;

    /// <summary>连按重置时间（秒）</summary>
    public float spamResetTime = 0.5f;

    /// <summary>每次连按减少的窗口时间（秒）</summary>
    public float windowReductionPerSpam = 0.015f;

    [Header("格挡参数")]

    /// <summary>格挡减伤比例（60%）</summary>
    public float blockDamageReduction = 0.6f;

    /// <summary>格挡时架势上升比例（30%）</summary>
    public float blockPostureIncrease = 0.3f;

    /// <summary>弹刀恢复自身架势比例（8%）</summary>
    public float deflectPostureRecovery = 0.08f;

    /// <summary>普通格挡窗口持续时间（秒），完美弹刀窗口结束后延伸</summary>
    public float blockWindowDuration = 0.4f;

    [Header("连续弹刀")]

    /// <summary>连续弹刀架势伤害倍率表，索引 0=第1次，4+=封顶</summary>
    public float[] deflectChainMultipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };

    /// <summary>连续弹刀加成重置时间（秒），超过此时间未弹刀则归零</summary>
    public float deflectChainResetTime = 1.0f;

    [Header("抖刀伤害惩罚")]

    /// <summary>抖刀惩罚下架势伤害最低倍率（20%）</summary>
    public float spamPostureDamageMin = 0.2f;

    [Header("Boss弹刀")]

    /// <summary>Boss 弹刀窗口时间（秒），9帧@60fps</summary>
    public float bossDeflectWindow = 0.15f;

    /// <summary>Boss 格挡窗口时间（秒）</summary>
    public float bossBlockWindow = 0.3f;

    /// <summary>Boss 架势持续时间（秒）</summary>
    public float bossStanceDuration = 0.4f;

    [Header("AI弹刀概率")]

    /// <summary>AI 在攻击时弹刀的概率</summary>
    public float deflectChanceOnAttack = 0.4f;

    /// <summary>AI 在连招中弹刀的概率</summary>
    public float deflectChanceInCombo = 0.6f;

    /// <summary>AI 低血量时弹刀的概率</summary>
    public float deflectChanceLowHealth = 0.25f;

    /// <summary>二阶段额外弹刀概率加成</summary>
    public float phase2Bonus = 0.1f;
}
