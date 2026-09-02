using System;
using UnityEngine;

namespace ARPG.Mgr
{

    // 全局暂停开关。菜单用未缩放时间，战斗靠 timeScale=0 冻结。
    //
    // 这里只负责记录「暂停状态」，不再自己写 Time.timeScale——
    // 顿帧也在动同一个变量，两边各写一次必然互相冲掉（旧代码里顿帧结束时要回读 IsPaused
    // 就是那条裂缝的证据）。现在统一由 TimeScaleController 唯一求值，
    // 暂停优先级高于顿帧，谁先谁后都不会出错。
    public static class GamePause
    {
        public static bool IsPaused { get; private set; }

        public static event Action<bool> OnChanged;

        public static void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;

            IsPaused = paused;
            TimeScaleController.Refresh(); // 申报状态变化，由它决定最终 timeScale
            OnChanged?.Invoke(paused);
            CursorController.Refresh();
        }
    }

}
