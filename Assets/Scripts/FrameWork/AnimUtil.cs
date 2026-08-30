using UnityEngine;

using ARPG.Boss;
using ARPG.Configs;
namespace ARPG.FrameWork
{

    // 状态名全局唯一：用短名哈希在整层查找，不必拼 _Hurt.Hurt_Ground。
    public static class AnimUtil
    {
        public static bool IsPlaying(AnimatorStateInfo info, string shortName)
        {
            return info.shortNameHash == Hash(shortName);
        }

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
            return animator.HasState(layerIndex, Hash(shortName));
        }

        // 资源侧拼写不统一（Mikiri / Miriki），按 Animator 里实际存在的短名选用。
        public static string ResolveState(Animator animator, params string[] aliases)
        {
            if (animator == null || aliases == null) return null;
            for (int i = 0; i < aliases.Length; i++)
            {
                if (HasState(animator, aliases[i])) return aliases[i];
            }
            return null;
        }

        // 不在这里 Animator.Update：崩解常从命中动画事件切入，Update 会重入。
        public static bool TryPlay(Animator animator, string shortName, int layerIndex = 0)
        {
            if (!HasState(animator, shortName, layerIndex)) return false;
            animator.Play(Hash(shortName), layerIndex, 0f);
            return true;
        }

        public static bool TryCrossFade(
            Animator animator,
            string shortName,
            float duration,
            int layerIndex = 0)
        {
            if (!HasState(animator, shortName, layerIndex)) return false;
            animator.CrossFade(Hash(shortName), duration, layerIndex);
            return true;
        }

        public static bool TryCrossFadeInFixedTime(
            Animator animator,
            string shortName,
            float duration,
            int layerIndex = 0,
            float fixedTimeOffset = 0f)
        {
            if (!HasState(animator, shortName, layerIndex)) return false;
            animator.CrossFadeInFixedTime(Hash(shortName), duration, layerIndex, fixedTimeOffset);
            return true;
        }

        // JumpThrust 两段都在 _Danger；仍用 Play 硬切，避免切招后 AttackAnimClock 读不到。
        public static bool TryBeginAttackAnim(
            Animator animator,
            AttackConfig config,
            BossMoveEntry moveEntry)
        {
            if (animator == null || config == null || string.IsNullOrEmpty(config.AnimName))
                return false;

            if (moveEntry != null && moveEntry.id == "JumpThrust")
                return TryPlay(animator, config.AnimName);

            return TryCrossFade(animator, config.AnimName, config.TransitionDuration);
        }

        // 传入 Parent.State 时只取最后一段，与 Animator 短名哈希一致。
        private static int Hash(string name)
        {
            int dot = name.LastIndexOf('.');
            if (dot >= 0 && dot < name.Length - 1)
            {
                name = name.Substring(dot + 1);
            }
            return Animator.StringToHash(name);
        }
    }

}
