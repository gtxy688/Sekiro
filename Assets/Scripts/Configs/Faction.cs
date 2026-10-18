namespace ARPG.Configs
{
    // 角色身份标签，供战斗与表现逻辑统一区分玩家和敌人，避免依赖组件或全局对象引用。
    // 默认值是 Enemy，玩家对象需显式配置为 Player。
    public enum Faction
    {
        Enemy = 0,
        Player = 1
    }

}
