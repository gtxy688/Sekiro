using UnityEngine;
using System.Collections.Generic;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 被完美弹反 / 被识破后的硬直：攻击者被打断，播指定 Clip，动画结束回待机。
    // 由 CharacterBody.ForceParryStun / ForceMikiriStun 强切（顶层走 GroundedState 初始子状态）。
    public class ParriedState : BaseState
    {
        private const string DefaultAnim = "Deflected";
        private const float ExitNormalizedTime = 0.95f;
        private readonly List<AnimatorClipInfo> recoveryClips = new List<AnimatorClipInfo>(1);

        private readonly string animName;
        private readonly bool freezeYawAfterExit;
        private float timer;
        private float duration;
        private float enterTime;
        private bool hasSeenAnim;
        private bool triedNestedPath;

        public ParriedState(CharacterBody body, string animName = null, bool freezeYawAfterExit = false) : base(body)
        {
            this.animName = string.IsNullOrEmpty(animName) ? DefaultAnim : animName;
            this.freezeYawAfterExit = freezeYawAfterExit;
            duration = body.Config != null ? body.Config.ParriedDuration : 0.35f;
        }

        public override void OnEnter()
        {
            timer = 0f;
            enterTime = Time.time;
            hasSeenAnim = false;
            triedNestedPath = false;
            body.IsParried = true;
            // 钉住打断当下的朝向。识破还会把冻结留到下一招（freezeYawAfterExit）。
            body.FreezeCombatYaw();

            if (!AnimUtil.TryPlay(body.Animator, animName))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少硬直状态：{animName}");
                AnimUtil.TryCrossFade(body.Animator, body.ResolveHurtAnim(HurtContext.Deflected), 0.05f);
            }
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;

            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            AnimatorStateInfo next = body.Animator.IsInTransition(0)
                ? body.Animator.GetNextAnimatorStateInfo(0)
                : info;
            if (!triedNestedPath && !AnimUtil.IsPlaying(info, animName)
                && !AnimUtil.IsPlaying(next, animName) && timer > 0.05f)
            {
                triedNestedPath = true;
                AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
                info = body.Animator.GetCurrentAnimatorStateInfo(0);
                next = body.Animator.IsInTransition(0)
                    ? body.Animator.GetNextAnimatorStateInfo(0)
                    : info;
            }

            bool animDone = false;
            if (AnimUtil.IsPlaying(info, animName) || AnimUtil.IsPlaying(next, animName))
            {
                hasSeenAnim = true;
                if (AnimUtil.IsPlaying(info, animName) && info.normalizedTime >= ExitNormalizedTime)
                    animDone = true;
            }
            else if (hasSeenAnim && !body.Animator.IsInTransition(0))
            {
                animDone = true;
            }

            // 硬直 = max(动画, 配置下限)：ParriedDuration 是最小硬直。
            if (animDone && timer >= duration)
            {
                if (body.EnableStateDebugLog)
                {
                    Debug.Log(
                        $"[CombatTiming][ParriedExit] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                        $"进入时刻={enterTime:F4}s，经过时间={Time.time - enterTime:F4}s，" +
                        $"硬直计时={timer:F3}s，配置下限={duration:F3}s，" +
                        $"仍是被弹动画={AnimUtil.IsPlaying(info, animName)}，" +
                        $"动画长度={info.length:F3}s，进度={info.normalizedTime:F3}，" +
                        $"状态速度={info.speed:F3}，速度倍率={info.speedMultiplier:F3}，" +
                        $"Animator速度={body.Animator.speed:F3}",
                        body);
                }

                body.EnterGrounded("parried: animation finished");
                return;
            }

            if (!hasSeenAnim && timer >= duration)
            {
                if (body.EnableStateDebugLog)
                {
                    Debug.Log(
                        $"[CombatTiming][ParriedExit] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                        $"进入时刻={enterTime:F4}s，经过时间={Time.time - enterTime:F4}s，" +
                        $"硬直计时={timer:F3}s，配置下限={duration:F3}s，原因=未识别动画，兜底恢复",
                        body);
                }
                body.EnterGrounded("parried: fallback duration reached");
            }
        }

        // 供反击安排读取预计恢复时刻；退出条件仍由 OnUpdate 决定。
        // 动画尚未进入、混合树或停播时不猜测，调用方等待下一帧或真实恢复。
        public bool TryGetRecoveryTime(out float recoveryTime)
        {
            recoveryTime = enterTime + duration;
            var animator = body.Animator;
            if (animator == null) return false;
            var info = animator.GetCurrentAnimatorStateInfo(0);
            bool useNext = !AnimUtil.IsPlaying(info, animName) && animator.IsInTransition(0);
            if (useNext) info = animator.GetNextAnimatorStateInfo(0);
            if (!AnimUtil.IsPlaying(info, animName)) return false;

            recoveryClips.Clear();
            if (useNext) animator.GetNextAnimatorClipInfo(0, recoveryClips);
            else animator.GetCurrentAnimatorClipInfo(0, recoveryClips);
            float speed = info.speed * info.speedMultiplier * animator.speed;
            if (recoveryClips.Count != 1 || recoveryClips[0].clip == null || speed <= 0f)
                return false;

            float remaining = Mathf.Max(0f, ExitNormalizedTime - info.normalizedTime)
                * recoveryClips[0].clip.length / speed;
            recoveryTime = Mathf.Max(recoveryTime, Time.time + remaining);
            return true;
        }

        public override bool HandleCommand(ICommand cmd)
        {
            return true;
        }

        public override void OnExit()
        {
            body.IsParried = false;
            if (freezeYawAfterExit)
            {
                return;
            }

            body.ClearCombatYawFrozen();
            body.SetSuppressRootYaw(false);
        }
    }

}
