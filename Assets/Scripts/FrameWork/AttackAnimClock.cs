using UnityEngine;

namespace ARPG.FrameWork
{

    public static class AttackAnimClock
    {
        public const int Layer = 0;

        public static float ReadSeconds(Animator animator, string animName)
        {
            if (animator == null || string.IsNullOrEmpty(animName))
                return 0f;

            int hash = Animator.StringToHash(animName);

            if (animator.IsInTransition(Layer))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(Layer);
                if (next.shortNameHash == hash)
                    return SecondsFrom(next);
            }

            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(Layer);
            if (current.shortNameHash == hash)
                return SecondsFrom(current);

            return 0f;
        }

        static float SecondsFrom(AnimatorStateInfo info)
        {
            if (info.length <= 0.0001f)
                return 0f;
            return info.normalizedTime * info.length;
        }
    }

}
