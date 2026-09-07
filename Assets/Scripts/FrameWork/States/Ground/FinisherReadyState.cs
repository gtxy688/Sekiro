using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 完美弹反打崩 Boss 后的玩家确认窗口。
    // 窗口长度由 DeflectToFinsher 动画决定，超时后 Boss 仅恢复 20% 架势。
    public class FinisherReadyState : BaseState
    {
        private const string ReadyAnim = "DeflectToFinsher";

        private readonly HierarchicalState parent;
        private readonly CharacterBody victim;
        private bool hasSeenReadyAnim;

        public FinisherReadyState(
            CharacterBody body,
            HierarchicalState parent,
            CharacterBody victim) : base(body)
        {
            this.parent = parent;
            this.victim = victim;
        }

        public override void OnEnter()
        {
            hasSeenReadyAnim = false;

            if (!AnimUtil.HasState(body.Animator, ReadyAnim))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少弹反忍杀确认状态：{ReadyAnim}");
                ExpireWindow();
                return;
            }

            AnimUtil.TryCrossFade(body.Animator, ReadyAnim, 0.05f);
        }

        public override void OnUpdate()
        {
            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, ReadyAnim))
            {
                hasSeenReadyAnim = true;
                if (info.normalizedTime >= 0.95f)
                {
                    ExpireWindow();
                }
            }
            else if (hasSeenReadyAnim && !body.Animator.IsInTransition(0))
            {
                ExpireWindow();
            }
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is FinisherCommand)
            {
                CombatManager.Instance?.TryExecuteFinisher(body, FinisherKind.Deflect);
            }
            return true;
        }

        public override bool OnHitReceived(HitData hit)
        {
            return true;
        }

        private void ExpireWindow()
        {
            if (victim != null && victim.IsPostureBroken)
            {
                victim.RecoverFromBreak(0.8f);
            }

            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

}
