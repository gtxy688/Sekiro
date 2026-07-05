using UnityEngine;
using Sekiro.Core.Data;
using Sekiro.Core.Events;
using Sekiro.Combat;
using Sekiro.Player.Combat;

namespace Sekiro.Player.StateMachine
{
    /// <summary>
    /// 玩家状态机驱动器 — MonoBehaviour 包装器。
    /// 收集组件引用、创建上下文、每帧驱动状态机。
    /// 监听 Boss 攻击/架势崩溃事件并执行对应流程。
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(PlayerCombatController))]
    [RequireComponent(typeof(DeflectSystem))]
    [RequireComponent(typeof(PostureSystem))]
    [RequireComponent(typeof(CharacterController))]
    public class PlayerStateMachineDriver : MonoBehaviour
    {
        #region Serialized Fields

        [Header("配置")]
        [SerializeField] private PlayerStats _playerStats;
        [SerializeField] private CombatConfig _combatConfig;

        #endregion

        #region Runtime State

        private PlayerContext _context;
        private PlayerStateMachine _stateMachine;
        private bool _bossPostureBroken;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            BuildContext();
            _stateMachine = new PlayerStateMachine();
            _stateMachine.Initialize(_context);

            // 注册事件
            BossAttackEvents.OnBossAttackHitPlayer += OnBossAttackHitPlayer;
            CombatEvents.SubscribeOnBossPostureBreak(OnBossPostureBreak);

            // 切换到初始状态
            _stateMachine.TransitionTo<States.GroundedState>();
        }

        private void Start()
        {
            if (_context.CombatController != null && _playerStats != null)
            {
                _context.CombatController.Initialize(
                    _playerStats,
                    _context.DeflectSystem,
                    _context.PostureSystem);
            }
        }

        private void Update()
        {
            // 帧冻结期间暂停所有状态更新
            if (_context.HitStopManager != null && _context.HitStopManager.IsActive)
                return;

            // 处决检测（优先级最高的输入判定）
            if (_bossPostureBroken && _context.InputReader != null
                && _context.InputReader.IsAttackPressed())
            {
                _bossPostureBroken = false;
                _stateMachine.TryTransitionTo<States.DeathblowState>();
                return;
            }

            _stateMachine.Update();
        }

        private void OnDestroy()
        {
            BossAttackEvents.OnBossAttackHitPlayer -= OnBossAttackHitPlayer;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Boss 攻击命中玩家时执行完整伤害/弹刀流程。
        /// </summary>
        private void OnBossAttackHitPlayer(AttackData attack, Vector3 attackDir, Vector3 playerForward)
        {
            if (_context.DeflectSystem == null || _context.CombatController == null)
                return;

            // 1. 弹刀判定
            DeflectResult result = _context.DeflectSystem.TryDeflect(attack, attackDir, playerForward);

            // 2. 应用伤害
            _context.CombatController.ApplyDamageResult(attack, result);

            // 3. 帧冻结
            float hitStopDuration = GetHitStopDuration(result);
            if (_context.HitStopManager != null)
                _context.HitStopManager.Trigger(hitStopDuration);

            // 4. 状态转换
            if (result == DeflectResult.None)
            {
                _stateMachine.TryTransitionTo<States.HitState>();
            }
            else if (result == DeflectResult.NormalBlock)
            {
                _stateMachine.TryTransitionTo<States.DeflectState>();
            }
        }

        /// <summary>
        /// Boss 架势崩溃时设置处决标记。
        /// </summary>
        private void OnBossPostureBreak()
        {
            _bossPostureBroken = true;
        }

        #endregion

        #region Internal

        private void BuildContext()
        {
            _context = new PlayerContext
            {
                Animator = GetComponentInChildren<Animator>(),
                Controller = GetComponent<PlayerController>(),
                CombatController = GetComponent<PlayerCombatController>(),
                CharacterController = GetComponent<CharacterController>(),
                Transform = transform,
                DeflectSystem = GetComponent<DeflectSystem>(),
                PostureSystem = GetComponent<PostureSystem>(),
                DangerSystem = GetComponent<DangerSystem>(),
                HitStopManager = GetComponent<HitStopManager>(),
                InputReader = GetComponent<InputReaderComponent>(),
                Stats = _playerStats,
                CombatConfig = _combatConfig
            };
        }

        private float GetHitStopDuration(DeflectResult result)
        {
            if (_combatConfig == null)
            {
                return result == DeflectResult.PerfectDeflect ? 0.05f
                     : result == DeflectResult.NormalBlock ? 0.033f
                     : 0f;
            }

            return result == DeflectResult.PerfectDeflect ? _combatConfig.deflectHitStop
                 : result == DeflectResult.NormalBlock ? _combatConfig.normalHitStop
                 : 0f;
        }

        #endregion

        #region Public API

        /// <summary>获取玩家状态机（供外部查询当前状态）</summary>
        public PlayerStateMachine StateMachine => _stateMachine;

        /// <summary>获取玩家上下文（供外部查询运行时引用）</summary>
        public PlayerContext Context => _context;

        #endregion
    }
}
