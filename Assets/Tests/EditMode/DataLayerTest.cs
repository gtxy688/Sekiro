using NUnit.Framework;
using Sekiro.Core.Data;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// ScriptableObject 数据层 EditMode 单元测试。
    /// 覆盖：PlayerStats 默认值、BossStats 阶段属性、AttackData 默认值、
    ///       DeflectConfig 弹刀参数、CombatConfig 帧冻结参数。
    /// </summary>
    [TestFixture]
    public class DataLayerTest
    {
        #region 1. PlayerStats

        /// <summary>
        /// 测试用例 1：PlayerStats 可以创建并读取默认值
        /// </summary>
        [Test]
        public void PlayerStats_CreateInstance_HasCorrectDefaults()
        {
            // Arrange & Act
            var stats = ScriptableObject.CreateInstance<PlayerStats>();

            // Assert - 基础属性
            Assert.AreEqual(1000f, stats.maxHealth, "maxHealth default should be 1000");
            Assert.AreEqual(300f, stats.maxPosture, "maxPosture default should be 300");
            Assert.AreEqual(15f, stats.postureRecoveryRate, "postureRecoveryRate default should be 15");
            Assert.AreEqual(100f, stats.attack, "attack default should be 100");
            Assert.AreEqual(50f, stats.defense, "defense default should be 50");
            Assert.AreEqual(5f, stats.moveSpeed, "moveSpeed default should be 5");

            // Assert - 弹刀参数
            Assert.AreEqual(0.2f, stats.baseDeflectWindow, "baseDeflectWindow default should be 0.2");
            Assert.AreEqual(0.016f, stats.minDeflectWindow, "minDeflectWindow default should be 0.016");
            Assert.AreEqual(0.5f, stats.spamResetTime, "spamResetTime default should be 0.5");
            Assert.AreEqual(0.015f, stats.windowReductionPerSpam, "windowReductionPerSpam default should be 0.015");
            Assert.AreEqual(0.08f, stats.deflectPostureRecovery, "deflectPostureRecovery default should be 0.08");

            // Assert - 连续弹刀加成
            Assert.IsNotNull(stats.deflectChainMultipliers, "deflectChainMultipliers should not be null");
            Assert.AreEqual(5, stats.deflectChainMultipliers.Length, "deflectChainMultipliers should have 5 elements");
            Assert.AreEqual(1.0f, stats.deflectChainMultipliers[0], "Chain multiplier [0] should be 1.0");
            Assert.AreEqual(1.5f, stats.deflectChainMultipliers[4], "Chain multiplier [4] should be 1.5");

            // Assert - 回血
            Assert.AreEqual(10, stats.maxHealingCharges, "maxHealingCharges default should be 10");
            Assert.AreEqual(0.3f, stats.healPercent, "healPercent default should be 0.3");
            Assert.AreEqual(0.8f, stats.healDuration, "healDuration default should be 0.8");

            // Cleanup
            Object.DestroyImmediate(stats);
        }

        #endregion

        #region 2. BossStats

        /// <summary>
        /// 测试用例 2a：BossStats 可以创建并读取一阶段默认值
        /// </summary>
        [Test]
        public void BossStats_CreateInstance_Phase1Defaults()
        {
            // Arrange & Act
            var stats = ScriptableObject.CreateInstance<BossStats>();

            // Assert - 一阶段
            Assert.AreEqual(1000f, stats.phase1MaxHealth, "phase1MaxHealth default should be 1000");
            Assert.AreEqual(300f, stats.phase1MaxPosture, "phase1MaxPosture default should be 300");
            Assert.AreEqual(100f, stats.phase1Attack, "phase1Attack default should be 100");

            // Cleanup
            Object.DestroyImmediate(stats);
        }

        /// <summary>
        /// 测试用例 2b：BossStats 可以读取二阶段默认值
        /// </summary>
        [Test]
        public void BossStats_CreateInstance_Phase2Defaults()
        {
            // Arrange & Act
            var stats = ScriptableObject.CreateInstance<BossStats>();

            // Assert - 二阶段
            Assert.AreEqual(1200f, stats.phase2MaxHealth, "phase2MaxHealth default should be 1200");
            Assert.AreEqual(400f, stats.phase2MaxPosture, "phase2MaxPosture default should be 400");
            Assert.AreEqual(120f, stats.phase2Attack, "phase2Attack default should be 120");

            // Assert - 阶段转换
            Assert.AreEqual(0.5f, stats.phaseTransitionHealthPercent,
                "phaseTransitionHealthPercent default should be 0.5");

            // Cleanup
            Object.DestroyImmediate(stats);
        }

        #endregion

        #region 3. AttackData

        /// <summary>
        /// 测试用例 3：AttackData 默认值正确
        /// </summary>
        [Test]
        public void AttackData_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var attack = new AttackData();

            // Assert
            Assert.AreEqual(AttackType.Normal, attack.attackType, "attackType default should be Normal");
            Assert.AreEqual(false, attack.canBeDeflected, "canBeDeflected default should be false");
            Assert.AreEqual(0f, attack.damage, "damage default should be 0");
            Assert.AreEqual(0f, attack.postureDamage, "postureDamage default should be 0");
            Assert.AreEqual(0f, attack.startupFrames, "startupFrames default should be 0");
            Assert.AreEqual(0f, attack.activeFrames, "activeFrames default should be 0");
            Assert.AreEqual(0f, attack.recoveryFrames, "recoveryFrames default should be 0");
            Assert.AreEqual(false, attack.isRanged, "isRanged default should be false");
            Assert.IsNull(attack.attackName, "attackName default should be null");
            Assert.IsNull(attack.animName, "animName default should be null");
            Assert.AreEqual(Vector3.zero, attack.hitboxOffset, "hitboxOffset default should be Vector3.zero");
        }

        /// <summary>
        /// 测试用例 3b：AttackType 枚举包含所有预期值
        /// </summary>
        [Test]
        public void AttackType_ContainsAllExpectedValues()
        {
            // Assert
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackType), AttackType.Normal));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackType), AttackType.Thrust));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackType), AttackType.Sweep));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackType), AttackType.Grab));
            Assert.IsTrue(System.Enum.IsDefined(typeof(AttackType), AttackType.Lightning));
        }

        #endregion

        #region 4. DeflectConfig

        /// <summary>
        /// 测试用例 4：DeflectConfig 弹刀参数正确
        /// </summary>
        [Test]
        public void DeflectConfig_CreateInstance_HasCorrectDefaults()
        {
            // Arrange & Act
            var config = ScriptableObject.CreateInstance<DeflectConfig>();

            // Assert - 玩家弹刀
            Assert.AreEqual(0.2f, config.baseDeflectWindow, "baseDeflectWindow default should be 0.2");
            Assert.AreEqual(0.016f, config.minDeflectWindow, "minDeflectWindow default should be 0.016");
            Assert.AreEqual(0.5f, config.spamResetTime, "spamResetTime default should be 0.5");
            Assert.AreEqual(0.015f, config.windowReductionPerSpam, "windowReductionPerSpam default should be 0.015");

            // Assert - Boss弹刀
            Assert.AreEqual(0.15f, config.bossDeflectWindow, "bossDeflectWindow default should be 0.15");
            Assert.AreEqual(0.3f, config.bossBlockWindow, "bossBlockWindow default should be 0.3");
            Assert.AreEqual(0.4f, config.bossStanceDuration, "bossStanceDuration default should be 0.4");

            // Assert - AI弹刀概率
            Assert.AreEqual(0.4f, config.deflectChanceOnAttack, "deflectChanceOnAttack default should be 0.4");
            Assert.AreEqual(0.6f, config.deflectChanceInCombo, "deflectChanceInCombo default should be 0.6");
            Assert.AreEqual(0.25f, config.deflectChanceLowHealth, "deflectChanceLowHealth default should be 0.25");

            // Cleanup
            Object.DestroyImmediate(config);
        }

        #endregion

        #region 5. CombatConfig

        /// <summary>
        /// 测试用例 5：CombatConfig 帧冻结参数正确
        /// </summary>
        [Test]
        public void CombatConfig_CreateInstance_HasCorrectDefaults()
        {
            // Arrange & Act
            var config = ScriptableObject.CreateInstance<CombatConfig>();

            // Assert - 帧冻结
            Assert.AreEqual(0.033f, config.normalHitStop, 0.001f, "normalHitStop default should be 0.033");
            Assert.AreEqual(0.05f, config.deflectHitStop, 0.001f, "deflectHitStop default should be 0.05");
            Assert.AreEqual(0.066f, config.mikiriHitStop, 0.001f, "mikiriHitStop default should be 0.066");

            // Assert - 架势
            Assert.AreEqual(2f, config.postureRecoveryDelay, "postureRecoveryDelay default should be 2");
            Assert.AreEqual(15f, config.postureRecoveryRate, "postureRecoveryRate default should be 15");

            // Assert - 输入缓冲
            Assert.AreEqual(0.15f, config.inputBufferWindow, 0.001f, "inputBufferWindow default should be 0.15");

            // Cleanup
            Object.DestroyImmediate(config);
        }

        #endregion
    }
}
