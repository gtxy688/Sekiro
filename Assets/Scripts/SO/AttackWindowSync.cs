using UnityEngine;

public static class AttackWindowSync
{
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
        if (pulses == null) return null;
        float len = Mathf.Max(0.01f, clipLength);
        HitPulse[] result = new HitPulse[pulses.Length];
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (p == null)
            {
                result[i] = new HitPulse { start = 0f, end = 0.01f };
                continue;
            }
            float start = Mathf.Clamp(p.start, 0f, len);
            float end = Mathf.Clamp(p.end, 0f, len);
            if (start >= end)
                end = Mathf.Min(len, start + 0.01f);
            result[i] = new HitPulse { start = start, end = end };
        }
        return result;
    }
}
