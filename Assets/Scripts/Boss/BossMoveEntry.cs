using System;

// 招式表的一行：选招用距离/权重/冷却/层，出招用动画名和窗口。
[Serializable]
public class BossMoveEntry
{
    public string id;
    public BossMoveLayer layer;
    public BossAnimSequence[] sequences;
    public BossMoveWindow[] windows;
    public int baseDamage = 10;
    public float postureDamage = 15f;
    public float knockback = 0f;
    public PerilousType perilous = PerilousType.None;
    public float minRange = 0f;
    public float maxRange = 99f;
    public float weight = 10f;
    public float cooldown = 4f;
    public BossMoveExtra extra = BossMoveExtra.None;
}
