// 招默认 → 段覆盖 → 刀/箭覆盖。不覆盖时行为与只配招伤害时相同。
public static class AttackCombatResolve
{
    public static void DefaultCombat(HitGrade grade, bool arrow, out int damage, out float posture)
    {
        switch (grade)
        {
            case HitGrade.Mid:
                damage = 15;
                posture = 15f;
                return;
            case HitGrade.Heavy:
                damage = arrow ? 20 : 25;
                posture = arrow ? 20f : 25f;
                return;
            default:
                damage = 10;
                posture = 10f;
                return;
        }
    }

    public static void ApplyWindow(AttackConfig cfg, BossMoveEntry entry, BossMoveWindow w)
    {
        if (cfg == null || entry == null) return;
        if (w != null && w.overrideCombat)
        {
            cfg.BaseDamage = w.baseDamage;
            cfg.PostureDamage = w.postureDamage;
            cfg.Knockback = w.knockback;
            cfg.HitGrade = w.hitGrade;
            return;
        }

        cfg.BaseDamage = entry.baseDamage;
        cfg.PostureDamage = entry.postureDamage;
        cfg.Knockback = entry.knockback;
        cfg.HitGrade = entry.hitGrade;
    }

    // 箭用：直接读招式表，不经过弓段烘焙的 AttackConfig。
    public static void Resolve(
        BossMoveEntry entry,
        BossMoveWindow w,
        out int damage,
        out float posture,
        out float knockback,
        out HitGrade grade)
    {
        if (entry == null)
        {
            damage = 0;
            posture = 0f;
            knockback = 0f;
            grade = HitGrade.Light;
            return;
        }

        if (w != null && w.overrideCombat)
        {
            damage = w.baseDamage;
            posture = w.postureDamage;
            knockback = w.knockback;
            grade = w.hitGrade;
            return;
        }

        damage = entry.baseDamage;
        posture = entry.postureDamage;
        knockback = entry.knockback;
        grade = entry.hitGrade;
    }

    public static void Resolve(
        BossMoveEntry entry,
        BossMoveWindow w,
        ArrowSpawnCue cue,
        out int damage,
        out float posture,
        out float knockback,
        out HitGrade grade)
    {
        if (cue != null && cue.overrideCombat)
        {
            damage = cue.baseDamage;
            posture = cue.postureDamage;
            knockback = cue.knockback;
            grade = cue.hitGrade;
            return;
        }

        Resolve(entry, w, out damage, out posture, out knockback, out grade);
    }

    public static void ApplyPulse(
        AttackConfig cfg,
        HitPulse pulse,
        int fallbackDamage,
        float fallbackPosture,
        float fallbackKnockback,
        HitGrade fallbackGrade)
    {
        if (cfg == null) return;
        if (pulse != null && pulse.overrideCombat)
        {
            cfg.BaseDamage = pulse.baseDamage;
            cfg.PostureDamage = pulse.postureDamage;
            cfg.Knockback = pulse.knockback;
            cfg.HitGrade = pulse.hitGrade;
            return;
        }

        cfg.BaseDamage = fallbackDamage;
        cfg.PostureDamage = fallbackPosture;
        cfg.Knockback = fallbackKnockback;
        cfg.HitGrade = fallbackGrade;
    }
}
