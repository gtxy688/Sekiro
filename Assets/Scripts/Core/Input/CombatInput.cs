/// <summary>
/// 战斗输入枚举，按优先级从高到低排列。
/// 数值越小优先级越高，用于输入优先级排序。
/// </summary>
public enum CombatInput
{
    /// <summary>忍杀（最高优先级）</summary>
    Deathblow = 0,

    /// <summary>弹刀/格挡</summary>
    Deflect = 1,

    /// <summary>松开格挡</summary>
    DeflectRelease = 2,

    /// <summary>攻击</summary>
    Attack = 3,

    /// <summary>闪避</summary>
    Dodge = 4,

    /// <summary>识破</summary>
    Mikiri = 5,

    /// <summary>跳跃</summary>
    Jump = 6,

    /// <summary>回血</summary>
    Heal = 7,

    /// <summary>锁定切换</summary>
    LockOn = 8,

    /// <summary>移动（最低优先级）</summary>
    Move = 9
}
