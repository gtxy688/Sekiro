using NUnit.Framework;
using Sekiro.Combat;
using Sekiro.Core.Data;
using Sekiro.Core.Events;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// PostureSystem 架势条系统 EditMode 单元测试。
    /// 覆盖：架势增加/减少、崩溃判定、恢复逻辑、事件触发。
    /// </summary>
    [TestFixture]
    public class PostureSystemTest
    {
        private CombatConfig _combatConfig;
        private PostureSystem _system;

        [SetUp]
        public void SetUp()
        {
            _combatConfig = ScriptableObject.CreateInstance<CombatConfig>();
            _combatConfig.postureRecoveryDelay = 2f;
            _combatConfig.postureRecoveryRate = 15f;

            _system = new GameObject("PostureSystemTest").AddComponent<PostureSystem>();
            SetConfig(_system, _combatConfig);
            _system.Initialize(300f, 15f, 2f);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.ClearAll();
            Object.DestroyImmediate(_combatConfig);
            if (_system != null && _system.gameObject != null)
                Object.DestroyImmediate(_system.gameObject);
        }

        #region Helper Methods

        /// <summary>
        /// 通过反射设置 PostureSystem 的 _combatConfig 字段
        /// </summary>
        private static void SetConfig(PostureSystem system, CombatConfig config)
        {
            var field = typeof(PostureSystem).GetField("_combatConfig",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(system, config);
        }

        private void SetFloatField(string fieldName, float value)
        {
            var field = typeof(PostureSystem).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_system, value);
        }

        private void SetBoolField(string fieldName, bool value)
        {
            var field = typeof(PostureSystem).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_system, value);
        }

        #endregion

        #region AddPosture Tests

        /// <summary>
        /// 架势未满不崩溃
        /// </summary>
        [Test]
        public void AddPosture_BelowMax_DoesNotTriggerBreak()
        {
            _system.AddPosture(100f);

            Assert.AreEqual(100f, _system.CurrentPosture, 0.001f,
                "Posture should be 100 after adding 100");
            Assert.IsFalse(_system.IsBroken,
                "Should not be broken when posture below max");
        }

        /// <summary>
        /// 架势超过最大值触发崩溃
        /// </summary>
        [Test]
        public void AddPosture_ExceedsMax_TriggersBreak()
        {
            bool breakFired = false;
            _system.OnPostureBroken += () => breakFired = true;

            _system.AddPosture(350f);

            Assert.IsTrue(_system.IsBroken,
                "Should be broken when posture exceeds max");
            Assert.IsTrue(breakFired,
                "OnPostureBroken event should fire");
            Assert.AreEqual(300f, _system.CurrentPosture, 0.001f,
                "Posture should be clamped to max");
        }

        /// <summary>
        /// 架势刚好满触发崩溃
        /// </summary>
        [Test]
        public void AddPosture_AtMax_TriggersBreak()
        {
            bool breakFired = false;
            _system.OnPostureBroken += () => breakFired = true;

            _system.AddPosture(300f);

            Assert.IsTrue(_system.IsBroken,
                "Should be broken when posture equals max");
            Assert.IsTrue(breakFired,
                "OnPostureBroken event should fire");
        }

        #endregion

        #region ReducePosture Tests

        /// <summary>
        /// 弹刀恢复架势
        /// </summary>
        [Test]
        public void ReducePosture_AfterHit_DecreasesPosture()
        {
            _system.AddPosture(150f);
            Assert.AreEqual(150f, _system.CurrentPosture, 0.001f);

            _system.ReducePosture(50f);

            Assert.AreEqual(100f, _system.CurrentPosture, 0.001f,
                "Posture should decrease by 50");
        }

        /// <summary>
        /// 架势不低于 0
        /// </summary>
        [Test]
        public void ReducePosture_BelowZero_ClampsToZero()
        {
            _system.AddPosture(50f);
            _system.ReducePosture(100f);

            Assert.AreEqual(0f, _system.CurrentPosture, 0.001f,
                "Posture should be clamped to 0");
        }

        #endregion

        #region Recovery Tests

        /// <summary>
        /// 脱战 2 秒后每秒恢复 15%
        /// </summary>
        [Test]
        public void Recovery_AfterDelay_RecoverRate15Percent()
        {
            _system.AddPosture(150f);
            // 距受击已过 3 秒（超过 2 秒恢复延迟），帧间隔 1 秒
            _system.TryRecover(1.0f, 3.0f);

            // 恢复量 = 300 * 0.15 * 1.0 * 1.0 = 45
            Assert.AreEqual(105f, _system.CurrentPosture, 0.5f,
                "After 1 second of recovery: 150 - 45 = 105");
        }

        /// <summary>
        /// 格挡时恢复速度减半
        /// </summary>
        [Test]
        public void Recovery_WhileBlocking_HalfRate()
        {
            _system.AddPosture(150f);
            _system.SetBlocking(true);

            _system.TryRecover(1.0f, 3.0f);

            // 恢复量 = 300 * 0.15 * 1.0 * 0.5 = 22.5
            Assert.AreEqual(127.5f, _system.CurrentPosture, 0.5f,
                "Recovery while blocking should be halved: 150 - 22.5 = 127.5");
        }

        /// <summary>
        /// 受击重置恢复计时器
        /// </summary>
        [Test]
        public void OnHit_ResetsRecoveryTimer()
        {
            _system.AddPosture(150f);

            // 受击重置计时器（_lastHitTime 被设为当前 Time.time ≈ 0）
            _system.OnHit();

            // 1 秒后尝试恢复（距受击仅 1 秒，未到 2 秒延迟）
            _system.TryRecover(1f, 1f);

            Assert.AreEqual(150f, _system.CurrentPosture, 0.001f,
                "No recovery should occur within delay after hit");
        }

        #endregion

        #region Property Tests

        /// <summary>
        /// 架势百分比计算正确
        /// </summary>
        [Test]
        public void PosturePercent_AtHalf_Returns05()
        {
            _system.AddPosture(150f);

            Assert.AreEqual(0.5f, _system.PosturePercent, 0.001f,
                "PosturePercent at 150/300 should be 0.5");
        }

        #endregion

        #region Event Tests

        /// <summary>
        /// 架势变化事件在 AddPosture 时触发
        /// </summary>
        [Test]
        public void OnPostureChanged_FiresOnAdd()
        {
            float receivedCurrent = -1f;
            float receivedMax = -1f;
            _system.OnPostureChanged += (current, max) =>
            {
                receivedCurrent = current;
                receivedMax = max;
            };

            _system.AddPosture(100f);

            Assert.AreEqual(100f, receivedCurrent, 0.001f,
                "Event should report current posture = 100");
            Assert.AreEqual(300f, receivedMax, 0.001f,
                "Event should report max posture = 300");
        }

        #endregion

        #region Static Method Direct Tests

        /// <summary>
        /// 直接测试 CalcRecoveryAmount 静态方法
        /// </summary>
        [Test]
        public void CalcRecoveryAmount_DirectCall_CorrectResults()
        {
            // 正常恢复：300 * 0.15 * 1.0 * 1.0 = 45
            Assert.AreEqual(45f,
                PostureSystem.CalcRecoveryAmount(300f, 0.15f, 1.0f, 1.0f), 0.001f);

            // 格挡恢复减半：300 * 0.15 * 1.0 * 0.5 = 22.5
            Assert.AreEqual(22.5f,
                PostureSystem.CalcRecoveryAmount(300f, 0.15f, 1.0f, 0.5f), 0.001f);

            // 零 deltaTime → 零恢复
            Assert.AreEqual(0f,
                PostureSystem.CalcRecoveryAmount(300f, 0.15f, 0f, 1.0f), 0.001f);
        }

        #endregion
    }
}
