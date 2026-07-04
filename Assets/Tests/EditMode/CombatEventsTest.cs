using NUnit.Framework;
using Sekiro.Core.Events;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// CombatEvents 事件总线 EditMode 单元测试。
    /// 覆盖：订阅/取消订阅、多订阅者、参数传递、ClearAll 清除。
    /// </summary>
    [TestFixture]
    public class CombatEventsTest
    {
        #region Setup & Teardown

        /// <summary>
        /// 每个测试前清除所有事件订阅，确保测试隔离
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            CombatEvents.ClearAll();
        }

        /// <summary>
        /// 每个测试后清除所有事件订阅
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearAll();
        }

        #endregion

        #region Tests

        /// <summary>
        /// 测试用例 1：订阅事件后，触发事件时回调被调用
        /// </summary>
        [Test]
        public void Subscribe_EventTriggered_CallbackInvoked()
        {
            // Arrange
            bool callbackInvoked = false;
            CombatEvents.SubscribeOnPerfectDeflect(() => callbackInvoked = true);

            // Act
            CombatEvents.RaisePerfectDeflect();

            // Assert
            Assert.IsTrue(callbackInvoked, "Callback should be invoked when event is raised");
        }

        /// <summary>
        /// 测试用例 2：取消订阅后，触发事件时回调不被调用
        /// </summary>
        [Test]
        public void Unsubscribe_EventTriggered_CallbackNotInvoked()
        {
            // Arrange
            int invokeCount = 0;
            System.Action callback = () => invokeCount++;
            CombatEvents.SubscribeOnPerfectDeflect(callback);

            // Act - 第一次触发，应该被调用
            CombatEvents.RaisePerfectDeflect();
            Assert.AreEqual(1, invokeCount, "Callback should be invoked once");

            // 取消订阅
            CombatEvents.UnsubscribeOnPerfectDeflect(callback);

            // Act - 第二次触发，不应该被调用
            CombatEvents.RaisePerfectDeflect();

            // Assert
            Assert.AreEqual(1, invokeCount, "Callback should not be invoked after unsubscribe");
        }

        /// <summary>
        /// 测试用例 3：多个订阅者都能收到事件
        /// </summary>
        [Test]
        public void MultipleSubscribers_AllReceiveEvent()
        {
            // Arrange
            int subscriber1Count = 0;
            int subscriber2Count = 0;
            int subscriber3Count = 0;

            CombatEvents.SubscribeOnPerfectDeflect(() => subscriber1Count++);
            CombatEvents.SubscribeOnPerfectDeflect(() => subscriber2Count++);
            CombatEvents.SubscribeOnPerfectDeflect(() => subscriber3Count++);

            // Act
            CombatEvents.RaisePerfectDeflect();

            // Assert
            Assert.AreEqual(1, subscriber1Count, "Subscriber 1 should receive event");
            Assert.AreEqual(1, subscriber2Count, "Subscriber 2 should receive event");
            Assert.AreEqual(1, subscriber3Count, "Subscriber 3 should receive event");
        }

        /// <summary>
        /// 测试用例 4：带参数的事件正确传递参数（伤害值）
        /// </summary>
        [Test]
        public void EventWithParameter_CorrectlyPassesParameter()
        {
            // Arrange
            float receivedDamage = 0f;
            CombatEvents.SubscribeOnPlayerDamaged((damage) => receivedDamage = damage);

            // Act
            CombatEvents.RaisePlayerDamaged(25.5f);

            // Assert
            Assert.AreEqual(25.5f, receivedDamage, 0.001f,
                "Damage parameter should be correctly passed to subscriber");
        }

        /// <summary>
        /// 测试用例 5：ClearAll() 清除所有订阅
        /// </summary>
        [Test]
        public void ClearAll_RemovesAllSubscriptions()
        {
            // Arrange
            int deflectCount = 0;
            int blockCount = 0;
            float damageReceived = 0f;

            CombatEvents.SubscribeOnPerfectDeflect(() => deflectCount++);
            CombatEvents.SubscribeOnNormalBlock(() => blockCount++);
            CombatEvents.SubscribeOnPlayerDamaged((damage) => damageReceived += damage);

            // Act - 验证订阅生效
            CombatEvents.RaisePerfectDeflect();
            CombatEvents.RaiseNormalBlock();
            CombatEvents.RaisePlayerDamaged(10f);

            Assert.AreEqual(1, deflectCount, "Deflect should work before ClearAll");
            Assert.AreEqual(1, blockCount, "Block should work before ClearAll");
            Assert.AreEqual(10f, damageReceived, 0.001f, "Damage should work before ClearAll");

            // 清除所有订阅
            CombatEvents.ClearAll();

            // 再次触发事件
            CombatEvents.RaisePerfectDeflect();
            CombatEvents.RaiseNormalBlock();
            CombatEvents.RaisePlayerDamaged(10f);

            // Assert - 计数不应增加
            Assert.AreEqual(1, deflectCount, "Deflect should not be invoked after ClearAll");
            Assert.AreEqual(1, blockCount, "Block should not be invoked after ClearAll");
            Assert.AreEqual(10f, damageReceived, 0.001f, "Damage should not be invoked after ClearAll");
        }

        /// <summary>
        /// 测试用例 6：带双参数的事件正确传递参数（架势条变化）
        /// </summary>
        [Test]
        public void EventWithTwoParameters_CorrectlyPassesParameters()
        {
            // Arrange
            float receivedCurrent = 0f;
            float receivedMax = 0f;
            CombatEvents.SubscribeOnPlayerPostureChanged((current, max) =>
            {
                receivedCurrent = current;
                receivedMax = max;
            });

            // Act
            CombatEvents.RaisePlayerPostureChanged(75f, 100f);

            // Assert
            Assert.AreEqual(75f, receivedCurrent, 0.001f, "Current posture should be 75");
            Assert.AreEqual(100f, receivedMax, 0.001f, "Max posture should be 100");
        }

        /// <summary>
        /// 测试用例 7：无订阅者时触发事件不会抛出异常
        /// </summary>
        [Test]
        public void RaiseEvent_NoSubscribers_DoesNotThrow()
        {
            // Act & Assert - 不应抛出异常
            Assert.DoesNotThrow(() => CombatEvents.RaisePerfectDeflect());
            Assert.DoesNotThrow(() => CombatEvents.RaisePlayerDamaged(10f));
            Assert.DoesNotThrow(() => CombatEvents.RaisePlayerPostureChanged(50f, 100f));
            Assert.DoesNotThrow(() => CombatEvents.RaiseBossPostureChanged(50f, 100f));
            Assert.DoesNotThrow(() => CombatEvents.RaiseCombatStart());
            Assert.DoesNotThrow(() => CombatEvents.RaiseCombatEnd());
        }

        /// <summary>
        /// 测试用例 8：所有事件类型都能正常工作
        /// </summary>
        [Test]
        public void AllEventTypes_WorkCorrectly()
        {
            // Arrange
            bool onPlayerDamaged = false;
            bool onBossDamaged = false;
            bool onPlayerPostureChanged = false;
            bool onBossPostureChanged = false;
            bool onPerfectDeflect = false;
            bool onNormalBlock = false;
            bool onMikiriCounter = false;
            bool onBossPostureBreak = false;
            bool onPlayerPostureBreak = false;
            bool onDeathblow = false;
            bool onLightningCounterSuccess = false;
            bool onLightningCounterFail = false;
            bool onHealingChargeChanged = false;
            bool onCombatStart = false;
            bool onCombatEnd = false;

            CombatEvents.SubscribeOnPlayerDamaged((d) => onPlayerDamaged = true);
            CombatEvents.SubscribeOnBossDamaged((d) => onBossDamaged = true);
            CombatEvents.SubscribeOnPlayerPostureChanged((c, m) => onPlayerPostureChanged = true);
            CombatEvents.SubscribeOnBossPostureChanged((c, m) => onBossPostureChanged = true);
            CombatEvents.SubscribeOnPerfectDeflect(() => onPerfectDeflect = true);
            CombatEvents.SubscribeOnNormalBlock(() => onNormalBlock = true);
            CombatEvents.SubscribeOnMikiriCounter(() => onMikiriCounter = true);
            CombatEvents.SubscribeOnBossPostureBreak(() => onBossPostureBreak = true);
            CombatEvents.SubscribeOnPlayerPostureBreak(() => onPlayerPostureBreak = true);
            CombatEvents.SubscribeOnDeathblow(() => onDeathblow = true);
            CombatEvents.SubscribeOnLightningCounterSuccess(() => onLightningCounterSuccess = true);
            CombatEvents.SubscribeOnLightningCounterFail(() => onLightningCounterFail = true);
            CombatEvents.SubscribeOnHealingChargeChanged((c) => onHealingChargeChanged = true);
            CombatEvents.SubscribeOnCombatStart(() => onCombatStart = true);
            CombatEvents.SubscribeOnCombatEnd(() => onCombatEnd = true);

            // Act
            CombatEvents.RaisePlayerDamaged(10f);
            CombatEvents.RaiseBossDamaged(20f);
            CombatEvents.RaisePlayerPostureChanged(50f, 100f);
            CombatEvents.RaiseBossPostureChanged(60f, 100f);
            CombatEvents.RaisePerfectDeflect();
            CombatEvents.RaiseNormalBlock();
            CombatEvents.RaiseMikiriCounter();
            CombatEvents.RaiseBossPostureBreak();
            CombatEvents.RaisePlayerPostureBreak();
            CombatEvents.RaiseDeathblow();
            CombatEvents.RaiseLightningCounterSuccess();
            CombatEvents.RaiseLightningCounterFail();
            CombatEvents.RaiseHealingChargeChanged(3);
            CombatEvents.RaiseCombatStart();
            CombatEvents.RaiseCombatEnd();

            // Assert
            Assert.IsTrue(onPlayerDamaged, "OnPlayerDamaged should be triggered");
            Assert.IsTrue(onBossDamaged, "OnBossDamaged should be triggered");
            Assert.IsTrue(onPlayerPostureChanged, "OnPlayerPostureChanged should be triggered");
            Assert.IsTrue(onBossPostureChanged, "OnBossPostureChanged should be triggered");
            Assert.IsTrue(onPerfectDeflect, "OnPerfectDeflect should be triggered");
            Assert.IsTrue(onNormalBlock, "OnNormalBlock should be triggered");
            Assert.IsTrue(onMikiriCounter, "OnMikiriCounter should be triggered");
            Assert.IsTrue(onBossPostureBreak, "OnBossPostureBreak should be triggered");
            Assert.IsTrue(onPlayerPostureBreak, "OnPlayerPostureBreak should be triggered");
            Assert.IsTrue(onDeathblow, "OnDeathblow should be triggered");
            Assert.IsTrue(onLightningCounterSuccess, "OnLightningCounterSuccess should be triggered");
            Assert.IsTrue(onLightningCounterFail, "OnLightningCounterFail should be triggered");
            Assert.IsTrue(onHealingChargeChanged, "OnHealingChargeChanged should be triggered");
            Assert.IsTrue(onCombatStart, "OnCombatStart should be triggered");
            Assert.IsTrue(onCombatEnd, "OnCombatEnd should be triggered");
        }

        #endregion
    }
}
