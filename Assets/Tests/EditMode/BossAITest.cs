using NUnit.Framework;
using Sekiro.Boss.Attacks;
using Sekiro.Boss.AI;
using Sekiro.Core.Data;
using Sekiro.Combat;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// Boss AI 一阶段（弦一郎）EditMode 单元测试。
    /// 覆盖：AI 攻击选择、Boss 弹刀判定、AI 弹刀概率、招式数据、AI 控制器。
    /// </summary>
    [TestFixture]
    public class BossAITest
    {
        #region SelectAttack — 距离判断

        /// <summary>
        /// 近距离（1.5米）应选择近战招式（非远程）
        /// </summary>
        [Test]
        public void SelectAttack_CloseDistance_ReturnsMeleeAttack()
        {
            var playerState = new PlayerStateInfo { distanceToBoss = 1.5f };
            var attack = BossAIController.SelectAttack(playerState, 42);
            Assert.IsNotNull(attack, "Should select an attack at close range");
            Assert.IsFalse(attack.isRanged, "Close range should not select ranged attack");
        }

        /// <summary>
        /// 远距离（5米）应选择远程射箭
        /// </summary>
        [Test]
        public void SelectAttack_FarDistance_ReturnsRangedAttack()
        {
            var playerState = new PlayerStateInfo { distanceToBoss = 5f };
            var attack = BossAIController.SelectAttack(playerState, 42);
            Assert.IsNotNull(attack, "Should select an attack at far range");
            Assert.IsTrue(attack.isRanged, "Far range should select ranged (arrow)");
        }

        /// <summary>
        /// 中距离（3米）应尊重权重，不选择超距招式
        /// </summary>
        [Test]
        public void SelectAttack_MediumDistance_RespectsWeights()
        {
            var playerState = new PlayerStateInfo { distanceToBoss = 3f };
            var attack = BossAIController.SelectAttack(playerState, 42);
            Assert.IsNotNull(attack, "Should select an attack at medium range");
            Assert.IsFalse(attack.isRanged, "Medium range should not use ranged");
            Assert.AreNotEqual("下劈", attack.attackName, "下劈 requires < 2m");
            Assert.AreNotEqual("扫击", attack.attackName, "扫击 requires < 2m");
        }

        /// <summary>
        /// 玩家连续攻击 3 次以上时，近距离也可能选择后撤射箭
        /// </summary>
        [Test]
        public void SelectAttack_PlayerComboing_CanSelectBackstepArrow()
        {
            var playerState = new PlayerStateInfo
            {
                distanceToBoss = 2f,
                playerComboCount = 4
            };
            bool foundRanged = false;
            for (int seed = 0; seed < 100; seed++)
            {
                var attack = BossAIController.SelectAttack(playerState, seed);
                if (attack != null && attack.isRanged)
                {
                    foundRanged = true;
                    break;
                }
            }
            Assert.IsTrue(foundRanged,
                "When player is comboing, backstep+arrow should be available even at close range");
        }

        /// <summary>
        /// 中距离（3米）不应选择距离限制 2.5 米的三连斩
        /// </summary>
        [Test]
        public void SelectAttack_MediumDistance_ExcludesOutOfRangeAttacks()
        {
            var playerState = new PlayerStateInfo { distanceToBoss = 3f };
            for (int seed = 0; seed < 50; seed++)
            {
                var attack = BossAIController.SelectAttack(playerState, seed);
                Assert.AreNotEqual("三连斩", attack.attackName,
                    "三连斩 requires < 2.5m, should not appear at 3m");
            }
        }

        #endregion

        #region Boss 弹刀判定 — TryDeflect

        /// <summary>
        /// 弹刀窗口内正面攻击应返回 PerfectDeflect
        /// </summary>
        [Test]
        public void BossDeflect_InWindow_ReturnsPerfectDeflect()
        {
            var config = CreateDefaultConfig();
            var result = BossDeflectSystem.TryDeflect(
                CreateDeflectableAttack(),
                UnityEngine.Vector3.forward,
                UnityEngine.Vector3.forward,
                true, true, config);
            Assert.AreEqual(DeflectResult.PerfectDeflect, result);
        }

        /// <summary>
        /// 超出弹刀窗口（仅格挡状态）应返回 NormalBlock
        /// </summary>
        [Test]
        public void BossDeflect_OutsideWindow_ReturnsNormalBlock()
        {
            var config = CreateDefaultConfig();
            var result = BossDeflectSystem.TryDeflect(
                CreateDeflectableAttack(),
                UnityEngine.Vector3.forward,
                UnityEngine.Vector3.forward,
                false, true, config);
            Assert.AreEqual(DeflectResult.NormalBlock, result);
        }

        /// <summary>
        /// 未在弹刀/格挡姿态应返回 None
        /// </summary>
        [Test]
        public void BossDeflect_NotInStance_ReturnsNone()
        {
            var config = CreateDefaultConfig();
            var result = BossDeflectSystem.TryDeflect(
                CreateDeflectableAttack(),
                UnityEngine.Vector3.forward,
                UnityEngine.Vector3.forward,
                false, false, config);
            Assert.AreEqual(DeflectResult.None, result);
        }

        /// <summary>
        /// 来自背后的攻击无法弹刀
        /// </summary>
        [Test]
        public void BossDeflect_WrongAngle_ReturnsNone()
        {
            var config = CreateDefaultConfig();
            var result = BossDeflectSystem.TryDeflect(
                CreateDeflectableAttack(),
                UnityEngine.Vector3.back,
                UnityEngine.Vector3.forward,
                true, true, config);
            Assert.AreEqual(DeflectResult.None, result);
        }

        #endregion

        #region AI 弹刀概率 — ShouldEnterDeflectStance

        /// <summary>
        /// 满血时弹刀概率应为 40%
        /// </summary>
        [Test]
        public void ShouldEnterDeflectStance_FullHealth_40Percent()
        {
            int count = CountDeflects(1.0f, 0, 1000);
            Assert.That(count, Is.InRange(300, 500),
                $"Expected ~400/1000, got {count}");
        }

        /// <summary>
        /// 玩家连招 2 次以上时弹刀概率提升至 60%
        /// </summary>
        [Test]
        public void ShouldEnterDeflectStance_PlayerCombo_60Percent()
        {
            int count = CountDeflects(1.0f, 3, 1000);
            Assert.That(count, Is.InRange(500, 700),
                $"Expected ~600/1000, got {count}");
        }

        /// <summary>
        /// Boss 低血量（&lt; 30%）时弹刀概率降至 25%
        /// </summary>
        [Test]
        public void ShouldEnterDeflectStance_LowHealth_25Percent()
        {
            int count = CountDeflects(0.2f, 0, 1000);
            Assert.That(count, Is.InRange(150, 350),
                $"Expected ~250/1000, got {count}");
        }

        /// <summary>
        /// 辅助方法：统计 N 次判定中弹刀触发的次数
        /// </summary>
        private int CountDeflects(float healthPercent, int comboCount, int trials)
        {
            int count = 0;
            for (int i = 0; i < trials; i++)
            {
                if (BossDeflectSystem.ShouldEnterDeflectStance(healthPercent, comboCount, i))
                    count++;
            }
            return count;
        }

        #endregion

        #region BossAttackData — 招式数据验证

        /// <summary>
        /// 一阶段应恰好 9 种招式
        /// </summary>
        [Test]
        public void GetPhase1Attacks_Count_Is9()
        {
            var attacks = BossAttackData.GetPhase1Attacks();
            Assert.AreEqual(9, attacks.Length);
        }

        /// <summary>
        /// 横斩数据应符合规格表
        /// </summary>
        [Test]
        public void CreateHorizontalSlash_DataCorrect()
        {
            var a = BossAttackData.CreateHorizontalSlash();
            Assert.AreEqual("横斩", a.attackName);
            Assert.AreEqual(100f, a.damage);
            Assert.AreEqual(15f, a.postureDamage);
            Assert.AreEqual(10f, a.startupFrames);
            Assert.AreEqual(4f, a.activeFrames);
            Assert.AreEqual(12f, a.recoveryFrames);
            Assert.IsTrue(a.canBeDeflected);
            Assert.AreEqual(AttackType.Normal, a.attackType);
        }

        /// <summary>
        /// 突刺应为不可弹刀的突刺危字
        /// </summary>
        [Test]
        public void CreateThrust_IsNotDeflectable_IsThrust()
        {
            var a = BossAttackData.CreateThrust();
            Assert.AreEqual("突刺", a.attackName);
            Assert.IsFalse(a.canBeDeflected, "突刺 should not be deflectable");
            Assert.AreEqual(AttackType.Thrust, a.attackType);
            Assert.AreEqual(150f, a.damage);
            Assert.AreEqual(25f, a.postureDamage);
        }

        /// <summary>
        /// 扫击应为不可弹刀的扫击危字
        /// </summary>
        [Test]
        public void CreateSweep_IsNotDeflectable_IsSweep()
        {
            var a = BossAttackData.CreateSweep();
            Assert.AreEqual("扫击", a.attackName);
            Assert.IsFalse(a.canBeDeflected, "扫击 should not be deflectable");
            Assert.AreEqual(AttackType.Sweep, a.attackType);
        }

        /// <summary>
        /// 射箭应为远程攻击
        /// </summary>
        [Test]
        public void CreateArrow_IsRanged()
        {
            var a = BossAttackData.CreateArrow();
            Assert.AreEqual("射箭", a.attackName);
            Assert.IsTrue(a.isRanged);
            Assert.AreEqual(60f, a.damage);
        }

        /// <summary>
        /// 后撤步无伤害
        /// </summary>
        [Test]
        public void CreateBackstep_ZeroDamage()
        {
            var a = BossAttackData.CreateBackstep();
            Assert.AreEqual("后撤步", a.attackName);
            Assert.AreEqual(0f, a.damage);
            Assert.AreEqual(0f, a.postureDamage);
        }

        #endregion

        #region BossAIController — 构造与状态

        /// <summary>
        /// 初始状态应为 Idle
        /// </summary>
        [Test]
        public void BossAIController_InitialState_IsIdle()
        {
            var controller = new BossAIController();
            Assert.AreEqual(BossAIState.Idle, controller.CurrentState);
        }

        /// <summary>
        /// 硬直状态应设置正确时长
        /// </summary>
        [Test]
        public void TransitionToStagger_SetsCorrectDuration()
        {
            var controller = new BossAIController();
            controller.TransitionToStaggerState(0.3f);
            Assert.AreEqual(BossAIState.Stagger, controller.CurrentState);
            Assert.AreEqual(0.3f, controller.StaggerDuration, 0.001f);
        }

        /// <summary>
        /// 反击状态标记应正确设置
        /// </summary>
        [Test]
        public void TransitionToCounterAttack_SetsFlag()
        {
            var controller = new BossAIController();
            controller.TransitionToCounterAttackState();
            Assert.IsTrue(controller.IsCounterAttackPending);
            Assert.AreEqual(BossAIState.Attack, controller.CurrentState);
        }

        #endregion

        #region Helpers

        private DeflectConfig CreateDefaultConfig()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<DeflectConfig>();
            config.bossDeflectWindow = 0.15f;
            config.bossBlockWindow = 0.3f;
            config.bossStanceDuration = 0.4f;
            config.deflectChanceOnAttack = 0.4f;
            config.deflectChanceInCombo = 0.6f;
            config.deflectChanceLowHealth = 0.25f;
            return config;
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
    }
}
