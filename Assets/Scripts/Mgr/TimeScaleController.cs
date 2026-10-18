using System.Collections;
using UnityEngine;

namespace ARPG.Mgr
{
    // 统一写入 Time.timeScale，按“暂停 > 顿帧 > 正常”求值，避免多个系统互相覆盖。
    public static class TimeScaleController
    {
        private const float DefaultHitStopScale = 0.05f;
        private const float DefaultHitStopDuration = 0.05f;

        // 顿帧参数由 CombatManager 在 Start 时从 Inspector 同步。
        public static bool EnableHitStop { get; set; } = true;
        public static float HitStopDuration { get; set; } = DefaultHitStopDuration;
        public static float HitStopScale { get; set; } = DefaultHitStopScale;

        public static bool IsHitStopping { get; private set; }

        private static Host host;
        private static Coroutine routine;

        // 暂停优先，顿帧结束后也必须重新求值，不能直接恢复为 1。
        private static void Apply()
        {
            Time.timeScale = GamePause.IsPaused ? 0f
                : IsHitStopping ? HitStopScale
                : 1f;
        }

        // 暂停状态变化时重新计算时间尺度。
        public static void Refresh() => Apply();

        // 请求一次顿帧；重复请求重启计时，不叠加时长。
        public static void HitStop(float duration = -1f)
        {
            if (!EnableHitStop || GamePause.IsPaused) return;
            if (duration < 0f) duration = HitStopDuration;

            Host runner = EnsureHost();
            if (routine != null) runner.StopCoroutine(routine);
            routine = runner.StartCoroutine(HitStopRoutine(duration));
        }

        // 复战或切场景时取消旧顿帧，避免延迟回调影响新一局。
        public static void CancelHitStop()
        {
            if (routine != null && host != null)
            {
                host.StopCoroutine(routine);
                routine = null;
            }
            IsHitStopping = false;
            Apply();
        }

        private static IEnumerator HitStopRoutine(float duration)
        {
            IsHitStopping = true;
            Apply();

            // 用真实时间计时，避免顿帧降低时间尺度后反过来延长自身。
            yield return new WaitForSecondsRealtime(duration);

            IsHitStopping = false;
            routine = null;
            Apply();
        }

        // 静态服务通过常驻 MonoBehaviour 执行协程。
        private class Host : MonoBehaviour { }

        private static Host EnsureHost()
        {
            if (host != null) return host;

            // 宿主跨场景保留，生命周期交给 Unity，不使用 HideAndDontSave。
            GameObject go = new GameObject("TimeScaleController");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
            return host;
        }

        // 进入运行时重置静态状态，兼容关闭 Domain Reload 的编辑器设置。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            host = null;
            routine = null;
            IsHitStopping = false;
            EnableHitStop = true;
            HitStopDuration = DefaultHitStopDuration;
            HitStopScale = DefaultHitStopScale;
        }
    }
}
