using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // StunnedState 在 Mid 倒地前段收到防御命令时进入，播放提前起身进防，而不是等倒地播完再走 StandingState。
    // 动画结束后仍按住则进入 DeflectState，已松开则回 Idle；配置时刻到达后也可提前取消到防御或垫步。
    // 这里只保存按下身份，主动弹反窗口要等真正进入 DeflectState 才申请开启。
    public class MidToGuardState : BaseState
    {
        private const float FallbackDuration = 1.2f;

        // 来自输入命令的 PressId：把“哪次按下”带过动画等待，结束时还能检查它是否已释放。
        private int pressId;
        private string animName;
        private float timer;
        private bool seenStart;
        private bool waitForAnim;

        public MidToGuardState(CharacterBody body, int pressId = 0) : base(body)
        {
            this.pressId = pressId;
        }

        // 配置秒数是提前取消的门槛；<= 0 表示不开放提前取消，并非立刻可取消。
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
            // 这个标志说明正处于进防动作，不代表已经能挡伤害；本状态的命中处理仍返回 false。
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
                // 未开放时消费防御命令，但保留新的真实按下身份；系统回防不能覆盖成一次主动尝试。
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
                // 垫步没有长按回防路径：未开放时返回 false，让输入缓冲在有效期内重试。
                return false;
            }
            return true;
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            if (waitForAnim)
            {
                // 见到目标动画后按进度结束；尚未见到时才用兜底秒数，避免请求切换当帧就结束。
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

        // 尚未真正进入防御判定：即使 IsGuarding 为 true，仍把命中交回 ReceiveHit 扣伤害并切新受击。
        public override bool OnHitReceived(HitData hit)
        {
            return false;
        }

        // 以这次按下的释放记录决定去向，避免等待动画期间松键后仍被强制留在防御里。
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
            // 身体侧统一执行叶子切换；这里不自行打开弹反窗口或结算本次按键。
            body.TryChangeToDeflectOrDodge(toDeflect, pressId);
        }
    }

}
