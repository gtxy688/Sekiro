using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Air;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 地面父状态：管理地面与空中的切换，内部子状态机管理待机、移动、攻击等动作。
    // 开场由 CharacterBody.Start 进入；未指定子状态时默认进入 IdleState。
    public class GroundedState : HierarchicalState
    {
        // 强制切入可指定初始子状态；null 表示使用默认待机。
        private readonly BaseState forcedInitialSubState;

        public GroundedState(CharacterBody body, BaseState forcedInitialSubState = null) : base(body)
        {
            this.forcedInitialSubState = forcedInitialSubState;
        }

        // 默认待机随父状态缓存，重复进入时通过 OnEnter 重置，减少回待机时的对象创建。
        private IdleState cachedIdleSubState;

        // HierarchicalState.OnEnter 调用这里，再让子状态机进入返回的状态。
        protected override BaseState GetInitialSubState()
        {
            return forcedInitialSubState ?? (cachedIdleSubState ??= new IdleState(body, this));
        }

        // 先处理跨父状态的切换，仍在地面时再更新当前子状态。
        public override void OnUpdate()
        {
            // 踩空掉落。Boss 不进空中状态（Config.UseAirState = false）。
            // 被弹开硬直（含 Deflected_Boat）不能因根运动短暂离地被切走。
            if (!body.IsGrounded && !body.IsPostureBroken && !body.IsParried && !body.IsFinisherLocked && body.UsesAirState)
            {
                body.EnterAirborne("grounded: left ground");
                return;
            }

            base.OnUpdate(); // 更新当前子状态，包含待机、移动、攻击等。
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

            
            // 通过上面的公共限制后，由父状态统一处理喝药，避免每个叶子重复实现。
            if (cmd is HealCommand)
            {
                SubStateMachine.ChangeState(new HealState(body, this));
                return true;
            }

            // M10：只有在崩解机会已经出现后新按的攻击键才会生成 FinisherCommand。
            // 普通 AttackCommand 即使仍在连招缓冲中，也绝不能因本刀把 Boss 打崩而变成忍杀。
            if (cmd is FinisherCommand)
            {
                if (body.Faction == Faction.Player)
                    CombatManager.Instance.TryExecuteAvailableFinisher(body);
                return true;
            }

            // Boss 已可忍杀时残留的普通攻击预输入直接丢弃，不能再落到 Idle/Move 开一刀。
            if (cmd is AttackCommand &&
                body.Faction == Faction.Player &&
                CombatManager.Instance.HasAvailableFinisher(body))
            {
                return true;
            }

            return false;
        }
    }

}
