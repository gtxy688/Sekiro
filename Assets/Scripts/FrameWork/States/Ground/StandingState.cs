using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // Mid/Heavy 倒地（含躺地）播完后的起身。期间挨刀按新的一次受击处理。
    public class StandingState : BaseState
    {
        private const float FallbackDuration = 2f;

        private string animName;
        private float timer;
        private bool waitForAnim;

        public StandingState(CharacterBody body) : base(body) { }

        public override void OnEnter()
        {
            timer = 0f;
            animName = HitReactionUtil.StandingAnim(body);
            waitForAnim = AnimUtil.TryCrossFade(body.Animator, animName, 0.08f);
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            if (waitForAnim && body.Animator != null)
            {
                AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if (AnimUtil.IsPlaying(info, animName))
                {
                    if (info.normalizedTime >= 0.99f && !body.Animator.IsInTransition(0))
                    {
                        GoIdle();
                        return;
                    }
                }

                if (timer >= FallbackDuration)
                    GoIdle();
                return;
            }

            GoIdle();
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is DodgeCommand)
            {
                CancelStanding(toDeflect: false);
                return true;
            }
            if (cmd is DeflectCommand)
            {
                CancelStanding(toDeflect: true);
                return true;
            }
            return true;
        }

        void CancelStanding(bool toDeflect)
        {
            body.IsKnockedDown = false;
            // 换格挡/垫步叶子（共用配方见 CharacterBody.TryChangeToDeflectOrDodge）
            body.TryChangeToDeflectOrDodge(toDeflect);
        }

        // 已经算站起来：不要拦截，让 ReceiveHit 按新一击完整播。
        public override bool OnHitReceived(HitData hit)
        {
            return false;
        }

        void GoIdle()
        {
            // 地面上换 Idle 子状态；非地面态回退重建地面父状态
            if (!body.TryChangeGroundedSubState(g => new IdleState(body, g)))
                body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

}
