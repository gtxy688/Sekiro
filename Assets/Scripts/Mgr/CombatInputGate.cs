using UnityEngine;

namespace ARPG.Mgr
{

    // 结算时阻止玩家操作，保留世界时间让演出继续播放。
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

}
