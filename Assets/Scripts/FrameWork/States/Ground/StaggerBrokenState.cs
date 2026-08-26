using UnityEngine;

// 架势崩解硬直（M9）：
//   玩家崩解 → 播倒地动画，播完立刻恢复（不加额外硬直，不被处决）
//   Boss 攻击崩解 → 处决窗口跟 Stagger_Broken 动画走，播完未处决则立刻清架势条
// 由 CharacterBody.ForcePostureBroken 强切（顶层走 GroundedState 初始子状态），
// 结束由 body.RecoverFromBreak 切顶层，不依赖 parent 引用
public class StaggerBrokenState : BaseState
{
    // Animator 当帧还没切到 clip 时的兜底：玩家 Stagger_Broken 约 75 帧 / 30fps
    private const float FallbackDuration = 2.5f;

    private string animName;
    private float timer;
    private float duration;
    private bool triedNestedPath;
    private bool seenStart;

    public StaggerBrokenState(CharacterBody body) : base(body)
    {
    }

    public override void OnEnter()
    {
        timer = 0f;
        triedNestedPath = false;
        seenStart = false;
        duration = FallbackDuration;
        animName = body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_Broken)
            ? body.Config.HurtAnim_Broken : "Hurt_Ground";

        if (body.Animator == null)
        {
            return;
        }

        if (!AnimUtil.TryPlay(body.Animator, animName))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少崩解状态：{animName}");
            duration = 0.5f;
        }
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (body.Animator == null)
        {
            if (timer >= duration)
            {
                FinishBreak();
            }
            return;
        }

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
            // 等 Animator 吃到 OnEnter 的 Play；仍对不上再解析一次子状态机路径
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
        return true;
    }

    private void FinishBreak()
    {
        body.RecoverFromBreak();
    }
}
