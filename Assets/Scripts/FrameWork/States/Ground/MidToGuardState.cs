using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // Hurt_Mid 倒地结束前按下防御：播起身进防，播完按住则举刀，松开则 Idle。
    public class MidToGuardState : BaseState
    {
        private const float FallbackDuration = 1.2f;

        private string animName;
        private float timer;
        private bool released;
        private bool seenStart;
        private bool waitForAnim;

        public MidToGuardState(CharacterBody body) : base(body) { }

        public bool CanDeflectOrDodgeCancel
        {
            get
            {
                float open = body.MidToGuardDeflectDodgeOpenTime;
                return open > 0f && timer >= open;
            }
        }

        public override void OnEnter()
        {
            body.IsGuarding = true;
            released = false;
            timer = 0f;
            seenStart = false;
            animName = HitReactionUtil.MidToGuardAnim(body);
            waitForAnim = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
        }

        public override void OnExit()
        {
            body.IsGuarding = false;
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is DeflectCommand)
            {
                if (CanDeflectOrDodgeCancel)
                {
                    CancelToDeflectOrDodge(toDeflect: true);
                    return true;
                }
                released = false;
                return true;
            }
            if (cmd is DodgeCommand)
            {
                if (CanDeflectOrDodgeCancel)
                {
                    CancelToDeflectOrDodge(toDeflect: false);
                    return true;
                }
                // 窗口未到：不消耗，留缓冲重试（格挡键可长按）
                return false;
            }
            if (cmd is IdleCommand)
            {
                released = true;
                return true;
            }
            return true;
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            if (waitForAnim && body.Animator != null)
            {
                AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if (AnimUtil.IsPlaying(info, animName))
                {
                    seenStart = true;
                    if (info.normalizedTime >= 0.95f)
                        Finish();
                    return;
                }

                if (seenStart || timer >= FallbackDuration)
                    Finish();
                return;
            }

            if (timer >= FallbackDuration)
                Finish();
        }

        // 起身防还没进举刀循环：被打按新受击打断。
        public override bool OnHitReceived(HitData hit)
        {
            return false;
        }

        void Finish()
        {
            body.TryChangeGroundedSubState(g => released
                ? (BaseState)new IdleState(body, g)
                : new DeflectState(body, g));
        }

        void CancelToDeflectOrDodge(bool toDeflect)
        {
            // 换格挡/垫步叶子（共用配方见 CharacterBody.TryChangeToDeflectOrDodge）
            body.TryChangeToDeflectOrDodge(toDeflect);
        }
    }

}
