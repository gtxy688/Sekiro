using System;

namespace Sekiro.Core.Events
{
    /// <summary>
    /// 战斗事件总线 - 订阅/取消订阅方法
    /// </summary>
    public static partial class CombatEvents
    {
        #region Subscribe Methods

        /// <summary>
        /// 订阅玩家受伤事件
        /// </summary>
        /// <param name="callback">回调函数，参数为伤害值</param>
        public static void SubscribeOnPlayerDamaged(Action<float> callback) => OnPlayerDamaged += callback;

        /// <summary>
        /// 订阅 Boss 受伤事件
        /// </summary>
        /// <param name="callback">回调函数，参数为伤害值</param>
        public static void SubscribeOnBossDamaged(Action<float> callback) => OnBossDamaged += callback;

        /// <summary>
        /// 订阅玩家架势变化事件
        /// </summary>
        /// <param name="callback">回调函数，参数为当前架势值和最大架势值</param>
        public static void SubscribeOnPlayerPostureChanged(Action<float, float> callback) => OnPlayerPostureChanged += callback;

        /// <summary>
        /// 订阅 Boss 架势变化事件
        /// </summary>
        /// <param name="callback">回调函数，参数为当前架势值和最大架势值</param>
        public static void SubscribeOnBossPostureChanged(Action<float, float> callback) => OnBossPostureChanged += callback;

        /// <summary>
        /// 订阅完美弹刀事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnPerfectDeflect(Action callback) => OnPerfectDeflect += callback;

        /// <summary>
        /// 订阅普通格挡事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnNormalBlock(Action callback) => OnNormalBlock += callback;

        /// <summary>
        /// 订阅识破成功事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnMikiriCounter(Action callback) => OnMikiriCounter += callback;

        /// <summary>
        /// 订阅 Boss 架势崩溃事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnBossPostureBreak(Action callback) => OnBossPostureBreak += callback;

        /// <summary>
        /// 订阅玩家架势崩溃事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnPlayerPostureBreak(Action callback) => OnPlayerPostureBreak += callback;

        /// <summary>
        /// 订阅忍杀处决事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnDeathblow(Action callback) => OnDeathblow += callback;

        /// <summary>
        /// 订阅雷电反击成功事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnLightningCounterSuccess(Action callback) => OnLightningCounterSuccess += callback;

        /// <summary>
        /// 订阅雷电反击失败事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnLightningCounterFail(Action callback) => OnLightningCounterFail += callback;

        /// <summary>
        /// 订阅治疗次数变化事件
        /// </summary>
        /// <param name="callback">回调函数，参数为剩余次数</param>
        public static void SubscribeOnHealingChargeChanged(Action<int> callback) => OnHealingChargeChanged += callback;

        /// <summary>
        /// 订阅战斗开始事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnCombatStart(Action callback) => OnCombatStart += callback;

        /// <summary>
        /// 订阅战斗结束事件
        /// </summary>
        /// <param name="callback">回调函数</param>
        public static void SubscribeOnCombatEnd(Action callback) => OnCombatEnd += callback;

        #endregion

        #region Unsubscribe Methods

        /// <summary>
        /// 取消订阅玩家受伤事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnPlayerDamaged(Action<float> callback) => OnPlayerDamaged -= callback;

        /// <summary>
        /// 取消订阅 Boss 受伤事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnBossDamaged(Action<float> callback) => OnBossDamaged -= callback;

        /// <summary>
        /// 取消订阅玩家架势变化事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnPlayerPostureChanged(Action<float, float> callback) => OnPlayerPostureChanged -= callback;

        /// <summary>
        /// 取消订阅 Boss 架势变化事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnBossPostureChanged(Action<float, float> callback) => OnBossPostureChanged -= callback;

        /// <summary>
        /// 取消订阅完美弹刀事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnPerfectDeflect(Action callback) => OnPerfectDeflect -= callback;

        /// <summary>
        /// 取消订阅普通格挡事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnNormalBlock(Action callback) => OnNormalBlock -= callback;

        /// <summary>
        /// 取消订阅识破成功事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnMikiriCounter(Action callback) => OnMikiriCounter -= callback;

        /// <summary>
        /// 取消订阅 Boss 架势崩溃事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnBossPostureBreak(Action callback) => OnBossPostureBreak -= callback;

        /// <summary>
        /// 取消订阅玩家架势崩溃事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnPlayerPostureBreak(Action callback) => OnPlayerPostureBreak -= callback;

        /// <summary>
        /// 取消订阅忍杀处决事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnDeathblow(Action callback) => OnDeathblow -= callback;

        /// <summary>
        /// 取消订阅雷电反击成功事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnLightningCounterSuccess(Action callback) => OnLightningCounterSuccess -= callback;

        /// <summary>
        /// 取消订阅雷电反击失败事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnLightningCounterFail(Action callback) => OnLightningCounterFail -= callback;

        /// <summary>
        /// 取消订阅治疗次数变化事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnHealingChargeChanged(Action<int> callback) => OnHealingChargeChanged -= callback;

        /// <summary>
        /// 取消订阅战斗开始事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnCombatStart(Action callback) => OnCombatStart -= callback;

        /// <summary>
        /// 取消订阅战斗结束事件
        /// </summary>
        /// <param name="callback">要移除的回调函数</param>
        public static void UnsubscribeOnCombatEnd(Action callback) => OnCombatEnd -= callback;

        #endregion

        #region Clear Methods

        /// <summary>
        /// 清除所有事件订阅。用于测试隔离或场景切换时重置事件系统。
        /// </summary>
        public static void ClearAll()
        {
            OnPlayerDamaged = null;
            OnBossDamaged = null;
            OnPlayerPostureChanged = null;
            OnBossPostureChanged = null;
            OnPerfectDeflect = null;
            OnNormalBlock = null;
            OnMikiriCounter = null;
            OnBossPostureBreak = null;
            OnPlayerPostureBreak = null;
            OnDeathblow = null;
            OnLightningCounterSuccess = null;
            OnLightningCounterFail = null;
            OnHealingChargeChanged = null;
            OnCombatStart = null;
            OnCombatEnd = null;
        }

        #endregion
    }
}
