using System;

// 危字攻击类型（M17）
// 危字攻击不可被普通防御/弹反抵挡，玩家必须用对应方式应对：
//   Thrust     突刺 → 识破 (MikiriCounter) / 弹反 / 躲避
//   Sweep      横扫 → 起跳踩头 / 垫步躲避（不可防御、不可识破）
//   Grab       抓取 → 弹反 / 垫步躲避（不可识破）
//   JumpThrust 跳跃突刺 → 弹刀 / 垫步躲避（不可识破——与地面突刺区分）
[Serializable]
public enum PerilousType
{
    None = 0,   // 非危字攻击
    Thrust,     // 突刺（地面刺击，可识破）
    Sweep,      // 下段横扫
    Grab,       // 抓取（可弹反/躲避）
    JumpThrust  // 跳跃突刺（跳起下劈，只可弹刀/躲避）
}
