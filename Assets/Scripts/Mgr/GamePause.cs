using System;
using UnityEngine;

namespace ARPG.Mgr
{

    // 保存全局暂停状态并通知订阅者；时间尺度统一交给 TimeScaleController。
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static event Action<bool> OnChanged;

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;

            IsPaused = paused;
            TimeScaleController.Refresh();
            OnChanged?.Invoke(paused);
            CursorController.Refresh();
        }
    }

}
