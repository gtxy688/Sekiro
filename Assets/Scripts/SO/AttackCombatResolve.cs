// 招默认 → 段覆盖 → 刀覆盖。不覆盖时行为与只配招伤害时相同。
public static class AttackCombatResolve
{
    public static void ApplyWindow(AttackConfig cfg, BossMoveEntry entry, BossMoveWindow w)
    {
        if (cfg == null || entry == null) return;
        if (w != null && w.overrideCombat)
        {
            cfg.BaseDamage = w.baseDamage;
            cfg.PostureDamage = w.postureDamage;
            cfg.Knockback = w.knockback;
            return;
        }

        cfg.BaseDamage = entry.baseDamage;
        cfg.PostureDamage = entry.postureDamage;
        cfg.Knockback = entry.knockback;
    }

    public static void ApplyPulse(
        AttackConfig cfg,
        HitPulse pulse,
        int fallbackDamage,
        float fallbackPosture,
        float fallbackKnockback)
    {
        if (cfg == null) return;
        if (pulse != null && pulse.overrideCombat)
        {
            cfg.BaseDamage = pulse.baseDamage;
            cfg.PostureDamage = pulse.postureDamage;
            cfg.Knockback = pulse.knockback;
            return;
        }

        cfg.BaseDamage = fallbackDamage;
        cfg.PostureDamage = fallbackPosture;
        cfg.Knockback = fallbackKnockback;
    }
}
