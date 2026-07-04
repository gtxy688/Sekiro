using System;
using System.Reflection;
using NUnit.Framework;
using Sekiro.Combat;
using Sekiro.Core.Data;
using Sekiro.Core.Events;
using Sekiro.Player.Combat;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// PlayerCombatController 的 EditMode 单元测试。
    /// 测试受击伤害计算、弹刀转发和回血逻辑。
    /// </summary>
    [TestFixture]
    public class PlayerCombatControllerTest
    {
        private const float Tolerance = 0.001f;

        private GameObject _playerGO;
        private PlayerCombatController _controller;
        private DeflectSystem _deflectSystem;
        private PostureSystem _postureSystem;
        private PlayerStats _playerStats;
        private DeflectConfig _deflectConfig;

        private float _bossDamageReceived;
        private Action<float> _bossDamagedHandler;
        private bool _perfectDeflectRaised;
        private Action _perfectDeflectHandler;

        #region SetUp / TearDown

        [SetUp]
        public void SetUp()
        {
            _playerGO = new GameObject("TestPlayer");

            // 创建 ScriptableObject 配置
            _playerStats = ScriptableObject.CreateInstance<PlayerStats>();
            _playerStats.maxHealth = 1000f;
            _playerStats.defense = 50f;
            _playerStats.maxPosture = 300f;
            _playerStats.postureRecoveryRate = 15f;
            _playerStats.maxHealingCharges = 10;
            _playerStats.healPercent = 0.3f;
            _playerStats.deflectPostureRecovery = 0.08f;

            _deflectConfig = ScriptableObject.CreateInstance<DeflectConfig>();

            // 添加组件
            _deflectSystem = _playerGO.AddComponent<DeflectSystem>();
            SetPrivateField(_deflectSystem, "_config", _deflectConfig);

            _postureSystem = _playerGO.AddComponent<PostureSystem>();

            _controller = _playerGO.AddComponent<PlayerCombatController>();
            _controller.Initialize(_playerStats, _deflectSystem, _postureSystem);

            // 事件追踪
            _bossDamageReceived = 0f;
            _bossDamagedHandler = (dmg) => _bossDamageReceived += dmg;
            CombatEvents.OnBossDamaged += _bossDamagedHandler;

            _perfectDeflectRaised = false;
            _perfectDeflectHandler = () => _perfectDeflectRaised = true;
            CombatEvents.OnPerfectDeflect += _perfectDeflectHandler;
        }

        [TearDown]
        public void TearDown()
        {
            CombatEvents.OnBossDamaged -= _bossDamagedHandler;
            CombatEvents.OnPerfectDeflect -= _perfectDeflectHandler;

            if (_playerGO != null)
                UnityEngine.Object.DestroyImmediate(_playerGO);
            if (_playerStats != null)
                UnityEngine.Object.DestroyImmediate(_playerStats);
            if (_deflectConfig != null)
                UnityEngine.Object.DestroyImmediate(_deflectConfig);
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 通过反射设置私有字段（Unity EditMode 测试常用模式）
        /// </summary>
        private static void SetPrivateField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(
                fieldName,
                BindingFlags.NonPublic | BindingFlags.Instance);
            field?.SetValue(target, value);
        }

        /// <summary>
        /// 创建测试用攻击数据
        /// </summary>
        private static AttackData CreateTestAttack(float damage, float postureDamage)
        {
            return new AttackData
            {
                attackName = "TestAttack",
                damage = damage,
                postureDamage = postureDamage,
                canBeDeflected = true,
                attackType = AttackType.Normal
            };
        }

        #endregion

        #region TakeDamage — 受击伤害计算

        /// <summary>
        /// 未防御时：全额 HP 伤害（damage - defense）和全额架势伤害
        /// </summary>
        [Test]
        public void TakeDamage_NoBlock_FullDamageAndPosture()
        {
            // Arrange
            var attack = CreateTestAttack(damage: 100f, postureDamage: 50f);

            // Act — DeflectResult.None 表示未防御
            _controller.ApplyDamageResult(attack, DeflectResult.None);

            // Assert
            // HP: (100 - 50) * 1.0 = 50 → 1000 - 50 = 950
            Assert.AreEqual(950f, _controller.CurrentHealth, Tolerance,
                "未防御时应承受全额 HP 伤害 (damage - defense)");

            // 架势: 50 * 1.0 = 50
            Assert.AreEqual(50f, _postureSystem.CurrentPosture, Tolerance,
                "未防御时应承受全额架势伤害");
        }

        /// <summary>
        /// 普通格挡时：HP 伤害 ×0.4，架势伤害 ×0.3
        /// </summary>
        [Test]
        public void TakeDamage_NormalBlock_ReducedDamageAndPosture()
        {
            // Arrange
            var attack = CreateTestAttack(damage: 100f, postureDamage: 50f);

            // Act
            _controller.ApplyDamageResult(attack, DeflectResult.NormalBlock);

            // Assert
            // HP: (100 - 50) * 0.4 = 20 → 1000 - 20 = 980
            Assert.AreEqual(980f, _controller.CurrentHealth, Tolerance,
                "普通格挡时 HP 伤害应为 (damage - defense) × 0.4");

            // 架势: 50 * 0.3 = 15
            Assert.AreEqual(15f, _postureSystem.CurrentPosture, Tolerance,
                "普通格挡时架势伤害应为 postureDamage × 0.3");
        }

        /// <summary>
        /// 完美弹刀时：零 HP 伤害，零自身架势伤害，Boss 架势上升，触发 PerfectDeflect 事件
        /// </summary>
        [Test]
        public void TakeDamage_PerfectDeflect_ZeroDamageAndBossPostureUp()
        {
            // Arrange
            var attack = CreateTestAttack(damage: 100f, postureDamage: 50f);

            // Act
            _controller.ApplyDamageResult(attack, DeflectResult.PerfectDeflect);

            // Assert — 玩家零伤害
            Assert.AreEqual(1000f, _controller.CurrentHealth, Tolerance,
                "完美弹刀时 HP 伤害应为 0");

            Assert.AreEqual(0f, _postureSystem.CurrentPosture, Tolerance,
                "完美弹刀时自身架势伤害应为 0");

            // Assert — Boss 架势伤害通过事件传递
            // 初始 multiplier = 1.0 (chain=0, spam=0)
            Assert.AreEqual(50f, _bossDamageReceived, Tolerance,
                "完美弹刀时应向 Boss 传递架势伤害 (postureDamage × multiplier)");

            // Assert — 完美弹刀事件触发
            Assert.IsTrue(_perfectDeflectRaised,
                "完美弹刀时应触发 OnPerfectDeflect 事件");
        }

        #endregion

        #region HandleDeflectPressed — 弹刀输入转发

        /// <summary>
        /// 弹刀按下输入应转发给 DeflectSystem，使其进入弹刀状态
        /// </summary>
        [Test]
        public void OnDeflectPressed_ForwardedToDeflectSystem()
        {
            // Arrange — 初始不在弹刀状态
            Assert.IsFalse(_deflectSystem.IsDeflecting,
                "测试前提：初始状态不应在弹刀中");

            // Act
            _controller.HandleDeflectPressed();

            // Assert
            Assert.IsTrue(_deflectSystem.IsDeflecting,
                "HandleDeflectPressed 应转发给 DeflectSystem.OnDeflectPressed()");
        }

        #endregion

        #region Heal — 回血逻辑

        /// <summary>
        /// 回血时：消耗次数，恢复生命值
        /// </summary>
        [Test]
        public void Heal_ChargesDecrease_HealthRecover()
        {
            // Arrange — 设置受伤状态
            _controller.SetHealth(500f);
            int chargesBefore = _controller.HealingCharges;

            // Act
            _controller.Heal();

            // Assert
            // 回血量 = maxHealth * healPercent = 1000 * 0.3 = 300
            // 预期生命: 500 + 300 = 800
            Assert.AreEqual(800f, _controller.CurrentHealth, Tolerance,
                "回血应恢复 maxHealth × healPercent 的生命值");

            // 次数减少 1
            Assert.AreEqual(chargesBefore - 1, _controller.HealingCharges,
                "回血应消耗 1 次使用次数");
        }

        /// <summary>
        /// 无回血次数时不应恢复生命
        /// </summary>
        [Test]
        public void Heal_NoCharges_NoHealing()
        {
            // Arrange — 耗尽回血次数
            _controller.SetHealth(500f);
            for (int i = 0; i < _playerStats.maxHealingCharges; i++)
                _controller.Heal();

            float healthBeforeHeal = _controller.CurrentHealth;

            // Act — 再次回血（应无效）
            _controller.Heal();

            // Assert
            Assert.AreEqual(healthBeforeHeal, _controller.CurrentHealth, Tolerance,
                "无回血次数时不应恢复生命值");
            Assert.AreEqual(0, _controller.HealingCharges,
                "回血次数应为 0");
        }

        #endregion

        #region DeflectResultToDefenseState — 状态映射

        /// <summary>
        /// 弹刀结果到防御状态的映射应正确
        /// </summary>
        [Test]
        public void DeflectResultToDefenseState_MapsCorrectly()
        {
            Assert.AreEqual(DefenseState.None,
                PlayerCombatController.DeflectResultToDefenseState(DeflectResult.None));
            Assert.AreEqual(DefenseState.NormalBlock,
                PlayerCombatController.DeflectResultToDefenseState(DeflectResult.NormalBlock));
            Assert.AreEqual(DefenseState.PerfectDeflect,
                PlayerCombatController.DeflectResultToDefenseState(DeflectResult.PerfectDeflect));
        }

        #endregion
    }
}
