using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 播放架势崩解倒地动画，结束后解除崩解；Boss 在此期间可被普通忍杀。
    // 达到配置时刻后可取消到格挡或垫步，并同步清除崩解数值。
    public class StaggerBrokenState : BaseState
    {
        // Animator 当帧还没切到 clip 时的兜底：玩家 Stagger_Broken 约 75 帧 / 30fps
        private const float FallbackDuration = 2.5f;

        private string animName;
        private float timer;
        private float duration;
        private bool triedNestedPath;
        private bool seenStart;

        public bool CanDeflectOrDodgeCancel
        {
            get
            {
                float open = body.BrokenDeflectDodgeOpenTime;
                return open > 0f && timer >= open;
            }
        }

        public StaggerBrokenState(CharacterBody body) : base(body)
        {
        }

        public override void OnEnter()
        {
            timer = 0f;
            triedNestedPath = false;
            seenStart = false;
            duration = FallbackDuration;
            animName = !string.IsNullOrEmpty(body.Config.HurtAnim_Broken)
                ? body.Config.HurtAnim_Broken : "Hurt_Ground";

            if (!AnimUtil.TryPlay(body.Animator, animName))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少崩解状态：{animName}");
                duration = 0.5f;
            }
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, animName))
            {
                // Play(0) 当帧可能仍带着上一圈末尾的 normalizedTime>=0.99。
                // 必须先看到开头，否则会立刻 RecoverFromBreak，后续连段被 StunnedState 二次受击全吞。
                if (info.normalizedTime < 0.5f)
                {
                    seenStart = true;
                    if (info.length > 0.05f)
                    {
                        duration = info.length;
                    }
                }

                if (seenStart && info.normalizedTime >= 0.99f && !body.Animator.IsInTransition(0))
                {
                    FinishBreak();
                    return;
                }
            }
            else if (!triedNestedPath && timer > 0.05f)
            {
                // 等待进入请求生效，仍未匹配时再尝试播放一次。
                triedNestedPath = true;
                AnimUtil.TryPlay(body.Animator, animName);
            }

            // 短名对不上或 clipInfo 为空时，仍按兜底时长结束，避免满条卡死。
            if (timer >= duration)
            {
                FinishBreak();
            }
        }

        public override bool HandleCommand(ICommand cmd)
        {
            // 达到取消时刻后可切格挡或垫步；系统回防的 PressId 为 0，不开主动弹反窗口。
            if (cmd is DeflectCommand || cmd is DodgeCommand)
            {
                int pressId = cmd is DeflectCommand deflectCmd ? deflectCmd.PressId : 0;
                if (CanDeflectOrDodgeCancel &&
                    body.TryChangeToDeflectOrDodge(cmd is DeflectCommand, pressId))
                {
                    // 架势条同步清空（与 RecoverFromBreak 一致），留在同一个 GroundedState 里只换子状态，
                    // 不走 RecoverFromBreak 重建顶层（会掐断垫步/抬刀的进场）。
                    body.ClearPostureBreak();
                    return true;
                }

                // 窗口未到 / 锁定期间未实际开启窗口：不消耗，留缓冲重试（格挡键可长按），也不记空按
                return false;
            }

            return true;
        }

        private void FinishBreak()
        {
            body.RecoverFromBreak();
        }
    }

}
