using UnityEngine;
using Sekiro.Core.StateMachine;
using Sekiro.Boss.AI;

namespace Sekiro.Boss
{
    /// <summary>
    /// Boss 状态机上下文 — 持有 Boss 各系统组件的引用。
    /// 状态类通过此上下文访问 Boss 系统，避免状态间直接耦合。
    /// </summary>
    public class BossContext
    {
        /// <summary>AI 控制器</summary>
        public BossAIController AIController;

        /// <summary>Boss 弹刀系统</summary>
        public BossDeflectSystem DeflectSystem;

        /// <summary>Boss Transform（位置和朝向）</summary>
        public Transform BossTransform;

        /// <summary>玩家 Transform</summary>
        public Transform PlayerTransform;

        /// <summary>Unity Animator 组件</summary>
        public Animator Animator;

        /// <summary>Boss 移动速度</summary>
        public float MoveSpeed = 3f;

        /// <summary>近战攻击距离阈值（米）</summary>
        public float AttackRange = 3f;

        /// <summary>Boss 当前血量百分比（0-1）</summary>
        public float HealthPercent = 1f;
    }

    /// <summary>
    /// Boss 状态机 — 继承 StateMachine 基类，管理 Boss 行为状态。
    /// 提供上下文访问和各状态的注册/转换。
    /// </summary>
    public class BossStateMachine : StateMachine
    {
        private BossContext _context;

        /// <summary>Boss 上下文（状态类通过此属性访问系统引用）</summary>
        public BossContext Context => _context;

        /// <summary>
        /// 初始化状态机，设置上下文并注册所有状态。
        /// </summary>
        /// <param name="context">Boss 上下文</param>
        public void Initialize(BossContext context)
        {
            _context = context;

            AddState<States.BossIdleState>();
            AddState<States.BossMoveState>();
            AddState<States.BossAttackState>();
            AddState<States.BossStaggerState>();
            AddState<States.BossCollapseState>();
            AddState<States.BossExecutedState>();
        }

    }
}
