using UnityEngine;

// 弦一郎默认招式表。由 Editor 按钮写入 BossMoveTable，不要运行时调用。
// 与 Assets/SO/Boss/GenichiroMoveTable.asset 同步：菜单 ARPG/Sync GenichiroMoveCatalog from Move Table
public static class GenichiroMoveCatalog
{
    static BossAnimSequence Seq(params string[] states)
    {
        return new BossAnimSequence { states = states };
    }

    static BossAnimSequence SeqWin(string[] states, BossMoveWindow[] windows)
    {
        return new BossAnimSequence { states = states, windows = windows };
    }

    static HitPulse P(float start, float end, HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f)
    {
        return new HitPulse
        {
            start = start, end = end, hitGrade = grade, overrideCombat = ov,
            baseDamage = dmg, postureDamage = posture
        };
    }

    static ArrowSpawnCue A(float time, HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f)
    {
        return new ArrowSpawnCue
        {
            time = time, hitGrade = grade, overrideCombat = ov,
            baseDamage = dmg, postureDamage = posture
        };
    }

    static BossMoveWindow Win(
        float hitStart, float recover, float comboEnd, float duration,
        HitPulse[] pulses = null, ArrowSpawnCue[] arrows = null,
        PerilousType perilous = PerilousType.None,
        AttackHitboxSlot slot = AttackHitboxSlot.Weapon,
        float rotateEnd = 0.35f, float transition = 0.1f,
        HitGrade grade = HitGrade.Light, bool ov = false,
        int dmg = 0, float posture = 0f, float knockback = 0f,
        bool waitAnimEnd = false)
    {
        var w = new BossMoveWindow
        {
            hitStartTime = hitStart,
            recoverStart = recover,
            comboWindowEnd = comboEnd,
            stateDuration = duration,
            rotateEnd = rotateEnd,
            transitionDuration = transition,
            perilous = perilous,
            hitboxSlot = slot,
            hitPulses = pulses ?? new HitPulse[0],
            arrowCues = arrows ?? new ArrowSpawnCue[0],
            sfxCues = new AttackSfxCue[0],
            hitGrade = grade,
            overrideCombat = ov,
            baseDamage = dmg,
            postureDamage = posture,
            knockback = knockback,
            waitAnimEnd = waitAnimEnd
        };
        AttackWindowSync.CoverDuration(w);
        return w;
    }

    static BossMoveEntry Move(
        string id, BossMoveLayer layer,
        float min, float max, float weight, float cooldown,
        BossAnimSequence[] sequences, BossMoveWindow[] windows,
        PerilousType perilous = PerilousType.None,
        BossMoveExtra extra = BossMoveExtra.None,
        int dmg = 10, float posture = 10f,
        HitGrade grade = HitGrade.Light, float knockback = 0f)
    {
        return new BossMoveEntry
        {
            id = id,
            layer = layer,
            minRange = min,
            maxRange = max,
            weight = weight,
            cooldown = cooldown,
            sequences = sequences,
            windows = windows,
            perilous = perilous,
            extra = extra,
            baseDamage = dmg,
            postureDamage = posture,
            hitGrade = grade,
            knockback = knockback
        };
    }

    public static void Apply(BossMoveTable t)
    {
        t.kengekiMaxRange = 2.5f;
        t.postureLowThreshold = 0f;
        t.air5HeavyInterruptChance = 0.5f;
        t.moves = new[]
        {
            Move("Bow_ThenSlash", BossMoveLayer.Active, 7f, 99f, 600f, 6f,
                new[] { Seq("Bow_Shot", "3015") },
                new[] { Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.7491985f) }, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f), Win(1.0820183f, 1.2035457f, 1.2035457f, 1.8f, new[] { P(1.0820183f, 1.2035457f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Bow_Shot", BossMoveLayer.Active, 7f, 99f, 200f, 5f,
                new[] { Seq("Bow_Shot") },
                new[] { Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.7455411f) }) },
                PerilousType.None, BossMoveExtra.None, dmg: 15, posture: 15f, grade: HitGrade.Mid),
            Move("Slash_Rush2", BossMoveLayer.Active, 5f, 99f, 300f, 6f,
                new[] { Seq("Slash_Rush2") },
                new[] { Win(0.75000054f, 1.2811115f, 1.36f, 2.9333334f, new[] { P(0.75000054f, 0.9248136f), P(1.0551845f, 1.2811115f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Slash_RushThenBow", BossMoveLayer.Active, 5f, 7f, 100f, 8f,
                new[] { Seq("Kengeki_Heavy", "3011") },
                new[] { Win(1.288518f, 1.8520359f, 1.8520359f, 2.1f, new[] { P(1.288518f, 1.8520359f) }), Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(1.0510972f) }, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f) }, PerilousType.None, BossMoveExtra.None),
            Move("Boat", BossMoveLayer.Active, 3f, 7f, 700f, 6f,
                new[] { Seq("Boat1", "Boat2") },
                new[] { Win(1.8537953f, 3.394523f, 3.394523f, 3.394523f, new[] { P(1.8537953f, 2.118609f), P(2.2149043f, 2.4386997f), P(2.7625844f, 2.9973044f), P(3.1545274f, 3.394523f) }), Win(0.15717596f, 1.5150914f, 1.5150914f, 2.4f, new[] { P(0.15717596f, 0.30958363f), P(0.4535649f, 0.5867594f), P(1.261897f, 1.5150914f) }, null, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f) }, PerilousType.None, BossMoveExtra.PostureLow),
            Move("Slash_Double", BossMoveLayer.Active, 3f, 5f, 10f, 4f,
                new[] { Seq("Slash_Double") },
                new[] { Win(0.8066667f, 1.3194427f, 1.3194427f, 1.8f, new[] { P(0.8066667f, 0.9394445f), P(1.2030542f, 1.3194427f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Slash_Heavy", BossMoveLayer.Active, 3f, 5f, 30f, 5f,
                new[] { Seq("Slash_Heavy") },
                new[] { Win(0.9875006f, 1.1259255f, 1.25f, 2f, new[] { P(0.9875006f, 1.1259255f) }) },
                PerilousType.None, BossMoveExtra.None, dmg: 15, posture: 15f, grade: HitGrade.Mid),
            Move("Slash_SpinElbow", BossMoveLayer.Active, 0f, 5f, 15f, 6f,
                new[] { Seq("Slash_Spin", "Elbow") },
                new[] { Win(0.56037045f, 0.9129629f, 1.0300001f, 1.6f, new[] { P(0.56037045f, 0.9129629f) }, null, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f), Win(1.1158785f, 1.2292604f, 1.2292604f, 1.4f, new[] { P(1.1158785f, 1.2292604f) }, null, PerilousType.Grab, AttackHitboxSlot.Elbow, grade: HitGrade.Heavy, ov: true, dmg: 25, posture: 25f) }, PerilousType.None, BossMoveExtra.None),
            Move("Slash_StepTurn", BossMoveLayer.Active, 0f, 3f, 15f, 4f,
                new[] { Seq("Slash_StepTurn") },
                new[] { Win(0.9033328f, 1.2358283f, 1.2366664f, 1.8f, new[] { P(0.9033328f, 1.2358283f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kick", BossMoveLayer.Active, 0f, 3f, 30f, 5f,
                new[] { Seq("Attack_Slash", "Kick") },
                new[] { Win(0.88175905f, 1.0816699f, 1.0816699f, 1.2f, new[] { P(0.88175905f, 1.0816699f) }), Win(0.43222204f, 0.5958335f, 0.91999996f, 1.4f, new[] { P(0.43222204f, 0.5958335f) }, null, PerilousType.None, AttackHitboxSlot.Kick, grade: HitGrade.Heavy, ov: true, dmg: 25, posture: 25f) }, PerilousType.None, BossMoveExtra.None),
            Move("JumpThrust", BossMoveLayer.Active, 0f, 5f, 100f, 8f,
                new[] { Seq("JumpThrust", "Thrust") },
                new[] { Win(1.1256675f, 1.2575663f, 99f, 99f, new[] { P(1.1256675f, 1.2575663f) }, null, rotateEnd: 1f), Win(1.034959f, 1.2893054f, 1.4499999f, 2f, new[] { P(1.034959f, 1.2893054f) }, null, PerilousType.Thrust, AttackHitboxSlot.Weapon) },
                PerilousType.None, BossMoveExtra.ConsecutiveParry2, dmg: 25, posture: 25f, grade: HitGrade.Heavy),
            Move("Jump_Danger", BossMoveLayer.Active, 0f, 5f, 100f, 8f,
                new[] { Seq("Jump_Danger") },
                new[] { Win(0.9012096f, 1.0940335f, 1.45f, 4f, new[] { P(0.9012096f, 1.0940335f) }, null, PerilousType.Thrust, AttackHitboxSlot.Weapon, waitAnimEnd: true) },
                PerilousType.Thrust, BossMoveExtra.PlayerKnockedDown, dmg: 25, posture: 25f, grade: HitGrade.Heavy),
            Move("Bow_Air5", BossMoveLayer.Active, 0f, 3f, 30f, 10f,
                new[] { Seq("Dodge_Back", "Bow_Air5") },
                new[] { Win(0.55f, 0.55f, 0.55f, 0.55f, null, null, rotateEnd: 0.15f, transition: 0.08f), Win(4.5f, 4.5f, 4.5f, 4.5f, null, new[] { A(0.61111164f), A(0.8486117f), A(1.0773154f), A(1.5577377f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Bow_Heavy", BossMoveLayer.Interrupt, 0f, 99f, 1f, 8f,
                new[] { Seq("Bow_Heavy") },
                new[] { Win(3f, 3f, 3f, 3f, null, new[] { A(1.5171292f) }) },
                PerilousType.None, BossMoveExtra.None, dmg: 20, posture: 20f, grade: HitGrade.Heavy),
            Move("Kengeki_Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 40f, 0.5f,
                new[] { Seq("3050"), Seq("3055"), Seq("3065"), Seq("3071"), Seq("3076") },
                new[] { Win(0.3614563f, 0.5538897f, 0.975f, 1.5f, new[] { P(0.3614563f, 0.5538897f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_Double", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 3f,
                new[] { Seq("Kengeki_Double") },
                new[] { Win(0.98000085f, 1.47f, 1.47f, 1.8f, new[] { P(0.98000085f, 1.0650002f), P(1.3162501f, 1.47f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_Thrust", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Kengeki_Thrust") },
                new[] { Win(1.034959f, 1.2893054f, 1.4499999f, 2f, new[] { P(1.034959f, 1.2893054f) }, null, grade: HitGrade.Heavy) },
                PerilousType.Thrust, BossMoveExtra.None, dmg: 25, posture: 25f, grade: HitGrade.Heavy),
            Move("Kengeki_Heavy", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
                new[] { Seq("Step_L", "Kengeki_Heavy"), Seq("Step_R", "Kengeki_Heavy") },
                new[] { Win(0.45f, 0.45f, 0.45f, 0.45f, null, null, rotateEnd: 0.15f, transition: 0.08f), Win(1.4249995f, 1.8338841f, 1.8338841f, 1.8338841f, new[] { P(1.4249995f, 1.8338841f) }, null, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_Bow", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 5f,
                new[] { Seq("3031", "3019", "3029"), SeqWin(new[] { "3031", "3036" }, new[] { Win(0.8f, 0.8f, 0.8f, 0.81481516f, null, new[] { A(0.81481516f) }, rotateEnd: 0.15f, transition: 0.08f, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f), Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.67263913f), A(0.91763914f) }) }) },
                new[] { Win(0.81481516f, 0.81481516f, 0.81481516f, 0.81481516f, null, new[] { A(0.80438864f) }, rotateEnd: 0.15f, transition: 0.08f, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f), Win(1.3062497f, 1.5613418f, 1.8f, 1.8f, new[] { P(1.3062497f, 1.5613418f) }), Win(0.27590698f, 0.75211066f, 1.8f, 1.8f, new[] { P(0.27590698f, 0.43349934f), P(0.58766615f, 0.75211066f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_Air5", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 8f,
                new[] { Seq("Dodge_Back", "Bow_Air5") },
                new[] { Win(0.55f, 0.55f, 0.55f, 0.55f, null, null, rotateEnd: 0.15f, transition: 0.08f), Win(4.5f, 4.5f, 4.5f, 4.5f, null, new[] { A(0.6069443f), A(0.84376293f), A(1.0741599f), A(1.5586642f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Boat_Full", BossMoveLayer.Kengeki, 0f, 2.5f, 80f, 8f,
                new[] { Seq("Boat_Full") },
                new[] { Win(1.6249937f, 4.7594366f, 4.7594366f, 4.7594366f, new[] { P(1.6249937f, 2.1034641f), P(2.2397008f, 2.4489942f), P(2.7723362f, 3.0251124f), P(3.1605282f, 3.3230271f), P(3.4674706f, 3.5874705f), P(3.7021914f, 3.8747108f), P(4.5066576f, 4.7594366f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_Bow2Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3018", "3015") },
                new[] { Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.8805106f), A(1.1501409f) }), Win(1.0520815f, 1.2534696f, 1.2534696f, 1.8f, new[] { P(1.0520815f, 1.2534696f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Kengeki_JumpBow", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("3034", "3036", "3015") },
                new[] { Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.64462954f) }, grade: HitGrade.Mid, ov: true, dmg: 15, posture: 15f), Win(1.6f, 1.6f, 1.6f, 1.6f, null, new[] { A(0.67263913f), A(0.91763914f) }), Win(0.98958087f, 1.2499986f, 1.2986075f, 1.8f, new[] { P(0.98958087f, 1.2499986f) }) }, PerilousType.None, BossMoveExtra.None),
            Move("Bow_AirHeavy", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
                new[] { Seq("Bow_AirHeavy") },
                new[] { Win(2.2f, 2.2f, 2.2f, 2.2f, null, new[] { A(1.5587976f) }, grade: HitGrade.Heavy) },
                PerilousType.None, BossMoveExtra.None, dmg: 20, posture: 20f, grade: HitGrade.Heavy)
        };
        ApplyCombatNumbers(t);
    }

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

}
