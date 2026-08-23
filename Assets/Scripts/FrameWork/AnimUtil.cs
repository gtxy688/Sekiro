using UnityEngine;

// Animator 短名匹配：子状态机里 IsName("Walk") 会对不上全路径，shortNameHash 只认状态短名
public static class AnimUtil
{
    public static bool IsPlaying(AnimatorStateInfo info, string shortName)
    {
        return info.shortNameHash == Animator.StringToHash(shortName);
    }

    // CrossFade 短名：没建状态会静默失败。Boss 没有 IdleToWalk 时用来跳过起步。
    public static bool HasState(Animator animator, string shortName)
    {
        return HasState(animator, shortName, 0);
    }

    public static bool HasState(Animator animator, string shortName, int layerIndex)
    {
        if (animator == null ||
            string.IsNullOrEmpty(shortName) ||
            layerIndex < 0 ||
            layerIndex >= animator.layerCount)
        {
            return false;
        }
        return animator.HasState(layerIndex, Animator.StringToHash(shortName));
    }

    // 不在这里 Animator.Update：崩解常从命中动画事件切入，Update 会重入。
    public static bool TryPlay(Animator animator, string shortName, int layerIndex = 0)
    {
        if (!HasState(animator, shortName, layerIndex))
        {
            return false;
        }

        animator.Play(shortName, layerIndex, 0f);
        return true;
    }
}
