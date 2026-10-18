namespace ARPG.Boss
{
    // 选招时的额外条件，由 BossMovePicker 按角色与对手当前状态判断。
    public enum BossMoveExtra
    {
        None = 0,
        HpBelow75 = 1,
        PostureLow = 2,         // 历史命名：累计架势达到表中阈值。
        ConsecutiveParry2 = 3,  // 连续被对手完美弹开至少两次。
        PlayerKnockedDown = 4  // 对手已进入躺地阶段。
    }

}
