using UnityEngine;

// 把表行的一段烤成运行时 AttackConfig。HideAndDontSave，不写进工程。
public static class BossAttackBaker
{
    public static AttackConfig Bake(BossMoveEntry entry, string animName, BossMoveWindow w)
    {
        AttackConfig cfg = ScriptableObject.CreateInstance<AttackConfig>();
        cfg.hideFlags = HideFlags.HideAndDontSave;
        cfg.name = string.IsNullOrEmpty(animName) ? entry.id : entry.id + "_" + animName;
        cfg.AnimName = animName;
        cfg.TransitionDuration = w.transitionDuration;
        AttackCombatResolve.ApplyWindow(cfg, entry, w);
        cfg.StateDuration = w.stateDuration;
        cfg.WaitAnimEnd = w.waitAnimEnd;
        cfg.AllowRotation = true;
        cfg.RotationSpeed = 1080f;
        // 弓段跟玩家转到出箭结束，不要用短垫步那套 0.15s 转向窗。
        bool bowTrack = (w.arrowCues != null && w.arrowCues.Length > 0)
            || (!AttackWindowSync.CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses)
                && w.stateDuration > 0.8f);
        cfg.RotationWindowEnd = bowTrack ? w.stateDuration : w.rotateEnd;
        cfg.NextCombo = null;

        // NoHit：hitStart>=recover，或只剩 0~0.01 假红条。残窗也烤成时长对齐，AttackState 全程不开刀。
        bool canHit = AttackWindowSync.CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses);
        PerilousType perilous = w.perilous != PerilousType.None ? w.perilous : entry.perilous;
        cfg.Perilous = canHit ? perilous : PerilousType.None;
        cfg.HitboxSlot = canHit ? w.hitboxSlot : AttackHitboxSlot.Weapon;
        if (canHit)
        {
            cfg.HitStartTime = w.hitStartTime;
            cfg.RecoveryWindowStart = w.recoverStart;
            cfg.ComboWindowEnd = w.comboWindowEnd;
            if (w.hitPulses != null && w.hitPulses.Length > 0)
            {
                cfg.hitPulses = new HitPulse[w.hitPulses.Length];
                for (int i = 0; i < w.hitPulses.Length; i++)
                {
                    HitPulse src = w.hitPulses[i];
                    if (src == null) continue;
                    cfg.hitPulses[i] = src.Clone();
                }
            }
        }
        else
        {
            AttackWindowSync.ApplyNoHit(cfg);
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
        if (w.arrowCues != null && w.arrowCues.Length > 0)
        {
            cfg.arrowCues = new ArrowSpawnCue[w.arrowCues.Length];
            for (int i = 0; i < w.arrowCues.Length; i++)
            {
                ArrowSpawnCue src = w.arrowCues[i];
                if (src == null) continue;
                cfg.arrowCues[i] = src.Clone();
            }
        }
        AttackWindowSync.CoverDuration(cfg);
        if (bowTrack || w.waitAnimEnd)
            cfg.RotationWindowEnd = cfg.StateDuration;
        else if (cfg.RotationWindowEnd > cfg.StateDuration)
            cfg.RotationWindowEnd = cfg.StateDuration;
        return cfg;
    }
}
