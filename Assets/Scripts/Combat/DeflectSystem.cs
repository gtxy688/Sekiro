using UnityEngine;
using Sekiro.Core.Data;

namespace Sekiro.Combat
{
    /// <summary>
    /// 弹刀结果枚举
    /// </summary>
    public enum DeflectResult
    {
        /// <summary>未弹刀（未在窗口内或角度不对）</summary>
        None,
        /// <summary>普通格挡（在格挡窗口内受击）</summary>
        NormalBlock,
        /// <summary>完美弹刀（在完美窗口内受击）</summary>
        PerfectDeflect
    }

    /// <summary>
    /// 弹刀系统，处理弹刀判定、抖刀惩罚、连续加成。
    /// 核心计算逻辑抽取为 internal static 方法，便于 EditMode 测试。
    /// 依赖：DeflectConfig（ScriptableObject）
    /// </summary>
    public class DeflectSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("配置")]
        [SerializeField] private DeflectConfig _config;

        #endregion

        #region Runtime State

        private int _spamCount;
        private float _lastDeflectPressTime;
        private float _lastDeflectReleaseTime;
        private bool _isDeflecting;
        private bool _isBlocking;
        private float _deflectTimer;
        private int _deflectChainCount;
        private float _lastDeflectSuccessTime;
        private bool _deflectStateActive;
        private float _blockTimer;

        #endregion

        #region Properties

        /// <summary>是否处于弹刀状态（完美弹刀窗口内）</summary>
        public bool IsDeflecting => _isDeflecting;

        /// <summary>是否处于格挡状态（按住右键或格挡窗口内）</summary>
        public bool IsBlocking => _isBlocking;

        /// <summary>当前抖刀计数（内部，供测试访问）</summary>
        internal int SpamCount => _spamCount;

        /// <summary>当前连续弹刀计数（内部，供测试访问）</summary>
        internal int DeflectChainCount => _deflectChainCount;

        #endregion

        #region Public API

        /// <summary>
        /// 玩家按下右键时调用。进入弹刀状态，检测抖刀惩罚。
        /// </summary>
        public void OnDeflectPressed()
        {
            float currentTime = Time.time;

            // 抖刀检测：在重置时间内再次按下且上次未成功弹刀
            if (_deflectStateActive && currentTime - _lastDeflectPressTime < _config.spamResetTime)
            {
                _spamCount++;
            }

            _lastDeflectPressTime = currentTime;
            _isDeflecting = true;
            _isBlocking = false;
            _deflectTimer = 0f;
            _blockTimer = 0f;
            _deflectStateActive = true;
        }

        /// <summary>
        /// 玩家松开右键时调用。退出弹刀/格挡状态。
        /// </summary>
        public void OnDeflectReleased()
        {
            _lastDeflectReleaseTime = Time.time;
            _isDeflecting = false;
            _isBlocking = false;
            _deflectStateActive = false;
        }

        /// <summary>
        /// 每帧调用，更新弹刀状态计时器。
        /// </summary>
        public void Update()
        {
            float deltaTime = Time.deltaTime;
            float currentTime = Time.time;

            if (_isDeflecting)
            {
                _deflectTimer += deltaTime;
                float window = GetCurrentDeflectWindow();

                if (_deflectTimer > window)
                {
                    _isDeflecting = false;
                    _isBlocking = true;
                    _blockTimer = 0f;
                }
            }
            else if (_isBlocking && _deflectStateActive)
            {
                // 轻点弹刀后的格挡窗口延伸（非长按）
                _blockTimer += deltaTime;
                if (_blockTimer > _config.blockWindowDuration)
                {
                    _isBlocking = false;
                    _deflectStateActive = false;
                }
            }

            // 抖刀惩罚自动重置：停止按键超过重置时间
            if (_spamCount > 0 && currentTime - _lastDeflectReleaseTime >= _config.spamResetTime)
            {
                _spamCount = 0;
            }

            // 连续弹刀加成重置：超过重置时间未成功弹刀
            if (_deflectChainCount > 0 && currentTime - _lastDeflectSuccessTime > _config.deflectChainResetTime)
            {
                _deflectChainCount = 0;
            }
        }

        /// <summary>
        /// 检测是否受到攻击，返回弹刀结果。
        /// </summary>
        /// <param name="incomingAttack">攻击数据</param>
        /// <param name="attackDirection">攻击方向</param>
        /// <param name="playerForward">玩家朝向</param>
        /// <returns>弹刀结果：None / NormalBlock / PerfectDeflect</returns>
        public DeflectResult TryDeflect(AttackData incomingAttack, Vector3 attackDirection, Vector3 playerForward)
        {
            if (incomingAttack == null || !incomingAttack.canBeDeflected)
                return DeflectResult.None;

            if (!_isDeflecting && !_isBlocking)
                return DeflectResult.None;

            if (!IsAttackFromFront(attackDirection, playerForward))
                return DeflectResult.None;

            if (_isDeflecting)
                return DeflectResult.PerfectDeflect;

            return DeflectResult.NormalBlock;
        }

        /// <summary>
        /// 成功弹刀后调用。重置抖刀惩罚，增加连续弹刀计数。
        /// </summary>
        public void OnSuccessfulDeflect()
        {
            _spamCount = 0;
            _deflectChainCount++;
            _lastDeflectSuccessTime = Time.time;
        }

        /// <summary>
        /// 获取当前弹刀窗口时间（秒），考虑抖刀惩罚。
        /// </summary>
        public float GetCurrentDeflectWindow()
        {
            return CalcDeflectWindow(
                _config.baseDeflectWindow,
                _config.minDeflectWindow,
                _config.windowReductionPerSpam,
                _spamCount);
        }

        /// <summary>
        /// 获取当前架势伤害倍率，考虑连续加成和抖刀惩罚。
        /// </summary>
        public float GetPostureDamageMultiplier()
        {
            float chainBonus = CalcChainBonus(_deflectChainCount, _config.deflectChainMultipliers);
            float spamPenalty = CalcSpamPenalty(
                _spamCount,
                _config.spamPostureDamageMin);
            return chainBonus * spamPenalty;
        }

        #endregion

        #region Static Calculation Methods (Testable)

        /// <summary>
        /// 计算弹刀窗口时间（纯计算，可测试）
        /// </summary>
        internal static float CalcDeflectWindow(
            float baseWindow, float minWindow, float reductionPerSpam, int spamCount)
        {
            float window = baseWindow - spamCount * reductionPerSpam;
            return Mathf.Max(window, minWindow);
        }

        /// <summary>
        /// 计算连续弹刀加成倍率（纯计算，可测试）
        /// </summary>
        internal static float CalcChainBonus(int chainCount, float[] multipliers)
        {
            if (multipliers == null || multipliers.Length == 0)
                return 1f;
            int index = Mathf.Clamp(chainCount - 1, 0, multipliers.Length - 1);
            return multipliers[index];
        }

        /// <summary>
        /// 计算抖刀惩罚倍率（纯计算，可测试）
        /// </summary>
        internal static float CalcSpamPenalty(int spamCount, float minMultiplier)
        {
            if (spamCount <= 0) return 1f;
            float step = (1f - minMultiplier) / 4f;
            float penalty = 1f - spamCount * step;
            return Mathf.Max(penalty, minMultiplier);
        }

        /// <summary>
        /// 检测攻击是否来自正面（纯计算，可测试）
        /// </summary>
        internal static bool IsAttackFromFront(Vector3 attackDir, Vector3 playerForward)
        {
            if (attackDir == Vector3.zero || playerForward == Vector3.zero)
                return false;
            // attackDir: 攻击方向（从攻击者指向防御者）
            // 正面攻击时 attackDir 与 playerForward 夹角 < 90°
            float angle = Vector3.Angle(attackDir, playerForward);
            return angle < 90f;
        }

        #endregion
    }
}
