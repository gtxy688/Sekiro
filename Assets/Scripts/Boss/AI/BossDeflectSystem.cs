using UnityEngine;
using Sekiro.Core.Data;
using Sekiro.Combat;

namespace Sekiro.Boss.AI
{
    /// <summary>
    /// Boss 弹刀系统 — 对称于玩家 DeflectSystem。
    /// AI 控制弹刀姿态的进入和退出，弹刀窗口 9 帧（0.15 秒）。
    /// 核心判定逻辑为 static 方法，便于 EditMode 测试。
    /// </summary>
    public class BossDeflectSystem : MonoBehaviour
    {
        #region Serialized Fields

        [Header("配置")]
        [SerializeField] private DeflectConfig _config;

        #endregion

        #region Runtime State

        private bool _isDeflectStance;
        private float _stanceTimer;
        private float _deflectTimer;
        private bool _isDeflectWindowActive;
        private int _playerComboCount;

        #endregion

        #region Properties

        /// <summary>是否处于弹刀姿态</summary>
        public bool IsDeflectStance => _isDeflectStance;

        /// <summary>当前玩家连击计数</summary>
        public int PlayerComboCount => _playerComboCount;

        #endregion

        #region Public API

        /// <summary>
        /// AI 决定进入弹刀姿态。设置弹刀窗口和姿态计时器。
        /// </summary>
        public void EnterDeflectStance()
        {
            if (_config == null) return;

            _isDeflectStance = true;
            _stanceTimer = _config.bossStanceDuration;
            _deflectTimer = _config.bossDeflectWindow;
            _isDeflectWindowActive = true;
        }

        /// <summary>
        /// 每帧调用，更新弹刀姿态计时器。
        /// </summary>
        public void Update()
        {
            if (!_isDeflectStance) return;

            float dt = Time.deltaTime;

            // 更新弹刀窗口
            if (_isDeflectWindowActive)
            {
                _deflectTimer -= dt;
                if (_deflectTimer <= 0f)
                    _isDeflectWindowActive = false;
            }

            // 更新姿态持续时间
            _stanceTimer -= dt;
            if (_stanceTimer <= 0f)
            {
                _isDeflectStance = false;
                _playerComboCount = 0;
            }
        }

        /// <summary>
        /// 检测玩家攻击是否被 Boss 弹开。
        /// </summary>
        /// <param name="playerAttack">玩家攻击数据</param>
        /// <param name="attackDirection">攻击方向（攻击者 → Boss）</param>
        /// <param name="bossForward">Boss 朝向前向量</param>
        /// <returns>弹刀结果：None / NormalBlock / PerfectDeflect</returns>
        public DeflectResult TryDeflect(AttackData playerAttack, Vector3 attackDirection, Vector3 bossForward)
        {
            return TryDeflect(
                playerAttack, attackDirection, bossForward,
                _isDeflectWindowActive, _isDeflectStance, _config);
        }

        /// <summary>
        /// AI 弹刀概率判定：是否应该进入弹刀姿态。
        /// </summary>
        /// <param name="bossHealthPercent">Boss 当前血量百分比（0-1）</param>
        public bool ShouldEnterDeflectStance(float bossHealthPercent)
        {
            return ShouldEnterDeflectStance(bossHealthPercent, _playerComboCount, Random.Range(0, 10000));
        }

        /// <summary>
        /// 增加玩家连击计数（每次玩家命中 Boss 时调用）
        /// </summary>
        public void IncrementComboCount()
        {
            _playerComboCount++;
        }

        /// <summary>
        /// 重置玩家连击计数（弹刀姿态结束时调用）
        /// </summary>
        public void ResetComboCount()
        {
            _playerComboCount = 0;
        }

        #endregion

        #region Static Methods (Testable)

        /// <summary>
        /// 弹刀判定（纯计算，可测试）。
        /// 检测攻击是否被 Boss 弹开或格挡。
        /// </summary>
        /// <param name="attack">攻击数据</param>
        /// <param name="attackDirection">攻击方向</param>
        /// <param name="bossForward">Boss 朝向</param>
        /// <param name="isDeflectWindowActive">弹刀窗口是否激活</param>
        /// <param name="isDeflectStance">是否在弹刀姿态</param>
        /// <param name="config">弹刀配置</param>
        /// <returns>弹刀结果</returns>
        public static DeflectResult TryDeflect(
            AttackData attack, Vector3 attackDirection, Vector3 bossForward,
            bool isDeflectWindowActive, bool isDeflectStance, DeflectConfig config)
        {
            if (attack == null || !attack.canBeDeflected)
                return DeflectResult.None;

            if (!isDeflectStance)
                return DeflectResult.None;

            if (!IsAttackFromFront(attackDirection, bossForward))
                return DeflectResult.None;

            if (isDeflectWindowActive)
                return DeflectResult.PerfectDeflect;

            return DeflectResult.NormalBlock;
        }

        /// <summary>
        /// AI 弹刀概率判定（纯计算，可测试）。
        /// 根据 Boss 血量和玩家连击数决定弹刀概率。
        /// </summary>
        /// <param name="bossHealthPercent">Boss 血量百分比（0-1）</param>
        /// <param name="playerComboCount">玩家当前连击数</param>
        /// <param name="randomSeed">随机种子（0~9999）</param>
        /// <returns>是否应进入弹刀姿态</returns>
        public static bool ShouldEnterDeflectStance(
            float bossHealthPercent, int playerComboCount, int randomSeed)
        {
            float chance;

            if (bossHealthPercent < 0.3f)
            {
                chance = 0.25f; // 低血量给玩家翻盘机会
            }
            else if (playerComboCount >= 2)
            {
                chance = 0.6f; // 玩家连招中提高弹刀概率
            }
            else
            {
                chance = 0.4f; // 基础概率
            }

            float roll = (randomSeed % 10000) / 10000f;
            return roll < chance;
        }

        /// <summary>
        /// 检测攻击是否来自正面（纯计算，可测试）
        /// </summary>
        internal static bool IsAttackFromFront(Vector3 attackDir, Vector3 forward)
        {
            if (attackDir == Vector3.zero || forward == Vector3.zero)
                return false;
            return Vector3.Angle(attackDir, forward) < 90f;
        }

        #endregion
    }
}
