using UnityEngine;

using ARPG.Boss;
namespace ARPG.Configs
{

    // 攻击窗口的共用规则：有效近战脉冲、无判定段以及覆盖所有事件点的时长。
    public static class AttackWindowSync
    {
        // 过短脉冲视为无效，避免把缩成细线的时间轴红条保留为近战判定。
        public const float MinMeleeHitSpan = 0.02f;

        // 近战只认跨度足够的 hitPulses；空数组表示无近战，不回退到节奏窗口。
        public static bool CanMeleeHit(HitPulse[] pulses)
        {
            if (pulses == null || pulses.Length == 0) return false;
            for (int i = 0; i < pulses.Length; i++)
            {
                HitPulse p = pulses[i];
                if (p == null) continue;
                if (p.end - p.start >= MinMeleeHitSpan)
                    return true;
            }
            return false;
        }

        // 有出箭点即按箭处理；无近战且持续至少 0.8 秒的段也使用箭数值。
        public static bool IsArrowWindow(BossMoveWindow w)
        {
            if (w == null) return false;
            if (w.arrowCues != null && w.arrowCues.Length > 0) return true;
            return !CanMeleeHit(w.hitPulses) && w.stateDuration >= 0.8f;
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
                if (CanMeleeHit(w.hitPulses))
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

        // 关闭近战时将节奏窗口一并收至段末，避免留下提前取消或连招入口。
        public static void ApplyNoHit(AttackConfig cfg)
        {
            cfg.hitPulses = new HitPulse[0];
            cfg.HitStartTime = cfg.StateDuration;
            cfg.RecoveryWindowStart = cfg.StateDuration;
            cfg.ComboWindowEnd = cfg.StateDuration;
        }

        public static void ApplyNoHit(BossMoveWindow w)
        {
            w.hitPulses = new HitPulse[0];
            w.hitStartTime = w.stateDuration;
            w.recoverStart = w.stateDuration;
            w.comboWindowEnd = w.stateDuration;
        }

        public static void ApplyPulses(AttackConfig cfg, HitPulse[] pulses)
        {
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

        // 时长必须覆盖节奏窗口和事件点，避免攻击状态提前结束或拒收配置。
        public static void CoverDuration(AttackConfig cfg)
        {
            if (cfg.ComboWindowEnd < cfg.RecoveryWindowStart)
                cfg.ComboWindowEnd = cfg.RecoveryWindowStart;
            float need = NeededDuration(
                cfg.HitStartTime, cfg.RecoveryWindowStart, cfg.ComboWindowEnd, cfg.hitPulses);
            need = MaxCueTime(need, cfg.sfxCues, cfg.arrowCues);
            if (cfg.StateDuration < need)
                cfg.StateDuration = need;
            if (cfg.RotationWindowEnd > cfg.StateDuration)
                cfg.RotationWindowEnd = cfg.StateDuration;
        }

        public static void CoverDuration(BossMoveWindow w)
        {
            if (w.comboWindowEnd < w.recoverStart)
                w.comboWindowEnd = w.recoverStart;
            // 提前切下一段时 combo 占位（如 99）不应把 stateDuration 撑回去。
            float comboForNeed = w.comboWindowEnd;
            if (!w.waitAnimEnd && w.stateDuration > 0.01f && w.comboWindowEnd > w.stateDuration)
                comboForNeed = w.stateDuration;
            float need = NeededDuration(w.hitStartTime, w.recoverStart, comboForNeed, w.hitPulses);
            need = MaxCueTime(need, w.sfxCues, w.arrowCues);
            if (w.stateDuration < need)
                w.stateDuration = need;
            if (w.rotateEnd > w.stateDuration)
                w.rotateEnd = w.stateDuration;
            if (!w.waitAnimEnd && w.comboWindowEnd > w.stateDuration)
                w.comboWindowEnd = w.stateDuration;
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

        // 保存时间轴时复制并裁剪脉冲，丢弃裁剪后过短的区间，保留原数据不变。
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

}
