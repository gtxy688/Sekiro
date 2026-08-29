using UnityEngine;

// 胜利结算时关掉玩家操作，不冻 timeScale（和暂停分开）。
public static class CombatInputGate
{
    public static bool Blocked { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Blocked = false;
    }

    public static void SetBlocked(bool blocked)
    {
        Blocked = blocked;
        CursorController.Refresh();
    }
}
