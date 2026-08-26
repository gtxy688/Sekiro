using UnityEngine;

public static class AttackWindowSync
{
    // 短于约一帧的红条是时间轴「缩到最小」的假窗，进招第 0 帧仍会开刀。
    public const float MinMeleeHitSpan = 0.02f;

    public static bool CanMeleeHit(float hitStart, float recover, HitPulse[] pulses)
    {
        if (pulses != null && pulses.Length > 0)
        {
            for (int i = 0; i < pulses.Length; i++)
            {
                HitPulse p = pulses[i];
                if (p == null) continue;
                if (p.end - p.start >= MinMeleeHitSpan)
                    return true;
            }
            return false;
        }
        return recover - hitStart >= MinMeleeHitSpan;
    }

    public static bool PulseIsMelee(HitPulse p)
    {
        return p != null && p.end - p.start >= MinMeleeHitSpan;
    }

    public static void ApplyNoHit(AttackConfig cfg)
    {
        if (cfg == null) return;
        cfg.hitPulses = new HitPulse[0];
        cfg.HitStartTime = cfg.StateDuration;
        cfg.RecoveryWindowStart = cfg.StateDuration;
        cfg.ComboWindowEnd = cfg.StateDuration;
    }

    public static void ApplyNoHit(BossMoveWindow w)
    {
        if (w == null) return;
        w.hitPulses = new HitPulse[0];
        w.hitStartTime = w.stateDuration;
        w.recoverStart = w.stateDuration;
        w.comboWindowEnd = w.stateDuration;
    }

    public static void ApplyPulses(AttackConfig cfg, HitPulse[] pulses)
    {
        if (cfg == null) return;
        cfg.hitPulses = pulses;
        if (pulses == null || pulses.Length == 0) return;
        HitPulse first = pulses[0];
        HitPulse last = pulses[pulses.Length - 1];
        if (first == null || last == null) return;
        cfg.HitStartTime = first.start;
        cfg.RecoveryWindowStart = last.end;
        if (cfg.ComboWindowEnd < cfg.RecoveryWindowStart)
            cfg.ComboWindowEnd = cfg.RecoveryWindowStart;
    }

    public static void ApplyPulses(BossMoveWindow w, HitPulse[] pulses)
    {
        if (w == null) return;
        w.hitPulses = pulses;
        if (pulses == null || pulses.Length == 0) return;
        HitPulse first = pulses[0];
        HitPulse last = pulses[pulses.Length - 1];
        if (first == null || last == null) return;
        w.hitStartTime = first.start;
        w.recoverStart = last.end;
        if (w.comboWindowEnd < w.recoverStart)
            w.comboWindowEnd = w.recoverStart;
    }

    public static HitPulse[] ClampPulses(HitPulse[] pulses, float clipLength)
    {
        if (pulses == null || pulses.Length == 0)
            return new HitPulse[0];

        float len = Mathf.Max(MinMeleeHitSpan, clipLength);
        HitPulse[] buf = new HitPulse[pulses.Length];
        int n = 0;
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (p == null) continue;
            float start = Mathf.Clamp(p.start, 0f, len);
            float end = Mathf.Clamp(p.end, 0f, len);
            if (end - start < MinMeleeHitSpan)
                continue;
            buf[n++] = new HitPulse { start = start, end = end };
        }

        HitPulse[] result = new HitPulse[n];
        for (int i = 0; i < n; i++)
            result[i] = buf[i];
        return result;
    }
}
