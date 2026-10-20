using UnityEngine;

namespace ARPG.FrameWork
{

    // 将当前攻击动画的进度换算为秒，供命中、音效和出箭窗口共用。
    public static class AttackAnimClock
    {
        public const int Layer = 0;

        public static float ReadSeconds(Animator animator, string animName)
        {
            int hash = Animator.StringToHash(animName);

            // 混合期间优先读目标状态，让新攻击从自己的动画时间开始推进。
            if (animator.IsInTransition(Layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(Layer);
                if (next.shortNameHash == hash)
                    return SecondsFrom(next);
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(Layer);
            if (current.shortNameHash == hash)
                return SecondsFrom(current);

            // 目标攻击尚未成为当前/下一动画时，从 0 等待；不借用上一动作的时间推进新招判定。
            return 0f;
        }

        static float SecondsFrom(AnimatorStateInfo info)
        {
            if (info.length <= 0.0001f)
                return 0f;
            // 归一化进度 × 状态长度，换算成攻击配置使用的动画内秒数；不是全局 Time.time。
            return info.normalizedTime * info.length;
        }
    }

}
