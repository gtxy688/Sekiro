using NUnit.Framework;
using Sekiro.Combat;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// HitStopManager 帧冻结系统 EditMode 单元测试。
    /// 测试 Trigger/Stop/IsActive 状态管理。
    /// 注意：Time.timeScale 修改在 EditMode 下不可靠，核心计时逻辑在 PlayMode 中验证。
    /// </summary>
    [TestFixture]
    public class HitStopManagerTest
    {
        private GameObject _go;
        private HitStopManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestHitStop");
            _manager = _go.AddComponent<HitStopManager>();
        }

        [TearDown]
        public void TearDown()
        {
            // 确保恢复 timeScale
            Time.timeScale = 1f;
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        /// <summary>
        /// 触发帧冻结后应处于激活状态
        /// </summary>
        [Test]
        public void Trigger_ValidDuration_BecomesActive()
        {
            _manager.Trigger(0.05f);

            Assert.IsTrue(_manager.IsActive, "触发后应处于帧冻结状态");
            Assert.AreEqual(0.05f, _manager.RemainingTime, 0.001f,
                "剩余时间应等于触发时长");
        }

        /// <summary>
        /// 零时长不应触发帧冻结
        /// </summary>
        [Test]
        public void Trigger_ZeroDuration_StaysInactive()
        {
            _manager.Trigger(0f);

            Assert.IsFalse(_manager.IsActive, "零时长不应激活帧冻结");
        }

        /// <summary>
        /// 负数时长不应触发帧冻结
        /// </summary>
        [Test]
        public void Trigger_NegativeDuration_StaysInactive()
        {
            _manager.Trigger(-0.1f);

            Assert.IsFalse(_manager.IsActive, "负时长不应激活帧冻结");
        }

        /// <summary>
        /// Stop 应立刻终止帧冻结
        /// </summary>
        [Test]
        public void Stop_WhileActive_Deactivates()
        {
            _manager.Trigger(0.1f);
            Assert.IsTrue(_manager.IsActive, "测试前提：触发后激活");

            _manager.Stop();

            Assert.IsFalse(_manager.IsActive, "Stop 后应取消激活");
            Assert.AreEqual(0f, _manager.RemainingTime, 0.001f,
                "Stop 后剩余时间为 0");
        }

        /// <summary>
        /// 未激活时 Stop 不应抛出异常
        /// </summary>
        [Test]
        public void Stop_WhenInactive_NoOp()
        {
            Assert.IsFalse(_manager.IsActive);

            // 不应抛出异常
            Assert.DoesNotThrow(() => _manager.Stop());
        }
    }
}
