using NUnit.Framework;
using Sekiro.Core.Input;

namespace Sekiro.Tests.EditMode
{
    /// <summary>
    /// InputBuffer 输入缓冲系统 EditMode 单元测试。
    /// 测试缓冲窗口、优先级排序、超时清除等核心逻辑。
    /// </summary>
    [TestFixture]
    public class InputBufferTest
    {
        private InputBuffer _buffer;
        private float _currentTime;

        [SetUp]
        public void SetUp()
        {
            _buffer = new InputBuffer();
            _currentTime = 0f;
        }

        #region 基础功能

        /// <summary>
        /// 添加输入后队列应有内容
        /// </summary>
        [Test]
        public void AddInput_SingleInput_HasInput()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);
            Assert.IsTrue(_buffer.HasInput);
            Assert.AreEqual(1, _buffer.Count);
        }

        /// <summary>
        /// 取出输入应按添加顺序返回
        /// </summary>
        [Test]
        public void GetNextInput_SingleInput_ReturnsSameInput()
        {
            _buffer.AddInput(CombatInput.Deflect, _currentTime);
            var result = _buffer.GetNextInput(_currentTime);

            Assert.AreEqual(CombatInput.Deflect, result);
            Assert.IsFalse(_buffer.HasInput);
        }

        /// <summary>
        /// 空队列应返回 null
        /// </summary>
        [Test]
        public void GetNextInput_Empty_ReturnsNull()
        {
            var result = _buffer.GetNextInput(_currentTime);
            Assert.IsNull(result);
        }

        /// <summary>
        /// Peek 不应移除输入
        /// </summary>
        [Test]
        public void PeekNextInput_DoesNotRemove()
        {
            _buffer.AddInput(CombatInput.Jump, _currentTime);

            var peeked = _buffer.PeekNextInput(_currentTime);
            Assert.AreEqual(CombatInput.Jump, peeked);
            Assert.AreEqual(1, _buffer.Count);
        }

        #endregion

        #region 优先级

        /// <summary>
        /// 高优先级输入应优先于低优先级输出
        /// </summary>
        [Test]
        public void GetNextInput_HigherPriorityFirst()
        {
            // 先添加低优先级，再添加高优先级
            _buffer.AddInput(CombatInput.Move, _currentTime);
            _buffer.AddInput(CombatInput.Deflect, _currentTime + 0.01f);

            // 输出顺序：Deflect（优先级高）→ Move（优先级低）
            Assert.AreEqual(CombatInput.Deflect, _buffer.GetNextInput(_currentTime + 0.02f));
            Assert.AreEqual(CombatInput.Move, _buffer.GetNextInput(_currentTime + 0.02f));
        }

        /// <summary>
        /// Deathblow 是最高优先级输入
        /// </summary>
        [Test]
        public void GetNextInput_DeathblowHighestPriority()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);
            _buffer.AddInput(CombatInput.Heal, _currentTime + 0.01f);
            _buffer.AddInput(CombatInput.Deathblow, _currentTime + 0.02f);

            // 应始终返回 Deathblow
            Assert.AreEqual(CombatInput.Deathblow, _buffer.GetNextInput(_currentTime + 0.03f));
        }

        #endregion

        #region 超时清除

        /// <summary>
        /// 超过缓冲窗口的陈旧输入应被自动清除
        /// </summary>
        [Test]
        public void GetNextInput_ExpiredInput_Cleaned()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);

            // 时间前进超过 150ms 窗口
            _currentTime += 0.2f;

            Assert.IsFalse(_buffer.HasInput, "过期输入应被清除");
            Assert.IsNull(_buffer.GetNextInput(_currentTime));
        }

        /// <summary>
        /// 部分过期时只清除过期输入
        /// </summary>
        [Test]
        public void GetNextInput_PartialExpiry_KeepsFresh()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);
            _currentTime += 0.1f;
            _buffer.AddInput(CombatInput.Deflect, _currentTime);

            // 总时间 0.2s，attack 在 0s 添加 → 过期，deflect 在 0.1s → 还在窗口内
            _currentTime += 0.1f;

            var result = _buffer.GetNextInput(_currentTime);
            Assert.AreEqual(CombatInput.Deflect, result, "只有较新的输入应保留");
            Assert.IsNull(_buffer.GetNextInput(_currentTime), "所有有效输入取出后应为空");
        }

        #endregion

        #region 清空

        /// <summary>
        /// Clear 应移除所有输入
        /// </summary>
        [Test]
        public void Clear_RemovesAll()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);
            _buffer.AddInput(CombatInput.Deflect, _currentTime + 0.01f);
            _buffer.AddInput(CombatInput.Dodge, _currentTime + 0.02f);

            _buffer.Clear();

            Assert.IsFalse(_buffer.HasInput);
            Assert.AreEqual(0, _buffer.Count);
        }

        #endregion

        #region 更新已有输入

        /// <summary>
        /// 添加已有同类型输入应更新其时间戳
        /// </summary>
        [Test]
        public void AddInput_SameType_UpdatesTimestamp()
        {
            _buffer.AddInput(CombatInput.Attack, _currentTime);

            // 时间前进后再次添加相同输入
            _currentTime += 0.05f;
            _buffer.AddInput(CombatInput.Attack, _currentTime);

            // 时间再前进 0.1s → 总计 0.15s，旧 attack 过期但新 attack 还在窗口内
            _currentTime += 0.1f;
            Assert.IsTrue(_buffer.HasInput, "时间戳更新后输入应仍在窗口内");
        }

        #endregion
    }
}
