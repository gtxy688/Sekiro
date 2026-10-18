using System;

namespace ARPG.Configs
{

    // 危字类型决定防御与反制规则；保留未使用的枚举值以兼容已有序列化数据。
    [Serializable]
    public enum PerilousType
    {
        None = 0,   // 非危字攻击
        Thrust,     // 突刺（地面刺击，可识破）
        Sweep,      // 当前招式表未使用。
        Grab,       // 抓取（可弹反/躲避）
        JumpThrust  // 保留枚举值，招式表不再标这一档
    }

}
