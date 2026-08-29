#if UNITY_EDITOR
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 把当前 GenichiroMoveTable.asset 的判定时间、等级、出箭等写回 GenichiroMoveCatalog.cs。
public static class GenichiroMoveCatalogExporter
{
    const string AssetPath = "Assets/SO/Boss/GenichiroMoveTable.asset";
    const string CatalogPath = "Assets/Scripts/Boss/GenichiroMoveCatalog.cs";

    [MenuItem("ARPG/Sync GenichiroMoveCatalog from Move Table")]
    public static void SyncFromAsset()
    {
        BossMoveTable table = AssetDatabase.LoadAssetAtPath<BossMoveTable>(AssetPath);
        if (table == null)
        {
            EditorUtility.DisplayDialog("导出失败", $"找不到 {AssetPath}", "确定");
            return;
        }

        string code = BuildCatalogSource(table);
        File.WriteAllText(CatalogPath, code, new UTF8Encoding(false));
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("导出完成", $"已写入 {CatalogPath}\n请检查 diff 后提交。", "确定");
    }

    public static string BuildCatalogSource(BossMoveTable table)
    {
        var sb = new StringBuilder();
        sb.AppendLine("using UnityEngine;");
        sb.AppendLine();
        sb.AppendLine("// 弦一郎默认招式表。由 Editor 按钮写入 BossMoveTable，不要运行时调用。");
        sb.AppendLine("// 与 Assets/SO/Boss/GenichiroMoveTable.asset 同步：菜单 ARPG/Sync GenichiroMoveCatalog from Move Table");
        sb.AppendLine("public static class GenichiroMoveCatalog");
        sb.AppendLine("{");
        AppendHelpers(sb);
        sb.AppendLine("    public static void Apply(BossMoveTable t)");
        sb.AppendLine("    {");
        sb.AppendLine(AssignLine(table.kengekiMaxRange, "        t.kengekiMaxRange = {0};"));
        sb.AppendLine(AssignLine(table.postureLowThreshold, "        t.postureLowThreshold = {0};"));
        sb.AppendLine(AssignLine(table.air5HeavyInterruptChance, "        t.air5HeavyInterruptChance = {0};"));
        sb.AppendLine(AssignLine(table.jumpThrustLife2SweepWeight, "        t.jumpThrustLife2SweepWeight = {0};"));
        sb.AppendLine(AssignLine(table.jumpThrustLife2ThrustWeight, "        t.jumpThrustLife2ThrustWeight = {0};"));
        sb.AppendLine("        t.moves = new[]");
        sb.AppendLine("        {");

        if (table.moves != null)
        {
            for (int i = 0; i < table.moves.Length; i++)
                AppendMove(sb, table.moves[i], i == table.moves.Length - 1);
        }

        sb.AppendLine("        };");
        sb.AppendLine("        ApplyCombatNumbers(t);");
        sb.AppendLine("    }");
        AppendPostApplyMethods(sb);
        sb.AppendLine("}");
        return sb.ToString();
    }

    static void AppendHelpers(StringBuilder sb)
    {
        sb.AppendLine(@"    static BossAnimSequence Seq(params string[] states)
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
");
    }

    static void AppendMove(StringBuilder sb, BossMoveEntry e, bool last)
    {
        sb.Append("            Move(");
        sb.Append(Q(e.id)).Append(", ");
        sb.Append("BossMoveLayer.").Append(e.layer).Append(", ");
        sb.Append(F(e.minRange)).Append(", ").Append(F(e.maxRange)).Append(", ");
        sb.Append(F(e.weight)).Append(", ").Append(F(e.cooldown)).Append(",\n");
        AppendSequences(sb, e);
        sb.Append(",\n");
        AppendWindows(sb, e.windows, "                ");
        sb.Append(")");

        if (e.perilous != PerilousType.None || e.extra != BossMoveExtra.None
            || e.baseDamage != 10 || e.postureDamage != 10f || e.hitGrade != HitGrade.Light
            || e.knockback != 0f)
        {
            sb.Append(",\n                ");
            if (e.perilous != PerilousType.None)
                sb.Append("PerilousType.").Append(e.perilous).Append(", ");
            else
                sb.Append("PerilousType.None, ");
            if (e.extra != BossMoveExtra.None)
                sb.Append("BossMoveExtra.").Append(e.extra);
            else
                sb.Append("BossMoveExtra.None");
            if (e.baseDamage != 10 || e.postureDamage != 10f || e.hitGrade != HitGrade.Light)
            {
                sb.Append(",\n                dmg: ").Append(e.baseDamage);
                sb.Append(", posture: ").Append(F(e.postureDamage));
                sb.Append(", grade: HitGrade.").Append(e.hitGrade);
            }
            if (e.knockback != 0f)
                sb.Append(", knockback: ").Append(F(e.knockback));
        }

        sb.Append(last ? "\n" : ",\n");
    }

    static void AppendSequences(StringBuilder sb, BossMoveEntry e)
    {
        sb.Append("                new[] { ");
        if (e.sequences == null || e.sequences.Length == 0)
        {
            sb.Append("}");
            return;
        }

        for (int i = 0; i < e.sequences.Length; i++)
        {
            BossAnimSequence seq = e.sequences[i];
            if (i > 0) sb.Append(", ");
            if (seq.windows != null && seq.windows.Length > 0)
            {
                sb.Append("SeqWin(new[] { ");
                AppendStates(sb, seq.states);
                sb.Append(" }, ");
                AppendWindowsInline(sb, seq.windows);
                sb.Append(")");
            }
            else
            {
                sb.Append("Seq(");
                AppendStates(sb, seq.states);
                sb.Append(")");
            }
        }
        sb.Append(" }");
    }

    static void AppendStates(StringBuilder sb, string[] states)
    {
        if (states == null || states.Length == 0) return;
        for (int i = 0; i < states.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(Q(states[i]));
        }
    }

    static void AppendWindows(StringBuilder sb, BossMoveWindow[] windows, string indent)
    {
        sb.Append(indent).Append("new[] { ");
        AppendWindowsInline(sb, windows);
        sb.Append(" }");
    }

    static void AppendWindowsInline(StringBuilder sb, BossMoveWindow[] windows)
    {
        if (windows == null || windows.Length == 0)
        {
            sb.Append("");
            return;
        }
        for (int i = 0; i < windows.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            AppendWindowCall(sb, windows[i]);
        }
    }

    static void AppendWindowCall(StringBuilder sb, BossMoveWindow w)
    {
        sb.Append("Win(");
        sb.Append(F(w.hitStartTime)).Append(", ");
        sb.Append(F(w.recoverStart)).Append(", ");
        sb.Append(F(w.comboWindowEnd)).Append(", ");
        sb.Append(F(w.stateDuration));

        if (w.hitPulses != null && w.hitPulses.Length > 0)
        {
            sb.Append(", new[] { ");
            for (int i = 0; i < w.hitPulses.Length; i++)
            {
                HitPulse p = w.hitPulses[i];
                if (i > 0) sb.Append(", ");
                sb.Append("P(").Append(F(p.start)).Append(", ").Append(F(p.end));
                if (p.hitGrade != HitGrade.Light || p.overrideCombat || p.baseDamage != 0 || p.postureDamage != 0f)
                {
                    sb.Append(", HitGrade.").Append(p.hitGrade);
                    if (p.overrideCombat || p.baseDamage != 0 || p.postureDamage != 0f)
                        sb.Append(", ov: true, dmg: ").Append(p.baseDamage)
                            .Append(", posture: ").Append(F(p.postureDamage));
                }
                sb.Append(")");
            }
            sb.Append(" }");
        }

        if (w.arrowCues != null && w.arrowCues.Length > 0)
        {
            sb.Append(w.hitPulses != null && w.hitPulses.Length > 0 ? ", " : ", null, ");
            sb.Append("new[] { ");
            for (int i = 0; i < w.arrowCues.Length; i++)
            {
                ArrowSpawnCue a = w.arrowCues[i];
                if (i > 0) sb.Append(", ");
                sb.Append("A(").Append(F(a.time));
                if (a.hitGrade != HitGrade.Light || a.overrideCombat || a.baseDamage != 0 || a.postureDamage != 0f)
                {
                    sb.Append(", HitGrade.").Append(a.hitGrade);
                    if (a.overrideCombat || a.baseDamage != 0 || a.postureDamage != 0f)
                        sb.Append(", ov: true, dmg: ").Append(a.baseDamage)
                            .Append(", posture: ").Append(F(a.postureDamage));
                }
                sb.Append(")");
            }
            sb.Append(" }");
        }

        bool hasOptional = w.perilous != PerilousType.None || w.hitboxSlot != AttackHitboxSlot.Weapon
            || w.rotateEnd != 0.35f || w.transitionDuration != 0.1f
            || w.hitGrade != HitGrade.Light || w.overrideCombat
            || w.baseDamage != 0 || w.postureDamage != 0f || w.knockback != 0f
            || w.waitAnimEnd;

        if (hasOptional)
        {
            if ((w.hitPulses == null || w.hitPulses.Length == 0) && (w.arrowCues == null || w.arrowCues.Length == 0))
                sb.Append(", null, null");
            else if (w.arrowCues == null || w.arrowCues.Length == 0)
                sb.Append(", null");

            sb.Append(", PerilousType.").Append(w.perilous);
            sb.Append(", AttackHitboxSlot.").Append(w.hitboxSlot);
            sb.Append(", rotateEnd: ").Append(F(w.rotateEnd));
            sb.Append(", transition: ").Append(F(w.transitionDuration));
            sb.Append(", grade: HitGrade.").Append(w.hitGrade);
            if (w.overrideCombat || w.baseDamage != 0 || w.postureDamage != 0f)
                sb.Append(", ov: true, dmg: ").Append(w.baseDamage)
                    .Append(", posture: ").Append(F(w.postureDamage));
            if (w.knockback != 0f)
                sb.Append(", knockback: ").Append(F(w.knockback));
            if (w.waitAnimEnd)
                sb.Append(", waitAnimEnd: true");
        }

        sb.Append(")");
    }

    static void AppendPostApplyMethods(StringBuilder sb)
    {
        sb.AppendLine(@"
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
");
    }

    static string Q(string s) => "\"" + (s ?? "") + "\"";

    static string F(float v) => v.ToString("G9", CultureInfo.InvariantCulture) + "f";

    static string AssignLine(float v, string format) => string.Format(format, F(v));
}
#endif
