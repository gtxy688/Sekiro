using UnityEngine;

/// <summary>
/// 战斗系统全局参数 ScriptableObject 配置。
/// 包含帧冻结、架势恢复和输入缓冲等全局参数。
/// 创建方式：Unity Editor → Create → Combat → CombatConfig
/// </summary>
[CreateAssetMenu(fileName = "CombatConfig", menuName = "Combat/CombatConfig")]
public class CombatConfig : ScriptableObject
{
    [Header("帧冻结")]

    /// <summary>普通攻击命中时的帧冻结时间（秒），约2帧</summary>
    public float normalHitStop = 0.033f;

    /// <summary>弹刀成功时的帧冻结时间（秒），约3帧</summary>
    public float deflectHitStop = 0.05f;

    /// <summary>识破成功时的帧冻结时间（秒），约4帧</summary>
    public float mikiriHitStop = 0.066f;

    /// <summary>雷电反击成功时的帧冻结时间（秒），约6帧</summary>
    public float lightningHitStop = 0.1f;

    [Header("架势")]

    /// <summary>脱战后架势开始恢复的延迟时间（秒）</summary>
    public float postureRecoveryDelay = 2f;

    /// <summary>架势每秒恢复百分比</summary>
    public float postureRecoveryRate = 15f;

    [Header("输入缓冲")]

    /// <summary>输入缓冲窗口时间（秒），150ms</summary>
    public float inputBufferWindow = 0.15f;
}
