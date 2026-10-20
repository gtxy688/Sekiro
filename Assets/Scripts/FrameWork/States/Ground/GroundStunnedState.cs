using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // StunnedState 的受击表现子状态：进入时选动画，更新时计算倒地和可取消时机。
    // 玩家轻受击结束直接回地面待机，中/重倒地结束先进入 StandingState 起身；Boss 按 HurtContext 恢复。
    public class GroundStunnedState : BaseState
    {
        // 动画请求已接受、但还没观察到开头时使用的兜底秒数；它不是输入取消开放时间。
        private const float HeavyFallbackDuration = 2.5f;

        // stunTimer 从本次反应重新进入时计时；连续受击若需要刷新，也会重置它。
        private float stunTimer;
        private readonly HurtContext context;
        private readonly bool useHitGrade;
        private HitGrade grade;
        // lightRepeat 选择连续轻受击表现；heavyRepeat 选择 Mid/Heavy 再受 Heavy 时的重复重击动画。
        private bool lightRepeat;
        private bool heavyRepeat;
        private string animName;
        private float duration;
        // waitForAnim 表示播放请求成功；seenStart 才表示确实见过目标动画的前半段。
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

        // 上层 StunnedState 查询此值：玩家已落地，且该等级的配置秒数已到，才接受垫步取消。
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

        // 仅 Mid 倒地前段能接专用 MidToGuard；过了落地结束点就不再开放这条提前起身路径。
        public bool CanMidToGuard
        {
            get
            {
                if (!useHitGrade || grade != HitGrade.Mid || heavyRepeat) return false;
                float end = body.Config.HurtMidFallEndTime;
                return stunTimer < end;
            }
        }

        // 轻受击（含连续轻受击）不必等硬直结束就能转防御，但必须已落地。
        public bool CanLightGuardCancel
        {
            get
            {
                if (!useHitGrade || !body.IsGrounded) return false;
                return grade == HitGrade.Light && !heavyRepeat;
            }
        }

        // StunnedState 在垫步取消时据此决定是否使用倒地专用的固定时间融合。
        public bool IsKnockdown => grade == HitGrade.Mid || grade == HitGrade.Heavy || heavyRepeat;

        // 供倒地追击判断：区分“倒地动画已经开始”与“确实落到地上并躺着”。
        // OnUpdate 将结果写入 body.IsKnockedDown，供外部战斗逻辑读取。
        public bool IsInKnockdownWindow()
        {
            if (!useHitGrade || !IsKnockdown || !body.IsGrounded) return false;
            // Repeat 分支接地后直接视为倒地，不再等待 HurtHeavyFallEndTime。
            if (heavyRepeat) return true;
            if (grade == HitGrade.Mid)
            {
                float fallEnd = body.Config.HurtMidFallEndTime;
                return stunTimer >= fallEnd;
            }
            float heavyFall = body.Config.HurtHeavyFallEndTime;
            return stunTimer >= heavyFall;
        }

        // 读角色配置映射出的开放秒数；这里只决定能否主动取消，不负责决定整段动画何时结束。
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
                // HitReactionUtil 负责配置名与备用动画的选择，本状态只负责播放和恢复时机。
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

            // Boss 仅重受击等待动画；其他 HurtContext 使用普通受击时长。
            animName = body.ResolveHurtAnim(context);
            bool bossPlayed = AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
            waitForAnim = context == HurtContext.Heavy && bossPlayed;
            duration = waitForAnim ? HeavyFallbackDuration : body.StunDuration;
            if (!bossPlayed)
            {
                AnimUtil.TryCrossFade(body.Animator, body.ResolveHurtAnim(HurtContext.Normal), 0.05f);
            }
        }

        // 父状态已经扣完本次伤害，这里只决定是否换动画并重新计时。
        // Light 接任何等级都刷新；Mid 只被 Heavy 升级刷新；Heavy 仅同级 Heavy 刷新为躺地重复反应。
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

            if (waitForAnim)
            {
                AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if (AnimUtil.IsPlaying(info, animName))
                {
                    // Animator 切换当帧可能仍报告旧进度；见到开头后才允许用结尾判定恢复。
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

                // 未正常观察到结尾时按计时兜底；看到开头后 duration 可更新为实际动画长度。
                if (stunTimer >= duration)
                    FinishStun();
                return;
            }

            if (stunTimer >= duration)
                FinishStun();
        }

        // 受击父状态在这里整体退出；倒地反应结束还需起身，所以不能一律直接回 Idle。
        void FinishStun()
        {
            body.IsKnockedDown = false;
            bool knockdown = useHitGrade && IsKnockdown;
            if (knockdown)
                body.EnterGrounded(new StandingState(body), "stunned: knockdown animation finished");
            else
                body.EnterGrounded("stunned: light animation finished");
        }

        // 被垫步/防御等提前打断时也要撤掉倒地标志，不能只在自然结束路径清理。
        public override void OnExit()
        {
            body.IsKnockedDown = false;
        }
    }

}
