using System;
using UnityEngine;

// 全局暂停开关。菜单用未缩放时间，战斗靠 timeScale=0 冻结。
// 顿帧也会改 timeScale，结束时必须看这里，避免把暂停冲掉。
public static class GamePause
{
    public static bool IsPaused { get; private set; }

    public static event Action<bool> OnChanged;

    public static void SetPaused(bool paused)
    {
        if (IsPaused == paused) return;

        IsPaused = paused;
        Time.timeScale = paused ? 0f : 1f;
        OnChanged?.Invoke(paused);
    }
}
