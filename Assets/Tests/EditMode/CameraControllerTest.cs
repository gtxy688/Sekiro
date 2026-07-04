using NUnit.Framework;
using Sekiro.Camera;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// CameraController 纯逻辑部分的 EditMode 单元测试。
    /// 测试不依赖 Unity 组件的静态方法，验证相机位置计算、角度限制、朝向和跟随逻辑。
    /// </summary>
    [TestFixture]
    public class CameraControllerTest
    {
        private const float Tolerance = 0.001f;

        #region CalculateFollowPosition — 跟随位置计算

        /// <summary>
        /// 零旋转时，相机应在目标正后方、指定距离和高度处
        /// </summary>
        [Test]
        public void CalculateFollowPosition_ZeroRotation_BehindTarget()
        {
            // Arrange
            Vector3 targetPos = Vector3.zero;
            float distance = 5f;
            float height = 2f;
            float yaw = 0f;
            float pitch = 0f;

            // Act
            Vector3 result = CameraController.CalculateFollowPosition(
                targetPos, distance, height, yaw, pitch);

            // Assert — 相机在目标正后方（-Z 方向），高度为 height
            Assert.AreEqual(0f, result.x, Tolerance,
                "Camera X should be 0 when yaw is 0");
            Assert.AreEqual(height, result.y, Tolerance,
                "Camera Y should equal height offset");
            Assert.AreEqual(-distance, result.z, Tolerance,
                "Camera Z should be -distance when yaw is 0 (behind target)");
        }

        /// <summary>
        /// 90 度水平旋转时，相机应在目标左侧（-X 方向）
        /// </summary>
        [Test]
        public void CalculateFollowPosition_Yaw90_LeftSide()
        {
            // Arrange
            Vector3 targetPos = Vector3.zero;
            float distance = 5f;
            float height = 2f;
            float yaw = 90f;
            float pitch = 0f;

            // Act
            Vector3 result = CameraController.CalculateFollowPosition(
                targetPos, distance, height, yaw, pitch);

            // Assert — 旋转 90 度后，相机在目标左侧
            Assert.AreEqual(-distance, result.x, Tolerance,
                "Camera X should be -distance when yaw is 90");
            Assert.AreEqual(height, result.y, Tolerance,
                "Camera Y should equal height offset");
            Assert.AreEqual(0f, result.z, Tolerance,
                "Camera Z should be ~0 when yaw is 90");
        }

        /// <summary>
        /// 非原点目标位置应正确叠加偏移
        /// </summary>
        [Test]
        public void CalculateFollowPosition_WithOffset_AppliesTargetPosition()
        {
            // Arrange
            Vector3 targetPos = new Vector3(10f, 5f, 20f);
            float distance = 5f;
            float height = 2f;
            float yaw = 0f;
            float pitch = 0f;

            // Act
            Vector3 result = CameraController.CalculateFollowPosition(
                targetPos, distance, height, yaw, pitch);

            // Assert
            Assert.AreEqual(targetPos.x, result.x, Tolerance,
                "Camera X should be offset by target X");
            Assert.AreEqual(targetPos.y + height, result.y, Tolerance,
                "Camera Y should be target Y + height");
            Assert.AreEqual(targetPos.z - distance, result.z, Tolerance,
                "Camera Z should be target Z - distance");
        }

        /// <summary>
        /// 俯仰角应影响相机高度和水平距离的分配
        /// </summary>
        [Test]
        public void CalculateFollowPosition_WithPitch_AdjustsHeightAndDistance()
        {
            // Arrange
            Vector3 targetPos = Vector3.zero;
            float distance = 5f;
            float height = 2f;
            float yaw = 0f;
            float pitch = 45f;

            // Act
            Vector3 result = CameraController.CalculateFollowPosition(
                targetPos, distance, height, yaw, pitch);

            // Assert — 45 度俯仰使部分距离转为垂直偏移
            float pitchRad = pitch * Mathf.Deg2Rad;
            float expectedY = height + distance * Mathf.Sin(pitchRad);
            float expectedZ = -(distance * Mathf.Cos(pitchRad));
            Assert.AreEqual(expectedY, result.y, Tolerance,
                "Camera Y should increase with positive pitch");
            Assert.AreEqual(expectedZ, result.z, Tolerance,
                "Camera Z distance should decrease with positive pitch");
        }

        #endregion

        #region ClampVerticalAngle — 垂直角度限制

        /// <summary>
        /// 范围内的角度不应被修改
        /// </summary>
        [Test]
        public void ClampVerticalAngle_WithinRange_ReturnsUnchanged()
        {
            // Act
            float result = CameraController.ClampVerticalAngle(30f, -20f, 60f);

            // Assert
            Assert.AreEqual(30f, result, Tolerance,
                "Angle within range should not be modified");
        }

        /// <summary>
        /// 低于最小值的角度应被钳制到最小值
        /// </summary>
        [Test]
        public void ClampVerticalAngle_BelowMin_ClampedToMin()
        {
            // Act
            float result = CameraController.ClampVerticalAngle(-30f, -20f, 60f);

            // Assert
            Assert.AreEqual(-20f, result, Tolerance,
                "Angle below minimum should be clamped to minimum");
        }

        /// <summary>
        /// 超过最大值的角度应被钳制到最大值
        /// </summary>
        [Test]
        public void ClampVerticalAngle_AboveMax_ClampedToMax()
        {
            // Act
            float result = CameraController.ClampVerticalAngle(90f, -20f, 60f);

            // Assert
            Assert.AreEqual(60f, result, Tolerance,
                "Angle above maximum should be clamped to maximum");
        }

        /// <summary>
        /// 恰好等于边界值的角度不应被修改
        /// </summary>
        [Test]
        public void ClampVerticalAngle_AtBoundary_ReturnsUnchanged()
        {
            // Assert — 最小边界
            Assert.AreEqual(-20f,
                CameraController.ClampVerticalAngle(-20f, -20f, 60f), Tolerance,
                "Angle at minimum boundary should remain unchanged");
            // Assert — 最大边界
            Assert.AreEqual(60f,
                CameraController.ClampVerticalAngle(60f, -20f, 60f), Tolerance,
                "Angle at maximum boundary should remain unchanged");
        }

        #endregion

        #region CalculateLookAtRotation — 朝向目标旋转

        /// <summary>
        /// 目标在正前方时应返回 LookRotation(forward) 的旋转
        /// </summary>
        [Test]
        public void CalculateLookAtRotation_TargetAhead_FacesForward()
        {
            // Arrange
            Vector3 cameraPos = new Vector3(0f, 2f, -5f);
            Vector3 targetPos = new Vector3(0f, 2f, 5f);

            // Act
            Quaternion result = CameraController.CalculateLookAtRotation(cameraPos, targetPos);

            // Assert
            Quaternion expected = Quaternion.LookRotation(Vector3.forward);
            Assert.AreEqual(expected.x, result.x, Tolerance);
            Assert.AreEqual(expected.y, result.y, Tolerance);
            Assert.AreEqual(expected.z, result.z, Tolerance);
            Assert.AreEqual(expected.w, result.w, Tolerance);
        }

        /// <summary>
        /// 目标在右侧时应正确向右旋转
        /// </summary>
        [Test]
        public void CalculateLookAtRotation_TargetRight_FacesRight()
        {
            // Arrange
            Vector3 cameraPos = Vector3.zero;
            Vector3 targetPos = new Vector3(10f, 0f, 0f);

            // Act
            Quaternion result = CameraController.CalculateLookAtRotation(cameraPos, targetPos);

            // Assert
            Quaternion expected = Quaternion.LookRotation(Vector3.right);
            Assert.AreEqual(expected.x, result.x, Tolerance);
            Assert.AreEqual(expected.y, result.y, Tolerance);
            Assert.AreEqual(expected.z, result.z, Tolerance);
            Assert.AreEqual(expected.w, result.w, Tolerance);
        }

        /// <summary>
        /// 不同高度的目标应产生包含垂直分量的旋转
        /// </summary>
        [Test]
        public void CalculateLookAtRotation_TargetHigher_LooksUp()
        {
            // Arrange
            Vector3 cameraPos = Vector3.zero;
            Vector3 targetPos = new Vector3(0f, 5f, 5f);

            // Act
            Quaternion result = CameraController.CalculateLookAtRotation(cameraPos, targetPos);
            Vector3 forward = result * Vector3.forward;

            // Assert — forward 方向应有正的 Y 分量（向上看）
            Assert.Greater(forward.y, 0f,
                "Looking at a higher target should produce upward look direction");
        }

        #endregion

        #region SmoothFollow — 平滑跟随

        /// <summary>
        /// 插值结果应介于当前位置和目标位置之间
        /// </summary>
        [Test]
        public void SmoothFollow_NormalLerp_ResultBetweenCurrentAndTarget()
        {
            // Arrange
            Vector3 current = Vector3.zero;
            Vector3 target = new Vector3(10f, 0f, 0f);
            float smooth = 5f;
            float deltaTime = 0.02f;

            // Act
            Vector3 result = CameraController.SmoothFollow(current, target, smooth, deltaTime);

            // Assert — 结果应在 current 和 target 之间
            Assert.Greater(result.x, current.x,
                "Result should move toward target");
            Assert.Less(result.x, target.x,
                "Result should not overshoot target");
        }

        /// <summary>
        /// deltaTime 为 0 时应返回当前位置（无移动）
        /// </summary>
        [Test]
        public void SmoothFollow_ZeroDeltaTime_ReturnsCurrent()
        {
            // Arrange
            Vector3 current = new Vector3(1f, 2f, 3f);
            Vector3 target = new Vector3(10f, 20f, 30f);

            // Act
            Vector3 result = CameraController.SmoothFollow(current, target, 5f, 0f);

            // Assert
            Assert.AreEqual(current.x, result.x, Tolerance);
            Assert.AreEqual(current.y, result.y, Tolerance);
            Assert.AreEqual(current.z, result.z, Tolerance);
        }

        /// <summary>
        /// 较大的 deltaTime 应产生更接近目标的插值结果
        /// </summary>
        [Test]
        public void SmoothFollow_LargeDeltaTime_CloserToTarget()
        {
            // Arrange
            Vector3 current = Vector3.zero;
            Vector3 target = new Vector3(10f, 0f, 0f);
            float smooth = 5f;

            // Act — 小 dt vs 大 dt
            Vector3 smallDt = CameraController.SmoothFollow(current, target, smooth, 0.02f);
            Vector3 largeDt = CameraController.SmoothFollow(current, target, smooth, 0.1f);

            // Assert — 大 dt 应更接近 target
            Assert.Greater(largeDt.x, smallDt.x,
                "Larger deltaTime should produce result closer to target");
        }

        /// <summary>
        /// 当前位置等于目标位置时应返回相同位置
        /// </summary>
        [Test]
        public void SmoothFollow_AtTarget_ReturnsTarget()
        {
            // Arrange
            Vector3 pos = new Vector3(5f, 3f, 7f);

            // Act
            Vector3 result = CameraController.SmoothFollow(pos, pos, 5f, 0.02f);

            // Assert
            Assert.AreEqual(pos.x, result.x, Tolerance);
            Assert.AreEqual(pos.y, result.y, Tolerance);
            Assert.AreEqual(pos.z, result.z, Tolerance);
        }

        #endregion
    }
}
