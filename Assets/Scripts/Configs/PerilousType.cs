using System;

// 危字攻击类型（M17）
// 危字攻击不可被普通防御/弹反抵挡，玩家必须用对应方式应对：
//   Thrust 突刺 → 识破 (MikiriCounter)
//   Sweep  横扫 → 起跳
//   Grab   抓取 → 闪避
[Serializable]
public enum PerilousType
{
    None = 0, // 非危字攻击
    Thrust,   // 突刺
    Sweep,    // 下段横扫
    Grab      // 抓取
}
