using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // Hurt_Mid 受击期间按防御时播放起身进防，结束后按住则举刀，松开则回待机。
    // 保存 PressId，直到真正切入 DeflectState 才开启主动窗口，配置也可提前开放防御 / 垫步取消。
    public class MidToGuardState : BaseState
    {
        private const float FallbackDuration = 1.2f;

        private int pressId;
        private string animName;
        private float timer;
        private bool seenStart;
        private bool waitForAnim;

        public MidToGuardState(CharacterBody body, int pressId = 0) : base(body)
        {
            this.pressId = pressId;
        }

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
            if (cmd is DeflectCommand deflectCmd)
            {
                if (CanDeflectOrDodgeCancel)
                {
                    CancelToDeflectOrDodge(toDeflect: true, deflectCmd.PressId);
                    return true;
                }
                // 未到提前开放时刻时先保存真实按下，结束后再用它开启防御窗口。
                if (deflectCmd.IsPhysicalPress) pressId = deflectCmd.PressId;
                return true;
            }
            if (cmd is DodgeCommand)
            {
                if (CanDeflectOrDodgeCancel)
                {
                    CancelToDeflectOrDodge(toDeflect: false, pressId: 0);
                    return true;
                }
                // 窗口未到：不消耗，留缓冲重试（格挡键可长按）
                return false;
            }
            return true;
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            if (waitForAnim)
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
            body.TryChangeGroundedSubState(g => IsReleased()
                ? (BaseState)new IdleState(body, g)
                : new DeflectState(body, g, pressId: pressId));
        }

        // 这次按下是否已经松开。物理按下按 PressId 查释放记录；系统回防没有身份，看实时按住状态。
        private bool IsReleased()
        {
            if (pressId > 0) return body.IsDeflectPressReleased(pressId);
            return !body.IsDeflectHeld;
        }

        void CancelToDeflectOrDodge(bool toDeflect, int pressId)
        {
            // 换格挡/垫步叶子（共用配方见 CharacterBody.TryChangeToDeflectOrDodge）
            body.TryChangeToDeflectOrDodge(toDeflect, pressId);
        }
    }

}
