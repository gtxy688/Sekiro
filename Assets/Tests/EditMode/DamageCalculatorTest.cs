using NUnit.Framework;
using Sekiro.Combat;
using Sekiro.Core.Data;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// DamageCalculator 伤害计算系统 EditMode 单元测试。
    /// 覆盖：HP 伤害计算（未防御/普通格挡/完美弹刀/防御减免/不为负数）、
    /// 架势伤害计算（未防御/普通格挡/弹刀倍率加成）。
    /// </summary>
    [TestFixture]
    public class DamageCalculatorTest
    {
        #region Helpers

        /// <summary>
        /// 创建测试用 AttackData
        /// </summary>
        private static AttackData CreateAttack(float damage, float postureDamage)
        {
            return new AttackData
            {
                attackName = "TestAttack",
                damage = damage,
                postureDamage = postureDamage,
                attackType = AttackType.Normal,
                canBeDeflected = true
            };
        }

        #endregion

        #region CalculateDamage — HP 伤害计算

        /// <summary>
        /// 未防御时承受全额伤害（减去防御值后）
        /// </summary>
        [Test]
        public void CalculateDamage_NoBlock_FullDamage()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculateDamage(attack, 10f, DefenseState.None);

            // 100 - 10 = 90（未防御，全额伤害）
            Assert.AreEqual(90f, result, 0.001f,
                "No block: full damage minus defense = 100 - 10 = 90");
        }

        /// <summary>
        /// 普通格挡减伤 60%，只承受 40% 伤害
        /// </summary>
        [Test]
        public void CalculateDamage_NormalBlock_40Percent()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculateDamage(attack, 0f, DefenseState.NormalBlock);

            // (100 - 0) * 0.4 = 40
            Assert.AreEqual(40f, result, 0.001f,
                "Normal block: damage * 0.4 = 100 * 0.4 = 40");
        }

        /// <summary>
        /// 完美弹刀零伤害
        /// </summary>
        [Test]
        public void CalculateDamage_PerfectDeflect_ZeroDamage()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculateDamage(attack, 0f, DefenseState.PerfectDeflect);

            Assert.AreEqual(0f, result, 0.001f,
                "Perfect deflect: damage should be 0");
        }

        /// <summary>
        /// 防御值减少伤害
        /// </summary>
        [Test]
        public void CalculateDamage_DefenseReducesDamage()
        {
            var attack = CreateAttack(100f, 50f);

            float resultNoDef = DamageCalculator.CalculateDamage(attack, 0f, DefenseState.None);
            float resultWithDef = DamageCalculator.CalculateDamage(attack, 30f, DefenseState.None);

            Assert.AreEqual(100f, resultNoDef, 0.001f,
                "No defense: full damage = 100");
            Assert.AreEqual(70f, resultWithDef, 0.001f,
                "With 30 defense: 100 - 30 = 70");
            Assert.Less(resultWithDef, resultNoDef,
                "Defense should reduce damage");
        }

        /// <summary>
        /// 伤害永远不为负数（防御值超过伤害时钳制为 0）
        /// </summary>
        [Test]
        public void CalculateDamage_NeverNegative()
        {
            var attack = CreateAttack(10f, 5f);

            float result = DamageCalculator.CalculateDamage(attack, 100f, DefenseState.None);

            Assert.AreEqual(0f, result, 0.001f,
                "Damage should never be negative: 10 - 100 = clamped to 0");
            Assert.GreaterOrEqual(result, 0f,
                "Damage must be >= 0");
        }

        #endregion

        #region CalculatePostureDamage — 架势伤害计算

        /// <summary>
        /// 未防御时承受全额架势伤害
        /// </summary>
        [Test]
        public void CalculatePostureDamage_NoBlock_FullValue()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculatePostureDamage(attack, DefenseState.None, 1f);

            Assert.AreEqual(50f, result, 0.001f,
                "No block: full posture damage = 50");
        }

        /// <summary>
        /// 普通格挡架势伤害 × 0.3
        /// </summary>
        [Test]
        public void CalculatePostureDamage_NormalBlock_30Percent()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculatePostureDamage(attack, DefenseState.NormalBlock, 1f);

            // 50 * 0.3 = 15
            Assert.AreEqual(15f, result, 0.001f,
                "Normal block: posture * 0.3 = 50 * 0.3 = 15");
        }

        /// <summary>
        /// 弹刀倍率加成应用于架势伤害
        /// </summary>
        [Test]
        public void CalculatePostureDamage_WithDeflectMultiplier_Applied()
        {
            var attack = CreateAttack(100f, 50f);

            // 未防御 + 1.5 倍弹刀加成
            float result = DamageCalculator.CalculatePostureDamage(attack, DefenseState.None, 1.5f);

            // 50 * 1.5 = 75
            Assert.AreEqual(75f, result, 0.001f,
                "No block with 1.5x deflect multiplier: 50 * 1.5 = 75");
        }

        /// <summary>
        /// 完美弹刀时架势伤害也为零
        /// </summary>
        [Test]
        public void CalculatePostureDamage_PerfectDeflect_ZeroPosture()
        {
            var attack = CreateAttack(100f, 50f);

            float result = DamageCalculator.CalculatePostureDamage(attack, DefenseState.PerfectDeflect, 1.5f);

            Assert.AreEqual(0f, result, 0.001f,
                "Perfect deflect: posture damage should be 0 regardless of multiplier");
        }

        #endregion
    }
}
