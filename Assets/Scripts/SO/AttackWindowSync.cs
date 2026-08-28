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

    public static bool IsArrowWindow(BossMoveWindow w)
    {
        if (w == null) return false;
        if (w.arrowCues != null && w.arrowCues.Length > 0) return true;
        return !CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses) && w.stateDuration >= 0.8f;
    }

    public static bool EntryUsesArrowNums(BossMoveEntry entry)
    {
        if (entry == null || entry.windows == null) return false;
        bool anyMelee = false;
        bool anyArrow = false;
        for (int i = 0; i < entry.windows.Length; i++)
        {
            BossMoveWindow w = entry.windows[i];
            if (w == null) continue;
            if (CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses))
                anyMelee = true;
            else if (IsArrowWindow(w))
                anyArrow = true;
        }
        return anyArrow && !anyMelee;
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
        CoverDuration(cfg);
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
        CoverDuration(w);
    }

    // 时间轴只保存红条时，stateDuration 可能仍是旧占位值。时长必须盖住判定/连招窗，否则 AttackState 会拒收。
    public static void CoverDuration(AttackConfig cfg)
    {
        if (cfg == null) return;
        if (cfg.ComboWindowEnd < cfg.RecoveryWindowStart)
            cfg.ComboWindowEnd = cfg.RecoveryWindowStart;
        float need = NeededDuration(
            cfg.HitStartTime, cfg.RecoveryWindowStart, cfg.ComboWindowEnd, cfg.hitPulses);
        need = MaxCueTime(need, cfg.sfxCues, cfg.arrowCues);
        if (cfg.StateDuration < need)
            cfg.StateDuration = need;
    }

    public static void CoverDuration(BossMoveWindow w)
    {
        if (w == null) return;
        if (w.comboWindowEnd < w.recoverStart)
            w.comboWindowEnd = w.recoverStart;
        float need = NeededDuration(w.hitStartTime, w.recoverStart, w.comboWindowEnd, w.hitPulses);
        need = MaxCueTime(need, w.sfxCues, w.arrowCues);
        if (w.stateDuration < need)
            w.stateDuration = need;
    }

    static float NeededDuration(float hitStart, float recover, float comboEnd, HitPulse[] pulses)
    {
        float need = hitStart;
        if (recover > need) need = recover;
        if (comboEnd > need) need = comboEnd;
        if (pulses == null) return need;
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (p == null) continue;
            if (p.start > need) need = p.start;
            if (p.end > need) need = p.end;
        }
        return need;
    }

    static float MaxCueTime(float need, AttackSfxCue[] sfx, ArrowSpawnCue[] arrows)
    {
        if (sfx != null)
        {
            for (int i = 0; i < sfx.Length; i++)
            {
                if (sfx[i] != null && sfx[i].time > need)
                    need = sfx[i].time;
            }
        }
        if (arrows != null)
        {
            for (int i = 0; i < arrows.Length; i++)
            {
                if (arrows[i] != null && arrows[i].time > need)
                    need = arrows[i].time;
            }
        }
        return need;
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
            HitPulse copy = p.Clone();
            copy.start = start;
            copy.end = end;
            buf[n++] = copy;
        }

        HitPulse[] result = new HitPulse[n];
        for (int i = 0; i < n; i++)
            result[i] = buf[i];
        return result;
    }
}
