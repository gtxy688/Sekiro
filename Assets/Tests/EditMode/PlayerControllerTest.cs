using NUnit.Framework;
using Sekiro.Player.Movement;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// PlayerController 纯逻辑部分的 EditMode 单元测试。
    /// 测试不依赖 Unity 组件的静态方法，验证移动归一化、跳跃限制和朝向计算。
    /// </summary>
    [TestFixture]
    public class PlayerControllerTest
    {
        private const float Tolerance = 0.001f;

        #region NormalizeInput — 移动向量归一化

        /// <summary>
        /// 斜向输入（对角线）超过单位圆时应被归一化，防止斜向移动更快
        /// </summary>
        [Test]
        public void NormalizeInput_DiagonalInput_ReturnsNormalizedVector()
        {
            // Arrange — 右上方 45 度，原始长度 > 1
            Vector2 diagonalInput = new Vector2(1f, 1f);

            // Act
            Vector2 result = PlayerController.NormalizeInput(diagonalInput);

            // Assert — 归一化后长度应为 1
            Assert.AreEqual(1f, result.magnitude, Tolerance,
                "Diagonal input should be normalized to magnitude 1");
            // 方向不变
            float expectedX = 1f / Mathf.Sqrt(2f);
            Assert.AreEqual(expectedX, result.x, Tolerance,
                "Normalized X component should be 1/sqrt(2)");
            Assert.AreEqual(expectedX, result.y, Tolerance,
                "Normalized Y component should be 1/sqrt(2)");
        }

        /// <summary>
        /// 长度不超过 1 的输入不应被修改
        /// </summary>
        [Test]
        public void NormalizeInput_SmallInput_ReturnsUnchanged()
        {
            // Arrange
            Vector2 smallInput = new Vector2(0.5f, 0.3f);
            Assert.Less(smallInput.magnitude, 1f, "Test precondition: input magnitude < 1");

            // Act
            Vector2 result = PlayerController.NormalizeInput(smallInput);

            // Assert — 不修改
            Assert.AreEqual(smallInput.x, result.x, Tolerance,
                "Small input X should not be modified");
            Assert.AreEqual(smallInput.y, result.y, Tolerance,
                "Small input Y should not be modified");
        }

        /// <summary>
        /// 零向量应返回零向量
        /// </summary>
        [Test]
        public void NormalizeInput_ZeroInput_ReturnsZero()
        {
            // Act
            Vector2 result = PlayerController.NormalizeInput(Vector2.zero);

            // Assert
            Assert.AreEqual(Vector2.zero, result,
                "Zero input should return zero vector");
        }

        /// <summary>
        /// 单轴满输入（长度为 1）不应被修改
        /// </summary>
        [Test]
        public void NormalizeInput_UnitInput_ReturnsUnchanged()
        {
            // Arrange
            Vector2 unitInput = new Vector2(1f, 0f);

            // Act
            Vector2 result = PlayerController.NormalizeInput(unitInput);

            // Assert — magnitude == 1, not > 1, so no normalization
            Assert.AreEqual(1f, result.x, Tolerance);
            Assert.AreEqual(0f, result.y, Tolerance);
        }

        #endregion

        #region CanJump — 跳跃限制

        /// <summary>
        /// 在地面上且未跳跃时应允许跳跃
        /// </summary>
        [Test]
        public void CanJump_GroundedAndNotJumped_ReturnsTrue()
        {
            // Act
            bool result = PlayerController.CanJump(isGrounded: true, hasJumped: false);

            // Assert
            Assert.IsTrue(result, "Should be able to jump when grounded and not yet jumped");
        }

        /// <summary>
        /// 在空中时不应允许跳跃（一段跳限制）
        /// </summary>
        [Test]
        public void CanJump_Airborne_ReturnsFalse()
        {
            // Act
            bool result = PlayerController.CanJump(isGrounded: false, hasJumped: false);

            // Assert
            Assert.IsFalse(result, "Should not be able to jump when airborne");
        }

        /// <summary>
        /// 在地面上但已跳跃过时不应允许再次跳跃（一段跳限制）
        /// </summary>
        [Test]
        public void CanJump_GroundedButAlreadyJumped_ReturnsFalse()
        {
            // Act
            bool result = PlayerController.CanJump(isGrounded: true, hasJumped: true);

            // Assert
            Assert.IsFalse(result,
                "Should not be able to jump again after already jumping (single jump restriction)");
        }

        /// <summary>
        /// 在空中且已跳跃过时不应允许跳跃
        /// </summary>
        [Test]
        public void CanJump_AirborneAndAlreadyJumped_ReturnsFalse()
        {
            // Act
            bool result = PlayerController.CanJump(isGrounded: false, hasJumped: true);

            // Assert
            Assert.IsFalse(result,
                "Should not be able to jump when airborne and already jumped");
        }

        /// <summary>
        /// 落地后（hasJumped 被外部重置为 false）应恢复跳跃能力
        /// </summary>
        [Test]
        public void CanJump_AfterLandingReset_ReturnsTrue()
        {
            // Arrange — 模拟跳跃后落地，hasJumped 被重置
            bool hasJumped = true; // 跳跃中
            // 落地时控制器将 hasJumped 设为 false
            hasJumped = false;

            // Act
            bool result = PlayerController.CanJump(isGrounded: true, hasJumped: hasJumped);

            // Assert
            Assert.IsTrue(result,
                "Should be able to jump again after landing resets jump state");
        }

        #endregion

        #region CalculateTargetRotation — 目标朝向计算

        /// <summary>
        /// 非锁定模式下，朝向应与移动方向一致
        /// </summary>
        [Test]
        public void CalculateTargetRotation_NoLockOn_FacesMoveDirection()
        {
            // Arrange — 向正前方移动
            Vector3 moveDirection = Vector3.forward;

            // Act
            Quaternion result = PlayerController.CalculateTargetRotation(
                moveDirection,
                lockOnTarget: null,
                currentPosition: Vector3.zero);

            // Assert — 应面朝正前方
            Quaternion expected = Quaternion.LookRotation(Vector3.forward);
            Assert.AreEqual(expected.x, result.x, Tolerance);
            Assert.AreEqual(expected.y, result.y, Tolerance);
            Assert.AreEqual(expected.z, result.z, Tolerance);
            Assert.AreEqual(expected.w, result.w, Tolerance);
        }

        /// <summary>
        /// 锁定模式下，应面朝锁定目标方向，忽略移动方向
        /// </summary>
        [Test]
        public void CalculateTargetRotation_WithLockOn_FacesTarget()
        {
            // Arrange — 角色在原点，目标在右侧，但移动方向是前方
            Vector3 moveDirection = Vector3.forward;
            Vector3 targetPosition = new Vector3(10f, 0f, 0f);
            Vector3 playerPosition = Vector3.zero;

            // Act
            Quaternion result = PlayerController.CalculateTargetRotation(
                moveDirection,
                lockOnTarget: targetPosition,
                currentPosition: playerPosition);

            // Assert — 应面朝右方（锁定目标方向）
            Quaternion expected = Quaternion.LookRotation(Vector3.right);
            Assert.AreEqual(expected.x, result.x, Tolerance);
            Assert.AreEqual(expected.y, result.y, Tolerance);
            Assert.AreEqual(expected.z, result.z, Tolerance);
            Assert.AreEqual(expected.w, result.w, Tolerance);
        }

        /// <summary>
        /// 锁定目标在不同高度时，朝向应忽略 Y 轴差异（水平朝向）
        /// </summary>
        [Test]
        public void CalculateTargetRotation_LockOnDifferentHeight_IgnoresYAxis()
        {
            // Arrange — 目标在前方且高处
            Vector3 targetPosition = new Vector3(0f, 10f, 5f);
            Vector3 playerPosition = Vector3.zero;

            // Act
            Quaternion result = PlayerController.CalculateTargetRotation(
                Vector3.zero,
                lockOnTarget: targetPosition,
                currentPosition: playerPosition);

            // Assert — 水平方向应为正前方，Y 被忽略
            Vector3 forwardDir = result * Vector3.forward;
            Assert.AreEqual(0f, forwardDir.y, Tolerance,
                "Look direction should be horizontal (Y should be 0)");
        }

        /// <summary>
        /// 无移动输入且无锁定目标时，应返回单位四元数（不旋转）
        /// </summary>
        [Test]
        public void CalculateTargetRotation_NoMoveNoLockOn_ReturnsIdentity()
        {
            // Act
            Quaternion result = PlayerController.CalculateTargetRotation(
                Vector3.zero,
                lockOnTarget: null,
                currentPosition: Vector3.zero);

            // Assert
            Assert.AreEqual(Quaternion.identity.x, result.x, Tolerance);
            Assert.AreEqual(Quaternion.identity.y, result.y, Tolerance);
            Assert.AreEqual(Quaternion.identity.z, result.z, Tolerance);
            Assert.AreEqual(Quaternion.identity.w, result.w, Tolerance);
        }

        /// <summary>
        /// 锁定目标在身后时应正确旋转 180 度
        /// </summary>
        [Test]
        public void CalculateTargetRotation_TargetBehind_FacesBackward()
        {
            // Arrange — 目标在身后
            Vector3 targetPosition = new Vector3(0f, 0f, -5f);

            // Act
            Quaternion result = PlayerController.CalculateTargetRotation(
                Vector3.forward,
                lockOnTarget: targetPosition,
                currentPosition: Vector3.zero);

            // Assert — 应面朝后方
            Quaternion expected = Quaternion.LookRotation(Vector3.back);
            Assert.AreEqual(expected.x, result.x, Tolerance);
            Assert.AreEqual(expected.y, result.y, Tolerance);
            Assert.AreEqual(expected.z, result.z, Tolerance);
            Assert.AreEqual(expected.w, result.w, Tolerance);
        }

        #endregion
    }
}
