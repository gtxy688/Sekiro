// 额外抽招条件。PostureLow = 架势累计已过阈值（偏高/濒崩）时放行，阈值在表上，<=0 时用 MaxPosture 的一半。
// ConsecutiveParry2 = 玩家连续完美弹开本角色 ≥2 次才放行（3022 JumpThrust；≠ 交锋 3062）。
// PlayerKnockedDown = 玩家 Mid/Heavy 已过倒地过程、处于躺地（109031 类；Jump_Danger）。
public enum BossMoveExtra
{
    None = 0,
    HpBelow75 = 1,
    PostureLow = 2,
    ConsecutiveParry2 = 3,
    PlayerKnockedDown = 4
}
