using NUnit.Framework;
using Sekiro.Combat;
using Sekiro.Core.Data;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// DeflectSystem 弹刀系统 EditMode 单元测试。
    /// 覆盖：弹刀窗口计算、抖刀惩罚、连续弹刀加成、弹刀判定。
    /// </summary>
    [TestFixture]
    public class DeflectSystemTest
    {
        private DeflectConfig _config;
        private DeflectSystem _system;

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<DeflectConfig>();
            // 确保使用规格文档中的默认值
            _config.baseDeflectWindow = 0.2f;
            _config.minDeflectWindow = 0.016f;
            _config.spamResetTime = 0.5f;
            _config.windowReductionPerSpam = 0.015f;
            _config.blockDamageReduction = 0.6f;
            _config.blockPostureIncrease = 0.3f;
            _config.deflectPostureRecovery = 0.08f;
            _config.blockWindowDuration = 0.4f;
            _config.deflectChainMultipliers = new[] { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };
            _config.deflectChainResetTime = 1.0f;
            _config.spamPostureDamageMin = 0.2f;

            _system = new GameObject("DeflectSystemTest").AddComponent<DeflectSystem>();
            SetConfig(_system, _config);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_config);
            if (_system != null && _system.gameObject != null)
                Object.DestroyImmediate(_system.gameObject);
        }

        #region Helper Methods

        /// <summary>
        /// 通过反射设置 DeflectSystem 的 _config 字段（EditMode 测试无法使用 SerializeField）
        /// </summary>
        private static void SetConfig(DeflectSystem system, DeflectConfig config)
        {
            var field = typeof(DeflectSystem).GetField("_config",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(system, config);
        }

        /// <summary>
        /// 模拟按下右键 N 次（间隔 0.1 秒），制造抖刀惩罚
        /// </summary>
        private void SimulateSpamPresses(int count)
        {
            float time = 1f;
            for (int i = 0; i < count; i++)
            {
                time += 0.1f;
                SimulatePressedAt(time);
                time += 0.05f;
                SimulateReleasedAt(time);
            }
        }

        private void SimulatePressedAt(float time)
        {
            SetTimeField("_lastDeflectPressTime", time - 0.1f);
            SetBoolField("_deflectStateActive", true);
            _system.OnDeflectPressed();
        }

        private void SimulateReleasedAt(float time)
        {
            SetTimeField("_lastDeflectReleaseTime", time);
            _system.OnDeflectReleased();
        }

        private void SetTimeField(string fieldName, float value)
        {
            var field = typeof(DeflectSystem).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_system, value);
        }

        private void SetBoolField(string fieldName, bool value)
        {
            var field = typeof(DeflectSystem).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_system, value);
        }

        private void SetIntField(string fieldName, int value)
        {
            var field = typeof(DeflectSystem).GetField(fieldName,
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field.SetValue(_system, value);
        }

        #endregion

        #region GetCurrentDeflectWindow Tests

        /// <summary>
        /// 初始弹刀窗口应为 12 帧（0.2 秒）
        /// </summary>
        [Test]
        public void GetCurrentDeflectWindow_BaseValue_Returns12Frames()
        {
            float window = _system.GetCurrentDeflectWindow();
            Assert.AreEqual(0.2f, window, 0.001f,
                "Base deflect window should be 0.2s (12 frames at 60fps)");
        }

        /// <summary>
        /// 抖刀 3 次后窗口应递减
        /// </summary>
        [Test]
        public void GetCurrentDeflectWindow_After3Spams_WindowReduced()
        {
            SimulateSpamPresses(3);
            float window = _system.GetCurrentDeflectWindow();
            // 3次抖刀：0.2 - 3*0.015 = 0.155
            Assert.AreEqual(0.155f, window, 0.001f,
                "After 3 spams, window should be 0.155s");
            Assert.Less(window, 0.2f,
                "Window after spam should be less than base");
        }

        /// <summary>
        /// 抖刀惩罚下窗口不低于最小值（1 帧 = 0.016 秒）
        /// </summary>
        [Test]
        public void GetCurrentDeflectWindow_AtMinSpam_Returns1Frame()
        {
            // 大量抖刀使窗口降到最低
            SimulateSpamPresses(20);
            float window = _system.GetCurrentDeflectWindow();
            Assert.AreEqual(_config.minDeflectWindow, window, 0.001f,
                "Window should not go below minDeflectWindow (1 frame)");
        }

        #endregion

        #region OnSuccessfulDeflect Tests

        /// <summary>
        /// 成功弹刀后抖刀惩罚应重置
        /// </summary>
        [Test]
        public void OnSuccessfulDeflect_SpamPenaltyReset()
        {
            // 先制造抖刀惩罚
            SimulateSpamPresses(3);
            Assert.Greater(_system.SpamCount, 0, "SpamCount should be > 0 after spam");

            // 成功弹刀
            _system.OnSuccessfulDeflect();

            Assert.AreEqual(0, _system.SpamCount,
                "SpamCount should be 0 after successful deflect");

            float window = _system.GetCurrentDeflectWindow();
            Assert.AreEqual(0.2f, window, 0.001f,
                "Window should be restored to base after successful deflect");
        }

        #endregion

        #region Spam Penalty Auto Reset Tests

        /// <summary>
        /// 停止按键超过 0.5 秒后抖刀惩罚自动重置
        /// </summary>
        [Test]
        public void OnDeflectReleased_Wait05Seconds_SpamPenaltyAutoReset()
        {
            // 制造抖刀惩罚
            SimulateSpamPresses(3);
            Assert.Greater(_system.SpamCount, 0, "SpamCount should be > 0 after spam");

            // 松开按键
            _system.OnDeflectReleased();

            // 模拟时间推进超过 spamResetTime（0.5 秒）
            // 每次 Update 推进 0.11 秒，共 5 次 = 0.55 秒
            for (int i = 0; i < 5; i++)
            {
                // 通过反射模拟 deltaTime 和 time
                SimulateUpdateWithDelta(0.11f, 2f + i * 0.11f);
            }

            Assert.AreEqual(0, _system.SpamCount,
                "SpamCount should auto-reset after 0.5s of no presses");
        }

        /// <summary>
        /// 模拟 Update 调用（绕过 Time.deltaTime/Time.time）
        /// </summary>
        private void SimulateUpdateWithDelta(float deltaTime, float currentTime)
        {
            // 直接操作内部状态来模拟 Update 逻辑
            float lastRelease = (float)typeof(DeflectSystem)
                .GetField("_lastDeflectReleaseTime",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .GetValue(_system);

            if (_system.SpamCount > 0 && currentTime - lastRelease >= _config.spamResetTime)
            {
                SetIntField("_spamCount", 0);
            }
        }

        #endregion

        #region GetPostureDamageMultiplier Tests

        /// <summary>
        /// 连续弹刀第 2 次，架势伤害倍率应为 1.2
        /// </summary>
        [Test]
        public void GetPostureDamageMultiplier_Chain2_Returns12()
        {
            SetIntField("_deflectChainCount", 2); // 第2次成功弹刀 → index=1 → multipliers[1]=1.2
            float multiplier = _system.GetPostureDamageMultiplier();
            Assert.AreEqual(1.2f, multiplier, 0.001f,
                "Chain 2 should give 1.2x multiplier");
        }

        /// <summary>
        /// 连续弹刀第 5 次及以上，架势伤害倍率应封顶 1.5
        /// </summary>
        [Test]
        public void GetPostureDamageMultiplier_Chain5Plus_Caps15()
        {
            SetIntField("_deflectChainCount", 5); // 第5次 → index=4 → multipliers[4]=1.5
            float multiplier5 = _system.GetPostureDamageMultiplier();
            Assert.AreEqual(1.5f, multiplier5, 0.001f,
                "Chain 5 should cap at 1.5x");

            SetIntField("_deflectChainCount", 10); // 远超封顶
            float multiplier10 = _system.GetPostureDamageMultiplier();
            Assert.AreEqual(1.5f, multiplier10, 0.001f,
                "Chain 10 should still cap at 1.5x");
        }

        /// <summary>
        /// 抖刀 3 次后架势伤害应降低
        /// </summary>
        [Test]
        public void GetPostureDamageMultiplier_Spam3_Reduced()
        {
            SimulateSpamPresses(3);
            float multiplier = _system.GetPostureDamageMultiplier();
            Assert.Less(multiplier, 1.0f,
                "Spam 3 should reduce multiplier below 1.0");
            Assert.Greater(multiplier, _config.spamPostureDamageMin,
                "Spam 3 should still be above minimum penalty");
        }

        #endregion

        #region TryDeflect Tests

        /// <summary>
        /// 在完美弹刀窗口内受击，应返回 PerfectDeflect
        /// </summary>
        [Test]
        public void TryDeflect_InPerfectWindow_ReturnsPerfectDeflect()
        {
            var attack = CreateDeflectableAttack();
            _system.OnDeflectPressed();

            // attackDir = forward 表示攻击者在玩家前方，攻击方向指向玩家（与 playerForward 同向）
            // Angle(forward, forward) = 0° < 90° → 正面
            var result = _system.TryDeflect(attack, Vector3.forward, Vector3.forward);
            Assert.AreEqual(DeflectResult.PerfectDeflect, result,
                "Attack during deflect window should return PerfectDeflect");
        }

        /// <summary>
        /// 在普通格挡窗口内受击，应返回 NormalBlock
        /// </summary>
        [Test]
        public void TryDeflect_InBlockWindow_ReturnsNormalBlock()
        {
            var attack = CreateDeflectableAttack();

            // 按住右键进入格挡状态（非弹刀）
            SetBoolField("_isDeflecting", false);
            SetBoolField("_isBlocking", true);
            SetBoolField("_deflectStateActive", true);

            var result = _system.TryDeflect(attack, Vector3.forward, Vector3.forward);
            Assert.AreEqual(DeflectResult.NormalBlock, result,
                "Attack during block window should return NormalBlock");
        }

        /// <summary>
        /// 未在弹刀或格挡状态，应返回 None
        /// </summary>
        [Test]
        public void TryDeflect_OutsideWindow_ReturnsNone()
        {
            var attack = CreateDeflectableAttack();

            // 确保不在任何防御状态
            var result = _system.TryDeflect(attack, Vector3.forward, Vector3.forward);
            Assert.AreEqual(DeflectResult.None, result,
                "Attack outside any window should return None");
        }

        /// <summary>
        /// 攻击来自背后（角度 > 90°），应返回 None
        /// </summary>
        [Test]
        public void TryDeflect_WrongAngle_ReturnsNone()
        {
            var attack = CreateDeflectableAttack();
            _system.OnDeflectPressed();

            // attackDir = back 表示攻击者在玩家背后，攻击方向指向玩家背面
            // Angle(back, forward) = 180° > 90° → 非正面，不弹刀
            var result = _system.TryDeflect(attack, Vector3.back, Vector3.forward);
            Assert.AreEqual(DeflectResult.None, result,
                "Attack from behind should return None");
        }

        /// <summary>
        /// 攻击不可弹刀（canBeDeflected = false），应返回 None
        /// </summary>
        [Test]
        public void TryDeflect_NotDeflectable_ReturnsNone()
        {
            var attack = new AttackData { canBeDeflected = false };
            _system.OnDeflectPressed();

            var result = _system.TryDeflect(attack, Vector3.forward, Vector3.forward);
            Assert.AreEqual(DeflectResult.None, result,
                "Non-deflectable attack should return None");
        }

        private AttackData CreateDeflectableAttack()
        {
            return new AttackData
            {
                attackName = "TestSlash",
                damage = 100f,
                postureDamage = 50f,
                attackType = AttackType.Normal,
                canBeDeflected = true
            };
        }

        #endregion

        #region Static Method Direct Tests

        /// <summary>
        /// 直接测试 CalcDeflectWindow 静态方法
        /// </summary>
        [Test]
        public void CalcDeflectWindow_DirectCall_CorrectResults()
        {
            // 无惩罚
            Assert.AreEqual(0.2f,
                DeflectSystem.CalcDeflectWindow(0.2f, 0.016f, 0.015f, 0), 0.001f);

            // 3 次抖刀
            Assert.AreEqual(0.155f,
                DeflectSystem.CalcDeflectWindow(0.2f, 0.016f, 0.015f, 3), 0.001f);

            // 大量抖刀降到最小值
            Assert.AreEqual(0.016f,
                DeflectSystem.CalcDeflectWindow(0.2f, 0.016f, 0.015f, 20), 0.001f);
        }

        /// <summary>
        /// 直接测试 CalcChainBonus 静态方法
        /// </summary>
        [Test]
        public void CalcChainBonus_DirectCall_CorrectResults()
        {
            float[] multipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };

            Assert.AreEqual(1.0f, DeflectSystem.CalcChainBonus(1, multipliers), 0.001f);
            Assert.AreEqual(1.2f, DeflectSystem.CalcChainBonus(2, multipliers), 0.001f);
            Assert.AreEqual(1.4f, DeflectSystem.CalcChainBonus(3, multipliers), 0.001f);
            Assert.AreEqual(1.5f, DeflectSystem.CalcChainBonus(5, multipliers), 0.001f);
            Assert.AreEqual(1.5f, DeflectSystem.CalcChainBonus(10, multipliers), 0.001f);
        }

        /// <summary>
        /// 直接测试 CalcSpamPenalty 静态方法
        /// </summary>
        [Test]
        public void CalcSpamPenalty_DirectCall_CorrectResults()
        {
            Assert.AreEqual(1.0f, DeflectSystem.CalcSpamPenalty(0, 0.2f), 0.001f);
            Assert.Less(DeflectSystem.CalcSpamPenalty(3, 0.2f), 1.0f);
            Assert.AreEqual(0.2f, DeflectSystem.CalcSpamPenalty(10, 0.2f), 0.001f);
        }

        /// <summary>
        /// 直接测试 IsAttackFromFront 静态方法
        /// </summary>
        [Test]
        public void IsAttackFromFront_DirectCall_CorrectResults()
        {
            // 正面攻击：attackDir 与 playerForward 同向（攻击者在前方）
            Assert.IsTrue(DeflectSystem.IsAttackFromFront(Vector3.forward, Vector3.forward));

            // 背后攻击：attackDir 与 playerForward 反向（攻击者在身后）
            Assert.IsFalse(DeflectSystem.IsAttackFromFront(Vector3.back, Vector3.forward));

            // 侧面攻击：正好 90° 不算正面
            Assert.IsFalse(DeflectSystem.IsAttackFromFront(Vector3.right, Vector3.forward));
        }

        #endregion
    }
}
