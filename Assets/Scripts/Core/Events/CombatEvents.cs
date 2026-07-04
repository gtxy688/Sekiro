using System;

namespace Sekiro.Core.Events
{
    /// <summary>
    /// 战斗事件总线，模块间解耦通信的核心。
    /// Player 和 Boss 模块通过此静态类进行事件通信，禁止直接引用其他模块。
    /// </summary>
    public static partial class CombatEvents
    {
        #region Event Declarations

        /// <summary>
        /// 玩家受到伤害时触发。参数：伤害值
        /// </summary>
        public static event Action<float> OnPlayerDamaged;

        /// <summary>
        /// Boss 受到伤害时触发。参数：伤害值
        /// </summary>
        public static event Action<float> OnBossDamaged;

        /// <summary>
        /// 玩家架势条变化时触发。参数：当前架势值、最大架势值
        /// </summary>
        public static event Action<float, float> OnPlayerPostureChanged;

        /// <summary>
        /// Boss 架势条变化时触发。参数：当前架势值、最大架势值
        /// </summary>
        public static event Action<float, float> OnBossPostureChanged;

        /// <summary>
        /// 完美弹刀时触发
        /// </summary>
        public static event Action OnPerfectDeflect;

        /// <summary>
        /// 普通格挡时触发
        /// </summary>
        public static event Action OnNormalBlock;

        /// <summary>
        /// 识破（踩头）成功时触发
        /// </summary>
        public static event Action OnMikiriCounter;

        /// <summary>
        /// Boss 架势崩溃时触发（可处决）
        /// </summary>
        public static event Action OnBossPostureBreak;

        /// <summary>
        /// 玩家架势崩溃时触发
        /// </summary>
        public static event Action OnPlayerPostureBreak;

        /// <summary>
        /// 忍杀处决时触发
        /// </summary>
        public static event Action OnDeathblow;

        /// <summary>
        /// 雷电反击成功时触发
        /// </summary>
        public static event Action OnLightningCounterSuccess;

        /// <summary>
        /// 雷电反击失败时触发
        /// </summary>
        public static event Action OnLightningCounterFail;

        /// <summary>
        /// 治疗次数变化时触发。参数：剩余次数
        /// </summary>
        public static event Action<int> OnHealingChargeChanged;

        /// <summary>
        /// 战斗开始时触发
        /// </summary>
        public static event Action OnCombatStart;

        /// <summary>
        /// 战斗结束时触发
        /// </summary>
        public static event Action OnCombatEnd;

        #endregion

        #region Raise Methods

        /// <summary>
        /// 触发玩家受伤事件
        /// </summary>
        /// <param name="damage">伤害值</param>
        public static void RaisePlayerDamaged(float damage) => OnPlayerDamaged?.Invoke(damage);

        /// <summary>
        /// 触发 Boss 受伤事件
        /// </summary>
        /// <param name="damage">伤害值</param>
        public static void RaiseBossDamaged(float damage) => OnBossDamaged?.Invoke(damage);

        /// <summary>
        /// 触发玩家架势变化事件
        /// </summary>
        /// <param name="current">当前架势值</param>
        /// <param name="max">最大架势值</param>
        public static void RaisePlayerPostureChanged(float current, float max) => OnPlayerPostureChanged?.Invoke(current, max);

        /// <summary>
        /// 触发 Boss 架势变化事件
        /// </summary>
        /// <param name="current">当前架势值</param>
        /// <param name="max">最大架势值</param>
        public static void RaiseBossPostureChanged(float current, float max) => OnBossPostureChanged?.Invoke(current, max);

        /// <summary>
        /// 触发完美弹刀事件
        /// </summary>
        public static void RaisePerfectDeflect() => OnPerfectDeflect?.Invoke();

        /// <summary>
        /// 触发普通格挡事件
        /// </summary>
        public static void RaiseNormalBlock() => OnNormalBlock?.Invoke();

        /// <summary>
        /// 触发识破成功事件
        /// </summary>
        public static void RaiseMikiriCounter() => OnMikiriCounter?.Invoke();

        /// <summary>
        /// 触发 Boss 架势崩溃事件
        /// </summary>
        public static void RaiseBossPostureBreak() => OnBossPostureBreak?.Invoke();

        /// <summary>
        /// 触发玩家架势崩溃事件
        /// </summary>
        public static void RaisePlayerPostureBreak() => OnPlayerPostureBreak?.Invoke();

        /// <summary>
        /// 触发忍杀处决事件
        /// </summary>
        public static void RaiseDeathblow() => OnDeathblow?.Invoke();

        /// <summary>
        /// 触发雷电反击成功事件
        /// </summary>
        public static void RaiseLightningCounterSuccess() => OnLightningCounterSuccess?.Invoke();

        /// <summary>
        /// 触发雷电反击失败事件
        /// </summary>
        public static void RaiseLightningCounterFail() => OnLightningCounterFail?.Invoke();

        /// <summary>
        /// 触发治疗次数变化事件
        /// </summary>
        /// <param name="remaining">剩余次数</param>
        public static void RaiseHealingChargeChanged(int remaining) => OnHealingChargeChanged?.Invoke(remaining);

        /// <summary>
        /// 触发战斗开始事件
        /// </summary>
        public static void RaiseCombatStart() => OnCombatStart?.Invoke();

        /// <summary>
        /// 触发战斗结束事件
        /// </summary>
        public static void RaiseCombatEnd() => OnCombatEnd?.Invoke();

        #endregion
    }
}
