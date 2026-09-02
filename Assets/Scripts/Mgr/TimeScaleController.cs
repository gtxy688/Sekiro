using System.Collections;
using UnityEngine;

namespace ARPG.Mgr
{
    // 全局时间尺度的唯一写入者。
    //
    // 为什么需要这一层：顿帧（战斗表现）和暂停（系统菜单）都想改 Time.timeScale。
    // 两个系统各自回写同一个全局变量，就必然互相冲掉。旧代码里 CombatManager 结束顿帧时
    // 要写 `Time.timeScale = GamePause.IsPaused ? 0f : 1f`，这一行就是在承认
    // 「我不知道该写什么，得先去问另一个系统」——那不是防御，那是裂缝。
    // 真正的竞态比这更隐蔽：顿帧途中打开暂停、暂停期间顿帧到期，两条路径谁后写谁赢。
    //
    // 正解是单一写入者：谁都不许直接写 timeScale，都只向这里申报自己的状态，
    // 由唯一的求值函数 Apply() 按优先级算出结果。上面那两种情况因此自动正确，
    // 而且任何一方都不需要知道另一方存在。
    //
    // 优先级：暂停 > 顿帧 > 正常。暂停是系统级（玩家主动要求世界停下来），
    // 顿帧是表现级（几十毫秒的打击重量感），系统级压过表现级。
    public static class TimeScaleController
    {
        private const float DefaultHitStopScale = 0.05f;
        private const float DefaultHitStopDuration = 0.05f;

        // 顿帧参数。由 CombatManager 在 Start 时从 Inspector 同步过来（过渡期配置入口在那边）。
        public static bool EnableHitStop { get; set; } = true;
        public static float HitStopDuration { get; set; } = DefaultHitStopDuration;
        public static float HitStopScale { get; set; } = DefaultHitStopScale;

        public static bool IsHitStopping { get; private set; }

        private static Host host;
        private static Coroutine routine;

        // ===== 唯一的写入点。全项目只有这一行改 Time.timeScale。=====
        private static void Apply()
        {
            Time.timeScale = GamePause.IsPaused ? 0f
                : IsHitStopping ? HitStopScale
                : 1f;
        }

        // 供 GamePause 在暂停状态变化后调用。注意这是「申报」，不是「写入」——
        // 调用方不知道也不关心最终 timeScale 是多少。
        public static void Refresh() => Apply();

        // ===== 顿帧 =====

        // 请求一次顿帧。重复调用会重启计时（连续命中时不叠加、不叠乘）。
        public static void HitStop(float duration = -1f)
        {
            if (!EnableHitStop || GamePause.IsPaused) return;
            if (duration < 0f) duration = HitStopDuration;

            Host runner = EnsureHost();
            if (routine != null) runner.StopCoroutine(routine);
            routine = runner.StartCoroutine(HitStopRoutine(duration));
        }

        // 强制取消顿帧。复战 / 切场景时用：残留的顿帧会在新一局开场把时间拨回去。
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
            // 与旧实现一致：协程真正起跑时若已被暂停，放弃这次顿帧。
            // 单次调用内几乎不可能发生（HitStop 入口已查过一次），
            // 留着是为了将来有人改动调用顺序时不至于把暂停冲掉。
            if (GamePause.IsPaused)
            {
                IsHitStopping = false;
                routine = null;
                Apply();
                yield break;
            }

            IsHitStopping = true;
            Apply();

            // 用未缩放时间：顿帧本身把 timeScale 压到 0.05，
            // 若用 WaitForSeconds 会按缩放后计时，50ms 变成 1 秒。
            yield return new WaitForSecondsRealtime(duration);

            IsHitStopping = false;
            routine = null;
            Apply(); // 唯一求值，自动带上「这期间有没有被暂停」
        }

        // ===== 隐藏宿主：静态类跑不了协程，需要一个 MonoBehaviour 当 runner =====

        private class Host : MonoBehaviour { }

        private static Host EnsureHost()
        {
            if (host != null) return host;

            // 刻意不设 HideFlags：设了 HideAndDontSave 反而不会被清理，
            // 反复进出 Play 模式会在 DontDestroyOnLoad 里堆一堆积尸。
            // 不设则由 Unity 随 DontDestroyOnLoad 场景一起回收；
            // 顺带的好处是 Play 时能在 Hierarchy 里看见它，便于确认宿主确实建起来了。
            GameObject go = new GameObject("TimeScaleController");
            UnityEngine.Object.DontDestroyOnLoad(go);
            host = go.AddComponent<Host>();
            return host;
        }

        // 域重载（改脚本后回到 Unity）会保留静态字段但清掉所有 Unity 对象，
        // 宿主必须一起重建，否则协程跑在一个已死的 MonoBehaviour 上。
        // 与 CombatInputGate / CursorController 的做法一致。
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
