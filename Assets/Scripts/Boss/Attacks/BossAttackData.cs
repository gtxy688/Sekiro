using Sekiro.Core.Data;

namespace Sekiro.Boss.Attacks
{
    /// <summary>
    /// 一阶段招式数据工厂。
    /// 提供弦一郎一阶段 9 种招式的 AttackData 创建方法。
    /// 所有数据来自规格表（boss-ai.md），禁止硬编码。
    /// </summary>
    public static class BossAttackData
    {
        /// <summary>
        /// 获取一阶段全部 9 种招式数据
        /// </summary>
        /// <returns>招式数组</returns>
        public static AttackData[] GetPhase1Attacks()
        {
            return new AttackData[]
            {
                CreateHorizontalSlash(),
                CreateUpwardSlash(),
                CreateDownwardChop(),
                CreateThrust(),
                CreateJumpSlash(),
                CreateSweep(),
                CreateTripleSlash(),
                CreateArrow(),
                CreateBackstep()
            };
        }

        /// <summary>
        /// 横斩 — 基础近战招式，可弹刀
        /// 伤害 100，破韧 15，前摇 10 帧，判定 4 帧，后摇 12 帧
        /// </summary>
        public static AttackData CreateHorizontalSlash()
        {
            return new AttackData
            {
                attackName = "横斩",
                animName = "Boss_HorizontalSlash",
                damage = 100f,
                postureDamage = 15f,
                attackType = AttackType.Normal,
                canBeDeflected = true,
                startupFrames = 10f,
                activeFrames = 4f,
                recoveryFrames = 12f,
                hitboxRadius = 2f,
                vfxName = "BossSlashVFX",
                sfxName = "BossSlashSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 上挑 — 近战招式，可弹刀
        /// 伤害 110，破韧 15，前摇 12 帧，判定 4 帧，后摇 14 帧
        /// </summary>
        public static AttackData CreateUpwardSlash()
        {
            return new AttackData
            {
                attackName = "上挑",
                animName = "Boss_UpwardSlash",
                damage = 110f,
                postureDamage = 15f,
                attackType = AttackType.Normal,
                canBeDeflected = true,
                startupFrames = 12f,
                activeFrames = 4f,
                recoveryFrames = 14f,
                hitboxRadius = 2f,
                vfxName = "BossSlashVFX",
                sfxName = "BossSlashSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 下劈 — 近战招式，可弹刀
        /// 伤害 120，破韧 20，前摇 14 帧，判定 5 帧，后摇 16 帧
        /// </summary>
        public static AttackData CreateDownwardChop()
        {
            return new AttackData
            {
                attackName = "下劈",
                animName = "Boss_DownwardChop",
                damage = 120f,
                postureDamage = 20f,
                attackType = AttackType.Normal,
                canBeDeflected = true,
                startupFrames = 14f,
                activeFrames = 5f,
                recoveryFrames = 16f,
                hitboxRadius = 1.5f,
                vfxName = "BossChopVFX",
                sfxName = "BossChopSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 突刺 — 突刺危字，不可弹刀，需识破
        /// 伤害 150，破韧 25，前摇 16 帧，判定 6 帧，后摇 18 帧
        /// </summary>
        public static AttackData CreateThrust()
        {
            return new AttackData
            {
                attackName = "突刺",
                animName = "Boss_Thrust",
                damage = 150f,
                postureDamage = 25f,
                attackType = AttackType.Thrust,
                canBeDeflected = false,
                startupFrames = 16f,
                activeFrames = 6f,
                recoveryFrames = 18f,
                hitboxRadius = 1f,
                vfxName = "BossThrustVFX",
                sfxName = "BossThrustSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 跳跃劈 — 近战招式，可弹刀
        /// 伤害 130，破韧 20，前摇 20 帧，判定 6 帧，后摇 14 帧
        /// </summary>
        public static AttackData CreateJumpSlash()
        {
            return new AttackData
            {
                attackName = "跳跃劈",
                animName = "Boss_JumpSlash",
                damage = 130f,
                postureDamage = 20f,
                attackType = AttackType.Normal,
                canBeDeflected = true,
                startupFrames = 20f,
                activeFrames = 6f,
                recoveryFrames = 14f,
                hitboxRadius = 2.5f,
                vfxName = "BossJumpSlashVFX",
                sfxName = "BossJumpSlashSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 扫击 — 扫击危字，不可弹刀，需跳跃躲避
        /// 伤害 100，破韧 10，前摇 10 帧，判定 8 帧，后摇 10 帧
        /// </summary>
        public static AttackData CreateSweep()
        {
            return new AttackData
            {
                attackName = "扫击",
                animName = "Boss_Sweep",
                damage = 100f,
                postureDamage = 10f,
                attackType = AttackType.Sweep,
                canBeDeflected = false,
                startupFrames = 10f,
                activeFrames = 8f,
                recoveryFrames = 10f,
                hitboxRadius = 3f,
                vfxName = "BossSweepVFX",
                sfxName = "BossSweepSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 三连斩 — 三段连击近战招式，可弹刀（每段均可弹刀）
        /// 伤害 80+90+100，破韧 10+10+15
        /// 帧数据为第一击数值，动画系统处理三段连击
        /// </summary>
        public static AttackData CreateTripleSlash()
        {
            return new AttackData
            {
                attackName = "三连斩",
                animName = "Boss_TripleSlash",
                damage = 80f,
                postureDamage = 10f,
                attackType = AttackType.Normal,
                canBeDeflected = true,
                startupFrames = 8f,
                activeFrames = 3f,
                recoveryFrames = 10f,
                hitboxRadius = 2f,
                vfxName = "BossTripleSlashVFX",
                sfxName = "BossTripleSlashSFX",
                isRanged = false
            };
        }

        /// <summary>
        /// 射箭 — 远程三连射，每发伤害 60，破韧 10
        /// </summary>
        public static AttackData CreateArrow()
        {
            return new AttackData
            {
                attackName = "射箭",
                animName = "Boss_Arrow",
                damage = 60f,
                postureDamage = 10f,
                attackType = AttackType.Normal,
                canBeDeflected = false,
                startupFrames = 0f,
                activeFrames = 0f,
                recoveryFrames = 0f,
                hitboxRadius = 0f,
                vfxName = "BossArrowVFX",
                sfxName = "BossArrowSFX",
                isRanged = true
            };
        }

        /// <summary>
        /// 后撤步 — 位移招式，无伤害
        /// 前摇 10 帧用于后撤动画
        /// </summary>
        public static AttackData CreateBackstep()
        {
            return new AttackData
            {
                attackName = "后撤步",
                animName = "Boss_Backstep",
                damage = 0f,
                postureDamage = 0f,
                attackType = AttackType.Normal,
                canBeDeflected = false,
                startupFrames = 10f,
                activeFrames = 0f,
                recoveryFrames = 0f,
                hitboxRadius = 0f,
                vfxName = "",
                sfxName = "BossBackstepSFX",
                isRanged = false
            };
        }
    }
}
