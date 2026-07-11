/// <summary>
/// 攻击类型枚举，对应只狼中的五种危字/普通攻击类型。
/// </summary>
public enum AttackType
{
    /// <summary>普通攻击（可弹刀）</summary>
    Normal,

    /// <summary>突刺（需识破/踩头）</summary>
    Thrust,

    /// <summary>扫击（需跳跃躲避）</summary>
    Sweep,

    /// <summary>投技（需闪避，不可弹刀）</summary>
    Grab,

    /// <summary>雷电攻击（需雷电反）</summary>
    Lightning,
}
