using UnityEngine;

// 把表行的一段烤成运行时 AttackConfig。HideAndDontSave，不写进工程。
public static class BossAttackBaker
{
    public static AttackConfig Bake(BossMoveEntry entry, string animName, BossMoveWindow w)
    {
        AttackConfig cfg = ScriptableObject.CreateInstance<AttackConfig>();
        cfg.hideFlags = HideFlags.HideAndDontSave;
        cfg.AnimName = animName;
        cfg.TransitionDuration = w.transitionDuration;
        cfg.BaseDamage = entry.baseDamage;
        cfg.PostureDamage = entry.postureDamage;
        cfg.Knockback = entry.knockback;
        bool canHit = w.hitStartTime < w.stateDuration;
        cfg.Perilous = canHit ? entry.perilous : PerilousType.None;
        cfg.HitStartTime = w.hitStartTime;
        cfg.RecoveryWindowStart = w.recoverStart;
        cfg.ComboWindowEnd = w.comboWindowEnd;
        cfg.StateDuration = w.stateDuration;
        cfg.AllowRotation = true;
        cfg.RotationSpeed = 720f;
        cfg.RotationWindowEnd = w.rotateEnd;
        cfg.NextCombo = null;
        if (w.hitPulses != null && w.hitPulses.Length > 0)
        {
            cfg.hitPulses = new HitPulse[w.hitPulses.Length];
            for (int i = 0; i < w.hitPulses.Length; i++)
            {
                HitPulse src = w.hitPulses[i];
                if (src == null) continue;
                cfg.hitPulses[i] = new HitPulse { start = src.start, end = src.end };
            }
        }
        if (w.sfxCues != null && w.sfxCues.Length > 0)
        {
            cfg.sfxCues = new AttackSfxCue[w.sfxCues.Length];
            for (int i = 0; i < w.sfxCues.Length; i++)
            {
                AttackSfxCue src = w.sfxCues[i];
                if (src == null) continue;
                cfg.sfxCues[i] = new AttackSfxCue { time = src.time, clip = src.clip };
            }
        }
        return cfg;
    }
}
