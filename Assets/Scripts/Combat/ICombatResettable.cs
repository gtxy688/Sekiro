namespace ARPG.Combat
{
    // 复战地基：任何持有「一场战斗内的临时状态」的组件都实现它。
    //
    // 为什么需要：当前项目唯一的重置手段是「重载场景」——MonoBehaviour 全部重建，
    // 状态自然归零，所以从没暴露过问题。
    // 复战 / 连战要走「原地重开」，不重载场景，那时所有没显式重置的状态都会残留：
    // 架势、命数、处决锁、AI 冷却、复活阶段机、成对演出的中间态……
    //
    // 约定：ResetForEncounter() 只把本组件恢复到「战斗刚开始的那一瞬间」，
    // 不负责重建 GameObject、不负责重新加载资源。
    public interface ICombatResettable
    {
        void ResetForEncounter();
    }
}
