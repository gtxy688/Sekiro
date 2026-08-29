
    public static void ApplyCombatNumbers(BossMoveTable t)
    {
        if (t == null || t.moves == null) return;
        for (int i = 0; i < t.moves.Length; i++)
            FillEntryCombat(t.moves[i]);
    }

    static void FillEntryCombat(BossMoveEntry entry)
    {
        if (entry == null) return;
        bool arrowEntry = AttackWindowSync.EntryUsesArrowNums(entry);
        if (!HasAnyCombatOverride(entry))
            AttackCombatResolve.DefaultCombat(entry.hitGrade, arrowEntry, out entry.baseDamage, out entry.postureDamage);

        FillWindowsCombat(entry, entry.windows);
        if (entry.sequences == null) return;
        for (int s = 0; s < entry.sequences.Length; s++)
        {
            if (entry.sequences[s] == null) continue;
            FillWindowsCombat(entry, entry.sequences[s].windows);
        }
    }

    static bool HasAnyCombatOverride(BossMoveEntry entry)
    {
        if (entry == null) return false;
        if (HasWindowOverrides(entry.windows)) return true;
        if (entry.sequences == null) return false;
        for (int i = 0; i < entry.sequences.Length; i++)
        {
            if (entry.sequences[i] != null && HasWindowOverrides(entry.sequences[i].windows))
                return true;
        }
        return false;
    }

    static bool HasWindowOverrides(BossMoveWindow[] windows)
    {
        if (windows == null) return false;
        for (int i = 0; i < windows.Length; i++)
        {
            BossMoveWindow w = windows[i];
            if (w == null) continue;
            if (w.overrideCombat) return true;
            if (w.hitPulses != null)
            {
                for (int p = 0; p < w.hitPulses.Length; p++)
                    if (w.hitPulses[p] != null && w.hitPulses[p].overrideCombat) return true;
            }
            if (w.arrowCues != null)
            {
                for (int a = 0; a < w.arrowCues.Length; a++)
                    if (w.arrowCues[a] != null && w.arrowCues[a].overrideCombat) return true;
            }
        }
        return false;
    }

    static void FillWindowsCombat(BossMoveEntry entry, BossMoveWindow[] windows)
    {
        if (entry == null || windows == null) return;
        for (int i = 0; i < windows.Length; i++)
        {
            BossMoveWindow w = windows[i];
            if (w == null) continue;
            bool melee = AttackWindowSync.CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses);
            bool arrow = AttackWindowSync.IsArrowWindow(w);
            if (!melee && !arrow) continue;

            if (w.overrideCombat && w.hitGrade == entry.hitGrade)
                w.overrideCombat = false;
            if (w.overrideCombat)
                AttackCombatResolve.DefaultCombat(w.hitGrade, arrow, out w.baseDamage, out w.postureDamage);

            HitGrade inheritGrade = w.overrideCombat ? w.hitGrade : entry.hitGrade;

            if (w.hitPulses != null)
            {
                for (int p = 0; p < w.hitPulses.Length; p++)
                {
                    HitPulse pulse = w.hitPulses[p];
                    if (pulse == null) continue;
                    if (pulse.overrideCombat && pulse.hitGrade == inheritGrade)
                        pulse.overrideCombat = false;
                    if (pulse.overrideCombat)
                        AttackCombatResolve.DefaultCombat(pulse.hitGrade, false, out pulse.baseDamage, out pulse.postureDamage);
                }
            }

            if (w.arrowCues != null)
            {
                for (int a = 0; a < w.arrowCues.Length; a++)
                {
                    ArrowSpawnCue cue = w.arrowCues[a];
                    if (cue == null) continue;
                    if (cue.overrideCombat && cue.hitGrade == inheritGrade)
                        cue.overrideCombat = false;
                    if (cue.overrideCombat)
                        AttackCombatResolve.DefaultCombat(cue.hitGrade, true, out cue.baseDamage, out cue.postureDamage);
                }
            }
        }
    }
