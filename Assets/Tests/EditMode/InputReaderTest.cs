using NUnit.Framework;
using Sekiro.Core.Input;
using UnityEngine;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// InputReader 输入抽象层 EditMode 单元测试。
    /// 使用 TestInputProvider 模拟玩家输入，验证 InputReader 的查询方法正确响应。
    /// </summary>
    [TestFixture]
    public class InputReaderTest
    {
        /// <summary>
        /// 测试用输入提供者，允许手动设置按键状态以驱动测试
        /// </summary>
        private class TestInputProvider : IInputProvider
        {
            private readonly bool[] _pressed = new bool[10];
            private readonly bool[] _held = new bool[10];
            private Vector2 _moveInput;

            /// <summary>
            /// 设置某个战斗输入在本帧是否被按下
            /// </summary>
            public void SetPressed(CombatInput input, bool value)
            {
                _pressed[(int)input] = value;
            }

            /// <summary>
            /// 设置某个战斗输入是否被按住
            /// </summary>
            public void SetHeld(CombatInput input, bool value)
            {
                _held[(int)input] = value;
            }

            /// <summary>
            /// 设置移动输入向量
            /// </summary>
            public void SetMoveInput(Vector2 value)
            {
                _moveInput = value;
            }

            /// <summary>
            /// 重置所有输入状态
            /// </summary>
            public void Reset()
            {
                for (int i = 0; i < 10; i++)
                {
                    _pressed[i] = false;
                    _held[i] = false;
                }
                _moveInput = Vector2.zero;
            }

            public bool IsPressed(CombatInput input) => _pressed[(int)input];
            public bool IsHeld(CombatInput input) => _held[(int)input];
            public Vector2 GetMoveInput() => _moveInput;
            public void Update() { }
        }

        private InputReader _inputReader;
        private TestInputProvider _provider;

        #region Setup

        /// <summary>
        /// 每个测试前创建新的 InputReader 和 TestInputProvider
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            _provider = new TestInputProvider();
            _inputReader = new InputReader(_provider);
        }

        #endregion

        #region Tests

        /// <summary>
        /// 测试用例 1：IsAttackPressed() 返回攻击键状态
        /// </summary>
        [Test]
        public void IsAttackPressed_ReturnsAttackKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.Attack, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsAttackPressed();

            // Assert
            Assert.IsTrue(result, "IsAttackPressed should return true when attack key is pressed");

            // Arrange - 松开攻击键
            _provider.SetPressed(CombatInput.Attack, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsAttackPressed();

            // Assert
            Assert.IsFalse(result, "IsAttackPressed should return false when attack key is not pressed");
        }

        /// <summary>
        /// 测试用例 2：IsDeflectPressed() 返回弹刀键状态
        /// </summary>
        [Test]
        public void IsDeflectPressed_ReturnsDeflectKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.Deflect, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsDeflectPressed();

            // Assert
            Assert.IsTrue(result, "IsDeflectPressed should return true when deflect key is pressed");

            // Arrange
            _provider.SetPressed(CombatInput.Deflect, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsDeflectPressed();

            // Assert
            Assert.IsFalse(result, "IsDeflectPressed should return false when deflect key is not pressed");
        }

        /// <summary>
        /// 测试用例 3：IsDeflectHeld() 返回弹刀是否按住
        /// </summary>
        [Test]
        public void IsDeflectHeld_ReturnsDeflectHeldState()
        {
            // Arrange - 按住弹刀键
            _provider.SetHeld(CombatInput.Deflect, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsDeflectHeld();

            // Assert
            Assert.IsTrue(result, "IsDeflectHeld should return true when deflect key is held");

            // Arrange - 松开弹刀键
            _provider.SetHeld(CombatInput.Deflect, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsDeflectHeld();

            // Assert
            Assert.IsFalse(result, "IsDeflectHeld should return false when deflect key is not held");
        }

        /// <summary>
        /// 测试用例 4：GetMoveInput() 返回 WASD 向量
        /// </summary>
        [Test]
        public void GetMoveInput_ReturnsWASDVector()
        {
            // Arrange - 向右前方移动
            Vector2 expectedInput = new Vector2(1f, 1f);
            _provider.SetMoveInput(expectedInput);

            // Act
            _inputReader.Update();
            Vector2 result = _inputReader.GetMoveInput();

            // Assert
            Assert.AreEqual(expectedInput.x, result.x, 0.001f,
                "GetMoveInput X should match input");
            Assert.AreEqual(expectedInput.y, result.y, 0.001f,
                "GetMoveInput Y should match input");

            // Arrange - 无移动输入
            _provider.SetMoveInput(Vector2.zero);

            // Act
            _inputReader.Update();
            result = _inputReader.GetMoveInput();

            // Assert
            Assert.AreEqual(Vector2.zero, result,
                "GetMoveInput should return zero when no movement input");
        }

        /// <summary>
        /// 测试用例 5：IsDodgePressed() 返回闪避键状态
        /// </summary>
        [Test]
        public void IsDodgePressed_ReturnsDodgeKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.Dodge, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsDodgePressed();

            // Assert
            Assert.IsTrue(result, "IsDodgePressed should return true when dodge key is pressed");

            // Arrange
            _provider.SetPressed(CombatInput.Dodge, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsDodgePressed();

            // Assert
            Assert.IsFalse(result, "IsDodgePressed should return false when dodge key is not pressed");
        }

        /// <summary>
        /// 测试用例 6：IsJumpPressed() 返回跳跃键状态
        /// </summary>
        [Test]
        public void IsJumpPressed_ReturnsJumpKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.Jump, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsJumpPressed();

            // Assert
            Assert.IsTrue(result, "IsJumpPressed should return true when jump key is pressed");

            // Arrange
            _provider.SetPressed(CombatInput.Jump, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsJumpPressed();

            // Assert
            Assert.IsFalse(result, "IsJumpPressed should return false when jump key is not pressed");
        }

        /// <summary>
        /// 测试用例 7：IsHealPressed() 返回回血键状态
        /// </summary>
        [Test]
        public void IsHealPressed_ReturnsHealKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.Heal, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsHealPressed();

            // Assert
            Assert.IsTrue(result, "IsHealPressed should return true when heal key is pressed");

            // Arrange
            _provider.SetPressed(CombatInput.Heal, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsHealPressed();

            // Assert
            Assert.IsFalse(result, "IsHealPressed should return false when heal key is not pressed");
        }

        /// <summary>
        /// 测试用例 8：IsLockOnPressed() 返回锁定键状态
        /// </summary>
        [Test]
        public void IsLockOnPressed_ReturnsLockOnKeyState()
        {
            // Arrange
            _provider.SetPressed(CombatInput.LockOn, true);

            // Act
            _inputReader.Update();
            bool result = _inputReader.IsLockOnPressed();

            // Assert
            Assert.IsTrue(result, "IsLockOnPressed should return true when lock-on key is pressed");

            // Arrange
            _provider.SetPressed(CombatInput.LockOn, false);

            // Act
            _inputReader.Update();
            result = _inputReader.IsLockOnPressed();

            // Assert
            Assert.IsFalse(result, "IsLockOnPressed should return false when lock-on key is not pressed");
        }

        #endregion
    }
}
