using System;

namespace ARPG.Configs
{

    // 危字攻击类型（M17）
    // 危字攻击不可被普通防御/弹反抵挡，玩家必须用对应方式应对：
    //   Thrust     突刺 → 识破 / 弹反窗口 / 躲避（普通格挡等于没防）
    //   Grab       抓取（Elbow 投技）→ 弹反窗口 / 垫步躲避（不可识破；普通格挡等于没防）
    //   Sweep      已删除：未实现跳踩反制，Boss 招式表不再有横扫；枚举值保留防序列化失真
    //   JumpThrust 旧整招危字，已不用：3022 起跳非危，落地只接突刺（见 BossMovePicker）
    [Serializable]
    public enum PerilousType
    {
        None = 0,   // 非危字攻击
        Thrust,     // 突刺（地面刺击，可识破）
        Sweep,      // 下段横扫（已删除，保留枚举值防序列化失真）
        Grab,       // 抓取（可弹反/躲避）
        JumpThrust  // 保留枚举值，招式表不再标这一档
    }

}
