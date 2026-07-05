using UnityEngine;
using Sekiro.Core.StateMachine;
using Sekiro.Core.Data;
using Sekiro.Combat;
using Sekiro.Player.Input;
using Sekiro.Player.Combat;
using Sekiro.Player.Movement;

namespace Sekiro.Player.StateMachine
{
    /// <summary>
    /// 玩家状态机上下文 — 持有玩家所有子系统的引用。
    /// 状态类通过此上下文访问系统组件，避免状态间直接耦合。
    /// </summary>
    [System.Serializable]
    public class PlayerContext
    {
        [Header("组件引用")]
        public Animator Animator;
        public PlayerController Controller;
        public PlayerCombatController CombatController;
        public CharacterController CharacterController;
        public Transform Transform;

        [Header("战斗系统")]
        public DeflectSystem DeflectSystem;
        public PostureSystem PostureSystem;
        public DangerSystem DangerSystem;
        public HitStopManager HitStopManager;

        [Header("输入")]
        public InputReaderComponent InputReader;

        [Header("配置")]
        public PlayerStats Stats;
        public CombatConfig CombatConfig;
    }

    /// <summary>
    /// 玩家基础状态 — 所有玩家状态继承此类。
    /// 提供优先级属性和上下文快捷访问。
    /// </summary>
    public abstract class PlayerBaseState : State
    {
        protected PlayerStateMachine PlayerSM => (PlayerStateMachine)_stateMachine;
        protected PlayerContext Ctx => PlayerSM.Context;

        /// <summary>
        /// 状态优先级（越大越优先，高优先级可打断低优先级）。
        /// Deathblow(9) > Stun(8) > Hit(7) > LightningCharge(6) > Heal(5)
        /// > Deflect(4) > Dodge(3) > Mikiri(2) > Attack(1) > Grounded/Airborne(0)
        /// </summary>
        public abstract int Priority { get; }
    }

    /// <summary>
    /// 玩家状态机 — 继承 StateMachine 基类，管理玩家行为状态。
    /// 添加优先级打断检查，确保高优先级状态可中断低优先级状态。
    /// </summary>
    public class PlayerStateMachine : Core.StateMachine.StateMachine
    {
        private PlayerContext _context;

        /// <summary>玩家上下文（状态类通过此属性访问系统引用）</summary>
        public PlayerContext Context => _context;

        /// <summary>
        /// 初始化状态机，设置上下文并注册所有状态。
        /// </summary>
        /// <param name="context">玩家上下文</param>
        public void Initialize(PlayerContext context)
        {
            _context = context;

            // 基础层
            AddState<States.GroundedState>();
            AddState<States.AirborneState>();

            // 战斗层
            AddState<States.AttackState>();
            AddState<States.DeflectState>();
            AddState<States.DodgeState>();
            AddState<States.MikiriState>();
            AddState<States.HitState>();
            AddState<States.StunState>();
            AddState<States.DeathblowState>();
            AddState<States.HealState>();
        }

        /// <summary>
        /// 转换到指定状态，带优先级检查。
        /// 目标状态优先级必须高于当前状态才能打断。
        /// 相同优先级不能互相打断。
        /// </summary>
        /// <typeparam name="T">目标状态类型</typeparam>
        /// <returns>是否成功转换</returns>
        public bool TryTransitionTo<T>() where T : State
        {
            if (CanTransitionTo<T>())
            {
                TransitionTo<T>();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 检查是否能转换到目标状态（优先级判定）。
        /// </summary>
        public bool CanTransitionTo<T>() where T : State
        {
            if (_currentState == null) return true;

            int currentPriority = _currentState is PlayerBaseState current
                ? current.Priority : -1;

            int targetPriority = _states[typeof(T)] is PlayerBaseState target
                ? target.Priority : -1;

            return targetPriority > currentPriority;
        }
    }
}
