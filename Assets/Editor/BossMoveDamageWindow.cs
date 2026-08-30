using ARPG.Boss;
using ARPG.Configs;

namespace ARPG.Editor
{
    #if UNITY_EDITOR
    using UnityEditor;
    using UnityEngine;

    // 招式表伤害一览：招默认值，段/刀可勾选覆盖。时间轴仍只管出伤帧。
    public class BossMoveDamageWindow : EditorWindow
    {
        const string DefaultTablePath = "Assets/SO/Boss/GenichiroMoveTable.asset";
        const float NumWidth = 64f;
        const float GradeWidth = 72f;

        BossMoveTable table;
        Vector2 scroll;
        string filter = "";
        int sequenceIndex;
        readonly System.Collections.Generic.Dictionary<string, bool> fold =
            new System.Collections.Generic.Dictionary<string, bool>();

        [MenuItem("ARPG/招式伤害")]
        public static void OpenEmpty()
        {
            BossMoveDamageWindow w = GetWindow<BossMoveDamageWindow>("招式伤害");
            if (w.table == null)
                w.table = AssetDatabase.LoadAssetAtPath<BossMoveTable>(DefaultTablePath);
            w.Show();
        }

        public static void Open(BossMoveTable t)
        {
            BossMoveDamageWindow w = GetWindow<BossMoveDamageWindow>("招式伤害");
            w.table = t;
            w.Show();
        }

        void OnGUI()
        {
            table = (BossMoveTable)EditorGUILayout.ObjectField("招式表", table, typeof(BossMoveTable), false);
            if (table == null)
            {
                EditorGUILayout.HelpBox(
                    "拖入 GenichiroMoveTable，或菜单 ARPG/Create Genichiro Move Table。",
                    MessageType.Info);
                if (GUILayout.Button("加载默认弦一郎表"))
                    table = AssetDatabase.LoadAssetAtPath<BossMoveTable>(DefaultTablePath);
                return;
            }

            EditorGUILayout.HelpBox(
                "招那一行是默认伤害和受击等级。勾选段/刀/箭的「覆盖」后才能改那一行；不勾则跟招走。弓段不开刀，但每支出箭可单独改伤和等级。垫步仍无伤害。等级 Light/Mid/Heavy 决定打到玩家时的受击/格挡/弹反。改等级会按默认表填数字：箭 Light 10/10、Mid 15/15、Heavy 20/20；刀 Light 10/10、Mid 15/15、Heavy 25/25。时间轴只管出伤帧。",
                MessageType.None);

            EditorGUILayout.BeginHorizontal();
            filter = EditorGUILayout.TextField("筛选 id", filter);
            if (GUILayout.Button("打开时间轴", GUILayout.Width(88)))
                AttackTimelineWindow.Open(table);
            EditorGUILayout.EndHorizontal();

            DrawHeader();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            if (table.moves != null)
            {
                for (int i = 0; i < table.moves.Length; i++)
                {
                    BossMoveEntry entry = table.moves[i];
                    if (entry == null) continue;
                    if (!string.IsNullOrEmpty(filter) &&
                        (string.IsNullOrEmpty(entry.id) ||
                         entry.id.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0))
                        continue;
                    DrawMove(entry);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        static void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("招 / 段 / 刀", EditorStyles.miniBoldLabel);
            GUILayout.Label("层", EditorStyles.miniBoldLabel, GUILayout.Width(48));
            GUILayout.Label("覆盖", EditorStyles.miniBoldLabel, GUILayout.Width(36));
            GUILayout.Label("血量", EditorStyles.miniBoldLabel, GUILayout.Width(NumWidth));
            GUILayout.Label("架势", EditorStyles.miniBoldLabel, GUILayout.Width(NumWidth));
            GUILayout.Label("等级", EditorStyles.miniBoldLabel, GUILayout.Width(GradeWidth));
            EditorGUILayout.EndHorizontal();
        }

        void DrawMove(BossMoveEntry entry)
        {
            string key = string.IsNullOrEmpty(entry.id) ? "(空)" : entry.id;
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            bool open = IsFold(key);
            bool nextOpen = EditorGUILayout.Foldout(open, key, true);
            if (nextOpen != open)
                fold[key] = nextOpen;
            GUILayout.Label(LayerLabel(entry.layer), GUILayout.Width(48));
            GUILayout.Label("—", GUILayout.Width(36));
            DrawEntryCombat(entry);
            EditorGUILayout.EndHorizontal();

            if (nextOpen)
                DrawSegments(entry);
            EditorGUILayout.EndVertical();
        }

        void DrawEntryCombat(BossMoveEntry entry)
        {
            EditorGUI.BeginChangeCheck();
            int dmg = EditorGUILayout.IntField(entry.baseDamage, GUILayout.Width(NumWidth));
            float pos = EditorGUILayout.FloatField(entry.postureDamage, GUILayout.Width(NumWidth));
            HitGrade g = (HitGrade)EditorGUILayout.EnumPopup(entry.hitGrade, GUILayout.Width(GradeWidth));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(table, "招默认伤害");
                if (g != entry.hitGrade)
                {
                    int snapDmg;
                    float snapPos;
                    AttackCombatResolve.DefaultCombat(g, AttackWindowSync.EntryUsesArrowNums(entry), out snapDmg, out snapPos);
                    dmg = snapDmg;
                    pos = snapPos;
                }
                entry.baseDamage = dmg;
                entry.postureDamage = pos;
                entry.hitGrade = g;
                EditorUtility.SetDirty(table);
            }
        }

        void DrawSegments(BossMoveEntry entry)
        {
            BossAnimSequence sequence = CurrentSequence(entry);
            if (entry.sequences != null && entry.sequences.Length > 1)
            {
                string[] seqLabels = new string[entry.sequences.Length];
                for (int i = 0; i < entry.sequences.Length; i++)
                    seqLabels[i] = SequenceLabel(entry.sequences[i], i);
                sequenceIndex = EditorGUILayout.Popup(
                    "动画分支", Mathf.Clamp(sequenceIndex, 0, entry.sequences.Length - 1), seqLabels);
                sequence = CurrentSequence(entry);
            }
            else
            {
                sequenceIndex = 0;
            }

            int count = SegmentCount(entry, sequence);
            for (int s = 0; s < count; s++)
            {
                string anim = StateName(entry, sequence, s) ?? "—";
                BossMoveWindow w = BossMovePicker.WindowFor(entry, s, sequence);
                if (w == null) continue;

                bool melee = AttackWindowSync.CanMeleeHit(w.hitStartTime, w.recoverStart, w.hitPulses);
                bool ranged = !melee && !IsLocomotionAnim(anim);
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(16);
                GUILayout.Label(
                    ranged ? "段  " + anim + "  ·箭" : "段  " + anim,
                    GUILayout.ExpandWidth(true));
                GUILayout.Label("", GUILayout.Width(48));
                if (!melee && !ranged)
                {
                    GUILayout.Label("无近战", GUILayout.Width(36));
                    GUI.enabled = false;
                    EditorGUILayout.IntField(0, GUILayout.Width(NumWidth));
                    EditorGUILayout.FloatField(0f, GUILayout.Width(NumWidth));
                    EditorGUILayout.EnumPopup(HitGrade.Light, GUILayout.Width(GradeWidth));
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                    continue;
                }

                DrawOverrideCombat(
                    ref w.overrideCombat,
                    ref w.baseDamage,
                    ref w.postureDamage,
                    ref w.knockback,
                    ref w.hitGrade,
                    entry.baseDamage,
                    entry.postureDamage,
                    entry.knockback,
                    entry.hitGrade,
                    arrow: ranged);
                EditorGUILayout.EndHorizontal();

                int inheritDmg = w.overrideCombat ? w.baseDamage : entry.baseDamage;
                float inheritPos = w.overrideCombat ? w.postureDamage : entry.postureDamage;
                float inheritKb = w.overrideCombat ? w.knockback : entry.knockback;
                HitGrade inheritGrade = w.overrideCombat ? w.hitGrade : entry.hitGrade;

                if (ranged)
                {
                    DrawArrowCues(w, inheritDmg, inheritPos, inheritKb, inheritGrade);
                    continue;
                }

                if (w.hitPulses != null && w.hitPulses.Length > 0)
                {
                    for (int p = 0; p < w.hitPulses.Length; p++)
                    {
                        HitPulse pulse = w.hitPulses[p];
                        if (pulse == null) continue;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(32);
                        GUILayout.Label(
                            "刀 " + (p + 1) + "  " + pulse.start.ToString("0.00") + "–" + pulse.end.ToString("0.00"),
                            GUILayout.ExpandWidth(true));
                        GUILayout.Label("", GUILayout.Width(48));
                        DrawOverrideCombat(
                            ref pulse.overrideCombat,
                            ref pulse.baseDamage,
                            ref pulse.postureDamage,
                            ref pulse.knockback,
                            ref pulse.hitGrade,
                            inheritDmg,
                            inheritPos,
                            inheritKb,
                            inheritGrade,
                            arrow: false);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                else
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(32);
                    GUILayout.Label(
                        "刀 1  " + w.hitStartTime.ToString("0.00") + "–" + w.recoverStart.ToString("0.00"),
                        GUILayout.ExpandWidth(true));
                    GUILayout.Label("", GUILayout.Width(48));
                    GUILayout.Label("用段", GUILayout.Width(36));
                    GUI.enabled = false;
                    EditorGUILayout.IntField(inheritDmg, GUILayout.Width(NumWidth));
                    EditorGUILayout.FloatField(inheritPos, GUILayout.Width(NumWidth));
                    EditorGUILayout.EnumPopup(inheritGrade, GUILayout.Width(GradeWidth));
                    GUI.enabled = true;
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        void DrawArrowCues(
            BossMoveWindow w,
            int inheritDmg,
            float inheritPos,
            float inheritKb,
            HitGrade inheritGrade)
        {
            if (w.arrowCues == null || w.arrowCues.Length == 0)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(32);
                GUILayout.Label("箭  （时间轴未插出箭点）", GUILayout.ExpandWidth(true));
                EditorGUILayout.EndHorizontal();
                return;
            }

            for (int i = 0; i < w.arrowCues.Length; i++)
            {
                ArrowSpawnCue cue = w.arrowCues[i];
                if (cue == null) continue;
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(32);
                GUILayout.Label(
                    "箭 " + (i + 1) + "  " + cue.time.ToString("0.00") + "s",
                    GUILayout.ExpandWidth(true));
                GUILayout.Label("", GUILayout.Width(48));
                DrawOverrideCombat(
                    ref cue.overrideCombat,
                    ref cue.baseDamage,
                    ref cue.postureDamage,
                    ref cue.knockback,
                    ref cue.hitGrade,
                    inheritDmg,
                    inheritPos,
                    inheritKb,
                    inheritGrade,
                    arrow: true);
                EditorGUILayout.EndHorizontal();
            }
        }

        void DrawOverrideCombat(
            ref bool ov,
            ref int dmg,
            ref float pos,
            ref float kb,
            ref HitGrade grade,
            int inheritDmg,
            float inheritPos,
            float inheritKb,
            HitGrade inheritGrade,
            bool arrow)
        {
            bool nextOv = EditorGUILayout.Toggle(ov, GUILayout.Width(36));
            if (nextOv != ov)
            {
                Undo.RecordObject(table, "覆盖伤害");
                if (nextOv && !ov)
                {
                    dmg = inheritDmg;
                    pos = inheritPos;
                    kb = inheritKb;
                    grade = inheritGrade;
                }
                ov = nextOv;
                EditorUtility.SetDirty(table);
            }

            int showDmg = ov ? dmg : inheritDmg;
            float showPos = ov ? pos : inheritPos;
            GUI.enabled = ov;
            EditorGUI.BeginChangeCheck();
            int nd = EditorGUILayout.IntField(showDmg, GUILayout.Width(NumWidth));
            float np = EditorGUILayout.FloatField(showPos, GUILayout.Width(NumWidth));
            bool numbersChanged = EditorGUI.EndChangeCheck();
            GUI.enabled = true;
            if (ov && numbersChanged)
            {
                Undo.RecordObject(table, "招式伤害");
                dmg = nd;
                pos = np;
                EditorUtility.SetDirty(table);
            }

            HitGrade showG = ov ? grade : inheritGrade;
            HitGrade nextG = (HitGrade)EditorGUILayout.EnumPopup(showG, GUILayout.Width(GradeWidth));
            if (nextG != showG)
            {
                Undo.RecordObject(table, "受击等级");
                int snapDmg;
                float snapPos;
                AttackCombatResolve.DefaultCombat(nextG, arrow, out snapDmg, out snapPos);
                if (!ov)
                {
                    ov = true;
                    kb = inheritKb;
                }
                grade = nextG;
                dmg = snapDmg;
                pos = snapPos;
                EditorUtility.SetDirty(table);
            }
        }

        bool IsFold(string key)
        {
            bool open;
            return fold.TryGetValue(key, out open) && open;
        }

        static bool IsLocomotionAnim(string anim)
        {
            if (string.IsNullOrEmpty(anim)) return false;
            return anim.StartsWith("Dodge", System.StringComparison.OrdinalIgnoreCase)
                || anim.StartsWith("Step_", System.StringComparison.OrdinalIgnoreCase);
        }

        static string LayerLabel(BossMoveLayer layer)
        {
            switch (layer)
            {
                case BossMoveLayer.Active: return "主动";
                case BossMoveLayer.Kengeki: return "交锋";
                case BossMoveLayer.Interrupt: return "打断";
                default: return layer.ToString();
            }
        }

        static int SegmentCount(BossMoveEntry entry, BossAnimSequence seq)
        {
            if (entry == null) return 1;
            int windows = 0;
            if (seq != null && seq.windows != null && seq.windows.Length > 0)
                windows = seq.windows.Length;
            else if (entry.windows != null)
                windows = entry.windows.Length;
            int states = seq != null && seq.states != null ? seq.states.Length : 0;
            return Mathf.Max(1, windows, states);
        }

        static BossAnimSequence FirstSequence(BossMoveEntry entry)
        {
            if (entry == null || entry.sequences == null || entry.sequences.Length == 0)
                return null;
            return entry.sequences[0];
        }

        BossAnimSequence CurrentSequence(BossMoveEntry entry)
        {
            if (entry == null || entry.sequences == null || entry.sequences.Length == 0)
                return null;
            int si = Mathf.Clamp(sequenceIndex, 0, entry.sequences.Length - 1);
            return entry.sequences[si];
        }

        static string SequenceLabel(BossAnimSequence seq, int index)
        {
            if (seq == null || seq.states == null || seq.states.Length == 0)
                return "分支 " + (index + 1);
            return string.Join(" → ", seq.states);
        }

        static string StateName(BossMoveEntry entry, BossAnimSequence seq, int segment)
        {
            if (seq == null || seq.states == null || seq.states.Length == 0)
                return null;
            int i = Mathf.Clamp(segment, 0, seq.states.Length - 1);
            return seq.states[i];
        }
    }
    #endif

}
