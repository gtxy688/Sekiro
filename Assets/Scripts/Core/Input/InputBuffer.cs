using System.Collections.Generic;

namespace Sekiro.Core.Input
{
    /// <summary>
    /// 输入缓冲系统，缓冲窗口内暂存未消费的输入。
    /// 高优先级输入覆盖低优先级。超过 150ms 的陈旧输入被丢弃。
    /// </summary>
    public class InputBuffer
    {
        #region Constants

        /// <summary>缓冲窗口时长（秒）</summary>
        private readonly float _bufferWindow = 0.15f;

        /// <summary>当前缓冲的输入项</summary>
        private readonly SortedSet<BufferedInput> _buffer = new SortedSet<BufferedInput>(
            Comparer<BufferedInput>.Create((a, b) =>
            {
                // 按优先级降序排列（高优先在前）
                int priorityCompare = b.input.CompareTo(a.input);
                if (priorityCompare != 0) return priorityCompare;
                return a.timestamp.CompareTo(b.timestamp);
            }));

        #endregion

        #region Types

        /// <summary>缓冲的输入项</summary>
        private struct BufferedInput
        {
            public CombatInput input;
            public float timestamp;
        }

        #endregion

        #region Properties

        /// <summary>缓冲队列中是否有输入</summary>
        public bool HasInput => _buffer.Count > 0;

        /// <summary>当前缓冲数量</summary>
        public int Count => _buffer.Count;

        #endregion

        #region Public API

        /// <summary>
        /// 将输入加入缓冲队列。如果已存在相同类型的输入则更新其时间戳。
        /// </summary>
        /// <param name="input">输入的按键枚举</param>
        /// <param name="currentTime">当前时间（秒），传入 Time.time 值</param>
        public void AddInput(CombatInput input, float currentTime)
        {
            CleanExpired(currentTime);

            // 替换已存在的同类型旧条目
            var existing = new List<BufferedInput>();
            foreach (var item in _buffer)
            {
                if (item.input == input)
                    existing.Add(item);
            }
            foreach (var item in existing)
                _buffer.Remove(item);

            _buffer.Add(new BufferedInput { input = input, timestamp = currentTime });
        }

        /// <summary>
        /// 取出最高优先级的输入并移除。无输入时返回 null。
        /// </summary>
        /// <returns>最高优先级的输入，缓冲为空则返回 null</returns>
        public CombatInput? GetNextInput(float currentTime)
        {
            CleanExpired(currentTime);
            if (_buffer.Count == 0) return null;

            var next = _buffer.Min;
            _buffer.Remove(next);
            return next.input;
        }

        /// <summary>
        /// 查看最高优先级输入但不移除。无输入时返回 null。
        /// </summary>
        public CombatInput? PeekNextInput(float currentTime)
        {
            CleanExpired(currentTime);
            if (_buffer.Count == 0) return null;

            return _buffer.Min.input;
        }

        /// <summary>
        /// 清空所有缓冲的输入。
        /// </summary>
        public void Clear()
        {
            _buffer.Clear();
        }

        #endregion

        #region Internal

        /// <summary>
        /// 移除超过缓冲窗口时长的陈旧输入。
        /// </summary>
        private void CleanExpired(float currentTime)
        {
            var expired = new List<BufferedInput>();
            foreach (var item in _buffer)
            {
                if (currentTime - item.timestamp > _bufferWindow)
                    expired.Add(item);
            }
            foreach (var item in expired)
                _buffer.Remove(item);
        }

        #endregion
    }
}
