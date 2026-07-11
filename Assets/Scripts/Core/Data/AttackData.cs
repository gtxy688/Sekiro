using UnityEngine;

/// <summary>
/// 攻击动作数据，定义单个招式的完整参数。
/// 可复用于玩家和 Boss 的攻击配置。
/// </summary>
[System.Serializable]
public class AttackData
{
    /// <summary>招式名称（调试用）</summary>
    public string attackName;

    /// <summary>动画状态机中的动画名称</summary>
    public string animName;

    /// <summary>伤害值</summary>
    public float damage;

    /// <summary>对敌方架势伤害</summary>
    public float postureDamage;

    /// <summary>攻击类型：普通/突刺/扫击/投技/雷电</summary>
    public AttackType attackType;

    /// <summary>是否可被弹刀</summary>
    public bool canBeDeflected;

    [Header("帧数据")]

    /// <summary>前摇帧数</summary>
    public float startupFrames;

    /// <summary>判定帧数</summary>
    public float activeFrames;

    /// <summary>后摇帧数</summary>
    public float recoveryFrames;

    /// <summary>弹刀窗口帧数</summary>
    public float deflectWindowFrames;

    [Header("判定")]

    /// <summary>攻击判定范围半径</summary>
    public float hitboxRadius;

    /// <summary>攻击判定相对于角色中心的偏移</summary>
    public Vector3 hitboxOffset;

    [Header("特效")]

    /// <summary>攻击特效资源名称</summary>
    public string vfxName;

    /// <summary>攻击音效资源名称</summary>
    public string sfxName;

    /// <summary>是否远程攻击</summary>
    public bool isRanged;
}
