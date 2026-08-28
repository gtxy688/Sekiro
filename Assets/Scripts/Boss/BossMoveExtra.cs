// 额外抽招条件。PostureLow = 架势累计已过阈值（偏高/濒崩）时放行，阈值在表上，<=0 时用 MaxPosture 的一半。
// ConsecutiveParry2 = 玩家连续完美弹开本角色 ≥2 次才放行（3022 JumpThrust；≠ 交锋 3062）。
public enum BossMoveExtra
{
    None = 0,
    HpBelow75 = 1,
    PostureLow = 2,
    ConsecutiveParry2 = 3
}
