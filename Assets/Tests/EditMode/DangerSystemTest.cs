using NUnit.Framework;
using Sekiro.Combat;
using Sekiro.Core.Data;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// DangerSystem 危字系统 EditMode 单元测试。
    /// 测试识破判定、踩头判定等核心逻辑。
    /// </summary>
    [TestFixture]
    public class DangerSystemTest
    {
        private GameObject _go;
        private DangerSystem _dangerSystem;
        private GameObject _bossGo;

        private const float Tolerance = 0.001f;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestPlayer");
            _bossGo = new GameObject("TestBoss");
            _dangerSystem = _go.AddComponent<DangerSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (_bossGo != null)
                Object.DestroyImmediate(_bossGo);
        }

        #region 识破判定

        /// <summary>
        /// 无激活危字时不可识破
        /// </summary>
        [Test]
        public void CanMikiri_NoActiveDanger_ReturnsFalse()
        {
            bool result = _dangerSystem.CanMikiri(
                Vector3.zero, Vector3.forward);

            Assert.IsFalse(result, "无危字时不可识破");
        }

        /// <summary>
        /// 扫击危字不可识破
        /// </summary>
        [Test]
        public void CanMikiri_SweepDanger_ReturnsFalse()
        {
            SetupDanger(AttackType.Sweep, Vector3.zero);

            bool result = _dangerSystem.CanMikiri(
                new Vector3(1f, 0f, 0f), Vector3.forward);

            Assert.IsFalse(result, "扫击危不可识破");
        }

        /// <summary>
        /// 突刺危且在距离/角度范围内时返回 true
        /// </summary>
        [Test]
        public void CanMikiri_ThrustInRange_ReturnsTrue()
        {
            // Boss 在原点朝 +Z，玩家在 (0, 0, 2) — 距离 2m，角度 0°
            _bossGo.transform.position = Vector3.zero;
            _bossGo.transform.forward = Vector3.forward;
            SetupDanger(AttackType.Thrust, _bossGo.transform);

            _go.transform.position = new Vector3(0f, 0f, 2f);

            bool result = _dangerSystem.CanMikiri(
                _go.transform.position, Vector3.forward);

            Assert.IsTrue(result, "突刺危在范围内应可识破");
        }

        /// <summary>
        /// 超出距离时不可识破
        /// </summary>
        [Test]
        public void CanMikiri_ThrustOutOfRange_ReturnsFalse()
        {
            _bossGo.transform.position = Vector3.zero;
            _bossGo.transform.forward = Vector3.forward;
            SetupDanger(AttackType.Thrust, _bossGo.transform);

            // 玩家在 5m 外（默认最大 3m）
            _go.transform.position = new Vector3(0f, 0f, 5f);

            bool result = _dangerSystem.CanMikiri(
                _go.transform.position, Vector3.forward);

            Assert.IsFalse(result, "超出距离不可识破");
        }

        #endregion

        #region 踩头判定

        /// <summary>
        /// 玩家在 Boss 上方且在下落阶段时可踩头
        /// </summary>
        [Test]
        public void CanStomp_AboveBossFalling_ReturnsTrue()
        {
            SetupDanger(AttackType.Sweep, _bossGo.transform);
            _bossGo.transform.position = new Vector3(0f, 0f, 0f);
            _go.transform.position = new Vector3(0f, 0.3f, 0f); // 头顶附近

            // 下落中（Y 速度 < 0）
            bool result = _dangerSystem.CanStomp(_go.transform.position, -5f);

            Assert.IsTrue(result, "在 Boss 上方下落时应可踩头");
        }

        /// <summary>
        /// 上升阶段不可踩头
        /// </summary>
        [Test]
        public void CanStomp_Ascending_ReturnsFalse()
        {
            SetupDanger(AttackType.Sweep, _bossGo.transform);
            _bossGo.transform.position = Vector3.zero;
            _go.transform.position = new Vector3(0f, 0.3f, 0f);

            bool result = _dangerSystem.CanStomp(_go.transform.position, 5f);

            Assert.IsFalse(result, "上升阶段不可踩头");
        }

        /// <summary>
        /// 踩头必须在 Boss 头顶附近
        /// </summary>
        [Test]
        public void CanStomp_TooHigh_ReturnsFalse()
        {
            SetupDanger(AttackType.Sweep, _bossGo.transform);
            _bossGo.transform.position = Vector3.zero;
            _go.transform.position = new Vector3(0f, 2f, 0f); // 太高

            bool result = _dangerSystem.CanStomp(_go.transform.position, -5f);

            Assert.IsFalse(result, "太高不可踩头");
        }

        /// <summary>
        /// 非扫击危不可踩头
        /// </summary>
        [Test]
        public void CanStomp_ThrustDanger_ReturnsFalse()
        {
            SetupDanger(AttackType.Thrust, _bossGo.transform);
            _bossGo.transform.position = Vector3.zero;
            _go.transform.position = new Vector3(0f, 0.3f, 0f);

            bool result = _dangerSystem.CanStomp(_go.transform.position, -5f);

            Assert.IsFalse(result, "突刺危不可踩头");
        }

        #endregion

        #region Helpers

        /// <summary>
        /// 通过反射激活危字（DangerSystem.ActivateDanger 是 public 方法）
        /// </summary>
        private void SetupDanger(AttackType type, Transform source)
        {
            _dangerSystem.ActivateDanger(type, source);
        }

        #endregion
    }
}
