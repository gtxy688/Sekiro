using System;
using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Mgr
{

    // 调试/练习用玩法开关。表现层写入，战斗逻辑只读。
    public static class GameplaySettings
    {
        public const string InfiniteHealthKey = "gameplay.infinite_health";
        public const string OneHitPostureBreakKey = "gameplay.one_hit_posture_break";

        public static event Action OnChanged;

        public static bool InfiniteHealth { get; private set; }
        public static bool OneHitPostureBreak { get; private set; }

        private static bool loaded;

        public static void Load()
        {
            if (loaded) return;
            InfiniteHealth = PlayerPrefs.GetInt(InfiniteHealthKey, 0) == 1;
            OneHitPostureBreak = PlayerPrefs.GetInt(OneHitPostureBreakKey, 0) == 1;
            loaded = true;
        }

        public static void SetInfiniteHealth(bool enabled)
        {
            Load();
            if (InfiniteHealth == enabled) return;
            InfiniteHealth = enabled;
            PlayerPrefs.SetInt(InfiniteHealthKey, InfiniteHealth ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        public static void SetOneHitPostureBreak(bool enabled)
        {
            Load();
            if (OneHitPostureBreak == enabled) return;
            OneHitPostureBreak = enabled;
            PlayerPrefs.SetInt(OneHitPostureBreakKey, OneHitPostureBreak ? 1 : 0);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        // 弹反 / 普攻 / 识破：Boss 被玩家打出架势伤害时直接崩防；拼刀等其它来源不算。
        public static bool ShouldOneHitBreakBoss(
            CharacterBody target,
            PostureBreakSource source,
            CharacterBody instigator = null)
        {
            Load();
            // 不再需要判 CombatManager.Instance == null：判断只依赖 target 自己的阵营字段。
            if (!OneHitPostureBreak || target == null)
                return false;
            // 只有敌人吃一键崩防。改判阵营而非比 BossRef：
            // 多 Boss 时这条对所有敌人生效，而不是只对「列表里那一个」生效。
            // 下面已限定只有玩家打出来才算，所以语义是对的。
            if (target.Faction != Faction.Enemy)
                return false;

            if (source == PostureBreakSource.Deflect || source == PostureBreakSource.Mikiri)
                return true;

            return source == PostureBreakSource.Attack
                   && instigator != null
                   && HitReactionUtil.IsPlayer(instigator);
        }
    }

}
