using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 地面受击子状态。玩家按 HitGrade 选动画并处理连续受击；Boss 仍按 HurtContext。
    public class GroundStunnedState : BaseState
    {
        private const float HeavyFallbackDuration = 2.5f;

        private float stunTimer;
        private readonly HurtContext context;
        private readonly bool useHitGrade;
        private HitGrade grade;
        private bool lightRepeat;
        private bool heavyRepeat;
        private string animName;
        private float duration;
        private bool waitForAnim;
        private bool seenStart;

        public GroundStunnedState(
            CharacterBody body,
            HierarchicalState parent,
            HurtContext context,
            bool useHitGrade = false,
            HitGrade grade = HitGrade.Light) : base(body)
        {
            this.context = context;
            this.useHitGrade = useHitGrade;
            this.grade = grade;
        }

        public bool CanDodgeCancel
        {
            get
            {
                if (!useHitGrade || !body.IsGrounded) return false;
                float open = DodgeCancelOpenTime();
                if (open <= 0f) return false;
                return stunTimer >= open;
            }
        }

        public bool CanMidToGuard
        {
            get
            {
                if (!useHitGrade || grade != HitGrade.Mid || heavyRepeat) return false;
                float end = body.Config != null ? body.Config.HurtMidFallEndTime : 0.4f;
                return stunTimer < end;
            }
        }

        // Light（含 Hurt_Light2）全程可抬刀；空中受击落地前不给，没有空中格挡。
        public bool CanLightGuardCancel
        {
            get
            {
                if (!useHitGrade || !body.IsGrounded) return false;
                return grade == HitGrade.Light && !heavyRepeat;
            }
        }

        bool IsKnockdown => grade == HitGrade.Mid || grade == HitGrade.Heavy || heavyRepeat;

        // 已过落地前摇、处于躺地窗口（Jump_Danger 等危字追击用）。
        public bool IsInKnockdownWindow()
        {
            if (!useHitGrade || !IsKnockdown || !body.IsGrounded) return false;
            // Repeat 是躺地循环，一切入即算倒地，不等 HurtHeavyFallEndTime。
            if (heavyRepeat) return true;
            if (grade == HitGrade.Mid)
            {
                float fallEnd = body.Config != null ? body.Config.HurtMidFallEndTime : 0.4f;
                return stunTimer >= fallEnd;
            }
            float heavyFall = body.Config != null ? body.Config.HurtHeavyFallEndTime : 0.5f;
            return stunTimer >= heavyFall;
        }

        float DodgeCancelOpenTime()
        {
            if (grade == HitGrade.Heavy || heavyRepeat)
                return body.HeavyStunDuration;
            if (grade == HitGrade.Mid)
                return body.KnockdownStunDuration;
            return body.StunDuration;
        }

        public override void OnEnter()
        {
            stunTimer = 0f;
            seenStart = false;
            if (useHitGrade)
            {
                animName = HitReactionUtil.UnguardedAnim(body, grade, lightRepeat, heavyRepeat);
                bool played = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
                waitForAnim = played;
                duration = waitForAnim ? HeavyFallbackDuration : body.StunDuration;
                if (!played)
                {
                    animName = HitReactionUtil.UnguardedAnim(body, HitGrade.Light, false, false);
                    AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
                }
                return;
            }

            animName = body.ResolveHurtAnim(context);
            bool bossPlayed = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
            waitForAnim = context == HurtContext.Heavy && bossPlayed;
            duration = waitForAnim ? HeavyFallbackDuration : body.StunDuration;
            if (!bossPlayed)
            {
                AnimUtil.TryCrossFade(body.Animator, body.ResolveHurtAnim(HurtContext.Normal), 0.05f);
            }
        }

        public void ReceiveFollowUpHit(HitData hit)
        {
            if (!useHitGrade) return;
            if (!HitReactionUtil.ShouldRefreshHurt(grade, hit.hitGrade, out bool toHeavyRepeat))
                return;

            if (grade == HitGrade.Light)
                lightRepeat = true;
            grade = toHeavyRepeat ? HitGrade.Heavy : hit.hitGrade;
            heavyRepeat = toHeavyRepeat;
            OnEnter();
        }

        public override void OnUpdate()
        {
            stunTimer += Time.deltaTime;
            body.IsKnockedDown = IsInKnockdownWindow();

            if (waitForAnim && body.Animator != null)
            {
                AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if (AnimUtil.IsPlaying(info, animName))
                {
                    if (info.normalizedTime < 0.5f)
                    {
                        seenStart = true;
                        if (info.length > 0.05f)
                            duration = Mathf.Max(body.StunDuration, info.length);
                    }

                    if (seenStart && info.normalizedTime >= 0.99f && !body.Animator.IsInTransition(0))
                    {
                        FinishStun();
                        return;
                    }
                }

                if (stunTimer >= duration)
                    FinishStun();
                return;
            }

            if (stunTimer >= duration)
                FinishStun();
        }

        void FinishStun()
        {
            body.IsKnockedDown = false;
            bool knockdown = useHitGrade && IsKnockdown;
            if (knockdown)
                body.EnterGrounded(new StandingState(body), "stunned: knockdown animation finished");
            else
                body.EnterGrounded("stunned: light animation finished");
        }

        public override void OnExit()
        {
            body.IsKnockedDown = false;
        }
    }

}
