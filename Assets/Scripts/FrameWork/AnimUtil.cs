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
        if (animator == null || string.IsNullOrEmpty(shortName)) return false;
        return animator.HasState(0, Animator.StringToHash(shortName));
    }
}
