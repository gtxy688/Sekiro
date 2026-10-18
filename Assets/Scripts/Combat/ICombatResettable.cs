namespace ARPG.Combat
{
    // 供 EncounterScope 统一复位本场临时状态；各实现应可重复调用，且不依赖其他组件的复位顺序。
    // 通常恢复自身初始值，运行时生成的箭矢等对象则自行销毁。
    public interface ICombatResettable
    {
        void ResetForEncounter();
    }
}
