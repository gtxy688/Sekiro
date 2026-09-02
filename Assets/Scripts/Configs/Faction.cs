namespace ARPG.Configs
{
    // 角色阵营。判断「这是谁」一律查这个字段，不要拿对象去跟某个引用比。
    //
    // 为什么需要：此前项目里有三份 IsPlayer，而且用了两套判定标准——
    //   - HitReactionUtil：body == CombatManager.Instance.PlayerRef（比对象引用）
    //   - AudioManager / FXManager：GetComponent<PlayerBrain>() != null（查组件）
    // 比引用的那套意味着：除 PlayerRef 指向的那一个对象外，
    // 世界上没有任何东西能是「玩家」。加第二个 Boss、加友方 NPC、复战换角色，它一律判错。
    // 两套标准还可能给出矛盾答案（PlayerRef 没拖、但角色上有 PlayerBrain）。
    //
    // 为什么是枚举而不是 ScriptableObject：
    // 阵营是类型标签，不是可调数值；新增阵营必然伴随新逻辑，是代码级变更，
    // 不是策划该在 Inspector 里配的东西。用 SO 反而会引入
    // 「两个不同实例代表同一阵营」的隐患，判断时又得比引用——绕回原问题。
    //
    // 默认 Enemy：新增字段时旧 prefab / 旧场景对象会落到这个值，
    // 玩家那个对象需要手工改成 Player（CombatManager 启动时若发现玩家不是 Player 会报错提醒）。
    public enum Faction
    {
        Enemy = 0,
        Player = 1
    }

}
