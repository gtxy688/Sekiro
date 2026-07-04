using System;
using UnityEngine;
using Sekiro.Core.Data;
using Sekiro.Core.Events;

namespace Sekiro.Combat
{
    /// <summary>
    /// 架势条系统，管理架势值的增加、恢复和崩溃判定。
    /// 挂载在 Player 或 Boss 上，每个角色独立一条架势条。
    /// 依赖：CombatConfig（ScriptableObject）
    /// </summary>
    public class PostureSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("配置")]
        [SerializeField] private CombatConfig _combatConfig;

        #endregion

        #region Runtime State

        private float _maxPosture = 300f;
        private float _currentPosture;
        private float _recoveryRate = 0.15f;
        private float _recoveryDelay = 2f;
        private float _lastHitTime;
        private bool _isBlocking;
        private bool _isBroken;
        private float _blockRecoveryMultiplier = 0.5f;

        #endregion

        #region Properties

        /// <summary>当前架势值</summary>
        public float CurrentPosture => _currentPosture;

        /// <summary>最大架势值</summary>
        public float MaxPosture => _maxPosture;

        /// <summary>架势百分比（0-1）</summary>
        public float PosturePercent => _maxPosture > 0f
            ? Mathf.Clamp01(_currentPosture / _maxPosture) : 0f;

        /// <summary>是否处于崩溃状态</summary>
        public bool IsBroken => _isBroken;

        #endregion

        #region Events

        /// <summary>架势崩溃事件</summary>
        public event Action OnPostureBroken;

        /// <summary>架势值变化事件（当前值，最大值）</summary>
        public event Action<float, float> OnPostureChanged;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 初始化：从 CombatConfig 读取全局架势参数。
        /// 每实例参数（maxPosture 等）需通过 Initialize() 设置。
        /// </summary>
        private void Awake()
        {
            if (_combatConfig != null)
            {
                _recoveryRate = _combatConfig.postureRecoveryRate / 100f;
                _recoveryDelay = _combatConfig.postureRecoveryDelay;
            }

            _lastHitTime = Time.time;
        }

        /// <summary>
        /// 每帧调用，处理架势恢复逻辑。
        /// </summary>
        private void Update()
        {
            TryRecover();
        }

        #endregion

        #region Public API

        /// <summary>
        /// 初始化每实例参数（由 Player/Boss 控制器在 Start 时调用）。
        /// 覆盖 Awake 中的默认值。
        /// </summary>
        /// <param name="maxPosture">最大架势值（来自 PlayerStats/BossStats）</param>
        /// <param name="recoveryRatePercent">恢复速率百分比（如 15 表示 15%/秒）</param>
        /// <param name="recoveryDelay">脱战后恢复延迟（秒）</param>
        public void Initialize(float maxPosture, float recoveryRatePercent, float recoveryDelay)
        {
            _maxPosture = maxPosture;
            _recoveryRate = recoveryRatePercent / 100f;
            _recoveryDelay = recoveryDelay;
        }

        /// <summary>
        /// 增加架势值，可能触发崩溃。
        /// 崩溃状态下调用无效。
        /// </summary>
        /// <param name="amount">增加的架势值（正数）</param>
        public void AddPosture(float amount)
        {
            ChangePosture(amount);
        }

        /// <summary>
        /// 减少架势值（弹刀恢复）。
        /// 崩溃状态下调用无效。
        /// </summary>
        /// <param name="amount">减少的架势值（正数）</param>
        public void ReducePosture(float amount)
        {
            ChangePosture(-amount);
        }

        /// <summary>
        /// 受击时调用，重置恢复计时器。
        /// 由战斗控制器在受击时调用。
        /// </summary>
        public void OnHit()
        {
            _lastHitTime = Time.time;
        }

        /// <summary>
        /// 设置是否处于格挡状态（影响恢复速度）。
        /// 格挡时恢复速度减半。
        /// </summary>
        /// <param name="isBlocking">是否格挡中</param>
        public void SetBlocking(bool isBlocking)
        {
            _isBlocking = isBlocking;
        }

        /// <summary>
        /// 重置架势到 0，解除崩溃状态。
        /// 由状态机在崩溃动画结束后调用。
        /// </summary>
        public void Reset()
        {
            _currentPosture = 0f;
            _isBroken = false;
            _lastHitTime = Time.time;
            OnPostureChanged?.Invoke(_currentPosture, _maxPosture);
        }

        #endregion

        #region Internal Logic

        /// <summary>
        /// 统一处理架势值变化：钳制、事件触发、崩溃判定。
        /// </summary>
        /// <param name="delta">变化量（正数增加，负数减少）</param>
        private void ChangePosture(float delta)
        {
            if (_isBroken) return;

            float oldPosture = _currentPosture;
            _currentPosture = Mathf.Clamp(_currentPosture + delta, 0f, _maxPosture);

            if (Math.Abs(_currentPosture - oldPosture) < 0.001f) return;

            OnPostureChanged?.Invoke(_currentPosture, _maxPosture);

            if (_currentPosture >= _maxPosture)
            {
                _isBroken = true;
                OnPostureBroken?.Invoke();
            }
        }

        /// <summary>
        /// 架势恢复逻辑（使用 Unity Time）。
        /// 脱战超过延迟后按恢复速率回复，格挡状态下恢复速度减半。
        /// </summary>
        private void TryRecover()
        {
            TryRecover(Time.deltaTime, Time.time);
        }

        /// <summary>
        /// 架势恢复逻辑（可注入时间参数，供 EditMode 测试调用）。
        /// 脱战超过延迟后按恢复速率回复，格挡状态下恢复速度减半。
        /// </summary>
        /// <param name="deltaTime">本帧时间间隔（秒）</param>
        /// <param name="currentTime">当前时间（秒）</param>
        internal void TryRecover(float deltaTime, float currentTime)
        {
            if (_isBroken) return;

            float timeSinceHit = currentTime - _lastHitTime;
            if (timeSinceHit < _recoveryDelay) return;
            if (_currentPosture <= 0f) return;

            float blockMult = _isBlocking ? _blockRecoveryMultiplier : 1f;
            float recoveryAmount = CalcRecoveryAmount(
                _maxPosture, _recoveryRate, deltaTime, blockMult);

            _currentPosture = Mathf.Max(0f, _currentPosture - recoveryAmount);
            OnPostureChanged?.Invoke(_currentPosture, _maxPosture);
        }

        #endregion

        #region Static Calculation Methods (Testable)

        /// <summary>
        /// 计算单帧恢复量（纯计算，可测试）。
        /// </summary>
        /// <param name="maxPosture">最大架势值</param>
        /// <param name="recoveryRate">恢复速率（小数，如 0.15 表示 15%/秒）</param>
        /// <param name="deltaTime">帧间隔（秒）</param>
        /// <param name="blockMultiplier">格挡倍率（1.0 或 0.5）</param>
        /// <returns>本帧恢复的架势值</returns>
        internal static float CalcRecoveryAmount(
            float maxPosture, float recoveryRate, float deltaTime, float blockMultiplier)
        {
            return maxPosture * recoveryRate * deltaTime * blockMultiplier;
        }

        #endregion
    }
}
