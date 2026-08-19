using UnityEngine;

// Animator 短名匹配：子状态机里 IsName("Walk") 会对不上全路径，shortNameHash 只认状态短名
public static class AnimUtil
{
    public static bool IsPlaying(AnimatorStateInfo info, string shortName)
    {
        return info.shortNameHash == Animator.StringToHash(shortName);
    }
}
