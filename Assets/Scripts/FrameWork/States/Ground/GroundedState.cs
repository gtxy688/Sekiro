using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Air;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    public class GroundedState : HierarchicalState
    {
        // 强制切入时指定的初始子状态（被弹反硬直/崩解倒地/喝药等物理覆写场景）
        // null 则走默认 Idle
        private readonly BaseState forcedInitialSubState;

        public GroundedState(CharacterBody body, BaseState forcedInitialSubState = null) : base(body)
        {
            this.forcedInitialSubState = forcedInitialSubState;
        }

        // 陷阱3-1 状态复用：默认 Idle 子状态随本实例缓存。IdleState 的唯一存储字段是
        // 不可变的 parent（=本实例），行为状态全在 OnEnter 重置（CrossFade/清速度/清移动意图），
        // 重复进入安全——消灭每次回待机的 IdleState 分配。带强制子状态的变体不受影响。
        private IdleState cachedIdleSubState;

        protected override BaseState GetInitialSubState()
        {
            return forcedInitialSubState ?? (cachedIdleSubState ??= new IdleState(body, this));
        }

        // 负责处理父层级的状态切换,内部层级切换交由子状态去处理
        public override void OnUpdate()
        {
            // 踩空掉落。Boss 不进空中状态（Config.UseAirState = false）。
            // 被弹开硬直（含 Deflected_Boat）不能因根运动短暂离地被切走。
            if (!body.IsGrounded && !body.IsPostureBroken && !body.IsParried && !body.IsFinisherLocked && body.UsesAirState)
            {
                body.EnterAirborne("grounded: left ground");
                return;
            }

            base.OnUpdate(); // 执行子状态 (Idle 或 Move)
        }

        protected override bool OnParentHandleCommand(ICommand cmd)
        {
            // 忍杀 / Elbow 投技：父层也吞掉喝药，不能把演出切走。
            if (body.IsFinisherLocked)
            {
                return true;
            }

            // 崩解倒地由 StaggerBrokenState 吞命令。父层若先切喝药，
            // RecoverFromBreak 永远不会跑，架势条会卡满且无法忍杀。
            if (body.IsPostureBroken)
            {
                return false;
            }

            // 被弹开硬直：葫芦在父层会强切，必须留给 ParriedState 吞掉。
            if (body.IsParried)
            {
                return false;
            }

            // 喝药期间只有移动会下钻到 HealState；跳跃和重复喝药在父层直接吞掉。
            if (body.IsHealing && (cmd is JumpCommand || cmd is HealCommand))
            {
                return true;
            }

            // 攻击命中段不能被跳跃/喝药强切；命令保持在 0.2s 缓冲中等待后摇。
            if (body.IsAttacking &&
                !body.IsAttackRecoveryOpen &&
                (cmd is JumpCommand || cmd is HealCommand))
            {
                return false;
            }

            // 拦截跳跃指令
            if (cmd is JumpCommand)
            {
                if (!body.UsesAirState)
                {
                    return true;
                }

                body.QueueJump();
                body.EnterAirborne("command: jump");
                return true;
            }

            // M16：拦截葫芦指令 → 切喝药状态（播动画 + 可被打断硬直）
            if (cmd is HealCommand)
            {
                SubStateMachine.ChangeState(new HealState(body, this));
                return true;
            }

            // M10：玩家攻击指令优先查处决。Boss 崩解窗口内，连招后摇里再按攻击也走忍杀，
            // 不进 NextCombo。崩解那一刀本身不会再发 AttackCommand，所以不会被这刀直接处决。
            if (cmd is AttackCommand &&
                body.Faction == Faction.Player &&
                CombatManager.Instance != null &&
                CombatManager.Instance.TryExecuteAvailableFinisher(body))
            {
                return true;
            }

            return false;
        }
    }

}
