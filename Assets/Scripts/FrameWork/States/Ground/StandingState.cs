using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // GroundStunnedState 的 Mid/Heavy 倒地反应播完后进入，负责通常的起身过程。
    // 它处于 GroundedState 下，可立即用防御/垫步取消；动画结束回 Idle，挨刀则走新的一次受击。
    public class StandingState : BaseState
    {
        // 播放请求成功但未正常读到结尾时，最多等待这段秒数；缺少动画时下次更新直接回待机。
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
            if (waitForAnim)
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

        // 常规起身已没有受击父状态的取消门槛；防御/垫步立即交给 CharacterBody 的地面切换接口。
        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is DodgeCommand)
            {
                CancelStanding(toDeflect: false, pressId: 0);
                return true;
            }
            if (cmd is DeflectCommand deflectCmd)
            {
                CancelStanding(toDeflect: true, pressId: deflectCmd.PressId);
                return true;
            }
            return true;
        }

        void CancelStanding(bool toDeflect, int pressId)
        {
            body.IsKnockedDown = false;
            // 与 MidToGuard 的区别：这里可直接换防御/垫步叶子，不再等一段起身进防动画。
            // 防御保留这次 PressId；DeflectState 会据此申请窗口，而非在 StandingState 内开启。
            body.TryChangeToDeflectOrDodge(toDeflect, pressId);
        }

        // 返回 false 表示没有拦住命中，让 CharacterBody.ReceiveHit 结算伤害并选择新的受击反应。
        public override bool OnHitReceived(HitData hit)
        {
            return false;
        }

        void GoIdle()
        {
            // 优先只替换当前地面叶子；如果已不在地面父状态下，则通过统一入口重新进入地面待机。
            if (!body.TryChangeGroundedSubState(
                    g => new IdleState(body, g), "standing: animation finished"))
            {
                body.EnterGrounded("standing: fallback to grounded idle");
            }
        }
    }

}
