using Sekiro.Core.StateMachine;
using Sekiro.Core.Events;

namespace Sekiro.Boss.States
{
    /// <summary>
    /// Boss 处决状态 — 玩家对架势崩溃的 Boss 执行忍杀。
    /// 播放处决动画并触发战斗结束事件。
    /// 此状态为终态，Boss 不再行动。
    /// </summary>
    public class BossExecutedState : State
    {
        /// <summary>处决动画播放时长（秒），之后触发事件</summary>
        private const float ExecutionDuration = 2f;

        private float _timer;
        private bool _hasTriggered;

        /// <summary>
        /// 进入处决状态，播放处决动画。
        /// </summary>
        public override void Enter()
        {
            _timer = ExecutionDuration;
            _hasTriggered = false;

            var ctx = ((BossStateMachine)_stateMachine).Context;
            if (ctx?.Animator != null)
                ctx.Animator.SetTrigger("deathblow");
        }

        /// <summary>
        /// 每帧执行：等待处决动画播放完毕。
        /// 中途触发一次战斗结束事件。
        /// </summary>
        public override void Execute()
        {
            _timer -= UnityEngine.Time.deltaTime;

            // 处决动画中段触发事件
            if (!_hasTriggered && _timer < ExecutionDuration * 0.5f)
            {
                _hasTriggered = true;
                CombatEvents.RaiseDeathblow();
                CombatEvents.RaiseCombatEnd();
            }

            // 处决状态不自动退出（终态）
        }

        /// <summary>
        /// 退出处决状态。
        /// </summary>
        public override void Exit()
        {
        }
    }
}
