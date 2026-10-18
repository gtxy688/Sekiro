using System;
using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Mgr
{

    // 保存练习模式开关；菜单修改设置，战斗逻辑读取设置。
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

        // 一击崩防只对敌人的弹反、识破或玩家攻击来源生效。
        public static bool ShouldOneHitBreakBoss(
            CharacterBody target,
            PostureBreakSource source,
            CharacterBody instigator = null)
        {
            Load();
            if (!OneHitPostureBreak || target == null)
                return false;
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
