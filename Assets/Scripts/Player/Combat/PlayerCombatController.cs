using UnityEngine;
using Sekiro.Core.Data;
using Sekiro.Core.Events;
using Sekiro.Combat;
using Sekiro.Player.Input;

namespace Sekiro.Player.Combat
{
    /// <summary>
    /// 玩家战斗控制器 — 整合输入、弹刀、架势、攻击的中枢。
    /// 简化版：攻击/闪避/识破为动画触发预留，弹刀和受击为完整实现。
    /// </summary>
    public class PlayerCombatController : MonoBehaviour
    {
        #region Serialized Fields

        [Header("系统引用")]
        [SerializeField] private InputReaderComponent _inputReader;
        [SerializeField] private DeflectSystem _deflectSystem;
        [SerializeField] private PostureSystem _postureSystem;
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private CombatConfig _combatConfig;

        #endregion

        #region Runtime State

        private float _currentHealth;
        private int _healingCharges;
        private int _comboCount;
        private float _lastAttackTime;
        private bool _wasDeflectHeld;

        /// <summary>连招重置时间（秒）</summary>
        private const float ComboResetTime = 0.8f;

        /// <summary>最大连招段数</summary>
        private const int MaxComboCount = 3;

        #endregion

        #region Properties

        /// <summary>当前生命值</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>最大生命值</summary>
        public float MaxHealth => _playerStats != null ? _playerStats.maxHealth : 0f;

        /// <summary>剩余回血次数</summary>
        public int HealingCharges => _healingCharges;

        /// <summary>当前连招段数（1~3，0 表示未出招）</summary>
        public int ComboCount => _comboCount;

        #endregion

        #region Unity Lifecycle

        /// <summary>
        /// 自动获取同 GameObject 上的组件引用
        /// </summary>
        private void Awake()
        {
            if (_inputReader == null) _inputReader = GetComponent<InputReaderComponent>();
            if (_deflectSystem == null) _deflectSystem = GetComponent<DeflectSystem>();
            if (_postureSystem == null) _postureSystem = GetComponent<PostureSystem>();
        }

        /// <summary>
        /// 从 PlayerStats 初始化运行时状态
        /// </summary>
        private void Start()
        {
            if (_playerStats != null)
                InitFromStats(_playerStats);
        }

        /// <summary>
        /// 每帧读取输入并分发战斗行为
        /// </summary>
        private void Update()
        {
            if (_inputReader == null) return;

            // 弹刀（鼠标右键）
            if (_inputReader.IsDeflectPressed())
                HandleDeflectPressed();

            bool deflectHeld = _inputReader.IsDeflectHeld();
            if (_wasDeflectHeld && !deflectHeld)
                HandleDeflectReleased();
            _wasDeflectHeld = deflectHeld;

            // 攻击（鼠标左键）
            if (_inputReader.IsAttackPressed())
                HandleAttack();

            // 闪避 / 识破（Shift）
            if (_inputReader.IsDodgePressed())
                HandleDodge();

            // 跳跃（空格）— 由 PlayerController.Update 自行处理
            // TODO: 当需要战斗状态限制跳跃时，在此处拦截

            // 回血（E）
            if (_inputReader.IsHealPressed())
                Heal();
        }

        #endregion

        #region Input Handling

        /// <summary>
        /// 弹刀按下：转发给 DeflectSystem
        /// </summary>
        internal void HandleDeflectPressed()
        {
            _deflectSystem?.OnDeflectPressed();
        }

        /// <summary>
        /// 弹刀松开：转发给 DeflectSystem
        /// </summary>
        internal void HandleDeflectReleased()
        {
            _deflectSystem?.OnDeflectReleased();
        }

        /// <summary>
        /// 攻击输入：管理 3 段连招计数
        /// </summary>
        internal void HandleAttack()
        {
            if (Time.time - _lastAttackTime > ComboResetTime)
                _comboCount = 0;

            _comboCount++;
            if (_comboCount > MaxComboCount)
                _comboCount = 1;

            _lastAttackTime = Time.time;
            // TODO: 通过状态机触发对应段数的攻击动画
        }

        /// <summary>
        /// 闪避 / 识破输入（预留接口）
        /// </summary>
        internal void HandleDodge()
        {
            // TODO: 通过状态机触发闪避或识破动画
            // 当 Boss 释放突刺危字时，闪避自动切换为识破
        }

        #endregion

        #region Combat — 受击与回血

        /// <summary>
        /// 外部调用：玩家受到 Boss 攻击时触发。
        /// 调用 DeflectSystem 判定弹刀结果，再应用伤害。
        /// </summary>
        /// <param name="attack">攻击数据</param>
        /// <param name="attackDir">攻击方向（攻击者 → 玩家）</param>
        /// <param name="playerForward">玩家正面朝向</param>
        public void TakeDamage(AttackData attack, Vector3 attackDir, Vector3 playerForward)
        {
            if (_deflectSystem == null || _postureSystem == null || _playerStats == null)
                return;

            DeflectResult result = _deflectSystem.TryDeflect(attack, attackDir, playerForward);
            ApplyDamageResult(attack, result);
        }

        /// <summary>
        /// 根据弹刀结果应用伤害、架势变化和事件广播。
        /// 核心战斗逻辑，可独立测试。
        /// </summary>
        /// <param name="attack">攻击数据</param>
        /// <param name="result">弹刀结果</param>
        internal void ApplyDamageResult(AttackData attack, DeflectResult result)
        {
            DefenseState state = DeflectResultToDefenseState(result);

            // 1. HP 伤害
            float healthDamage = DamageCalculator.CalculateDamage(
                attack, _playerStats.defense, state);
            _currentHealth = Mathf.Max(0f, _currentHealth - healthDamage);

            // 2. 架势伤害（在弹刀加成计算前获取倍率）
            float deflectMultiplier = _deflectSystem != null
                ? _deflectSystem.GetPostureDamageMultiplier()
                : 1f;
            float postureDamage = DamageCalculator.CalculatePostureDamage(
                attack, state, deflectMultiplier);
            _postureSystem.AddPosture(postureDamage);
            _postureSystem.OnHit();

            // 3. 弹刀成功处理
            if (result == DeflectResult.PerfectDeflect)
            {
                _deflectSystem.OnSuccessfulDeflect();

                float recovery = _postureSystem.MaxPosture * _playerStats.deflectPostureRecovery;
                _postureSystem.ReducePosture(recovery);

                CombatEvents.RaisePerfectDeflect();

                // 完美弹刀对 Boss 造成架势伤害
                float bossPostureDamage = attack.postureDamage * deflectMultiplier;
                CombatEvents.RaiseBossDamaged(bossPostureDamage);
            }
            else if (result == DeflectResult.NormalBlock)
            {
                CombatEvents.RaiseNormalBlock();
            }

            // 4. 广播玩家状态事件
            CombatEvents.RaisePlayerDamaged(healthDamage);
            CombatEvents.RaisePlayerPostureChanged(
                _postureSystem.CurrentPosture, _postureSystem.MaxPosture);

            if (_postureSystem.IsBroken)
                CombatEvents.RaisePlayerPostureBreak();
        }

        /// <summary>
        /// 回血：消耗一次药葫芦，恢复生命值。
        /// 次数耗尽或满血时无效。
        /// </summary>
        public void Heal()
        {
            if (_healingCharges <= 0) return;
            if (_currentHealth >= _playerStats.maxHealth) return;

            _healingCharges--;
            float healAmount = _playerStats.maxHealth * _playerStats.healPercent;
            _currentHealth = Mathf.Min(_currentHealth + healAmount, _playerStats.maxHealth);

            CombatEvents.RaiseHealingChargeChanged(_healingCharges);
        }

        #endregion

        #region Utility

        /// <summary>
        /// 将弹刀结果映射为防御状态
        /// </summary>
        /// <param name="result">弹刀结果</param>
        /// <returns>对应的防御状态</returns>
        public static DefenseState DeflectResultToDefenseState(DeflectResult result)
        {
            switch (result)
            {
                case DeflectResult.PerfectDeflect: return DefenseState.PerfectDeflect;
                case DeflectResult.NormalBlock: return DefenseState.NormalBlock;
                default: return DefenseState.None;
            }
        }

        /// <summary>
        /// 从 PlayerStats 初始化运行时状态
        /// </summary>
        private void InitFromStats(PlayerStats stats)
        {
            _currentHealth = stats.maxHealth;
            _healingCharges = stats.maxHealingCharges;

            if (_postureSystem != null)
                _postureSystem.Initialize(stats.maxPosture, stats.postureRecoveryRate, 2f);
        }

        /// <summary>
        /// 初始化控制器（供测试和外部系统集成调用）
        /// </summary>
        /// <param name="stats">玩家属性配置</param>
        /// <param name="deflect">弹刀系统实例</param>
        /// <param name="posture">架势系统实例</param>
        internal void Initialize(PlayerStats stats, DeflectSystem deflect, PostureSystem posture)
        {
            _playerStats = stats;
            _deflectSystem = deflect;
            _postureSystem = posture;
            InitFromStats(stats);
        }

        /// <summary>
        /// 设置当前生命值（供测试和剧情脚本调用）
        /// </summary>
        /// <param name="health">目标生命值</param>
        internal void SetHealth(float health)
        {
            _currentHealth = Mathf.Clamp(health, 0f, MaxHealth);
        }

        #endregion
    }
}
