#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// 招式表伤害一览：招默认值，段/刀可勾选覆盖。时间轴仍只管出伤帧。
public class BossMoveDamageWindow : EditorWindow
{
    const string DefaultTablePath = "Assets/SO/Boss/GenichiroMoveTable.asset";
    const float NumWidth = 64f;
    const float HeavyWidth = 36f;
    const float HeavyKnockback = 1f;

    BossMoveTable table;
    Vector2 scroll;
    string filter = "";
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
            "招那一行是默认伤害。勾选段/刀/箭的「覆盖」后才能改那一行；不勾则跟招走。弓段不开刀，但可以单独改箭伤。垫步仍无伤害。勾「重击」后挨打走重受击（击退>0）。时间轴只管出伤帧。",
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
        GUILayout.Label("重击", EditorStyles.miniBoldLabel, GUILayout.Width(HeavyWidth));
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
        bool heavy = EditorGUILayout.Toggle(entry.knockback > 0f, GUILayout.Width(HeavyWidth));
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(table, "招默认伤害");
            entry.baseDamage = dmg;
            entry.postureDamage = pos;
            entry.knockback = heavy ? Mathf.Max(entry.knockback, HeavyKnockback) : 0f;
            EditorUtility.SetDirty(table);
        }
    }

    void DrawSegments(BossMoveEntry entry)
    {
        int count = SegmentCount(entry);
        for (int s = 0; s < count; s++)
        {
            string anim = StateName(entry, s) ?? "—";
            BossMoveWindow w = BossMovePicker.WindowFor(entry, s);
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
                EditorGUILayout.Toggle(false, GUILayout.Width(HeavyWidth));
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
                continue;
            }

            DrawOverrideCombat(
                ref w.overrideCombat,
                ref w.baseDamage,
                ref w.postureDamage,
                ref w.knockback,
                entry.baseDamage,
                entry.postureDamage,
                entry.knockback);
            EditorGUILayout.EndHorizontal();

            if (ranged)
                continue;

            int inheritDmg = w.overrideCombat ? w.baseDamage : entry.baseDamage;
            float inheritPos = w.overrideCombat ? w.postureDamage : entry.postureDamage;
            float inheritKb = w.overrideCombat ? w.knockback : entry.knockback;

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
                        inheritDmg,
                        inheritPos,
                        inheritKb);
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
                EditorGUILayout.Toggle(inheritKb > 0f, GUILayout.Width(HeavyWidth));
                GUI.enabled = true;
                EditorGUILayout.EndHorizontal();
            }
        }
    }

    void DrawOverrideCombat(
        ref bool ov,
        ref int dmg,
        ref float pos,
        ref float kb,
        int inheritDmg,
        float inheritPos,
        float inheritKb)
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
            }
            ov = nextOv;
            EditorUtility.SetDirty(table);
        }

        int showDmg = ov ? dmg : inheritDmg;
        float showPos = ov ? pos : inheritPos;
        float showKb = ov ? kb : inheritKb;
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

        bool shownHeavy = showKb > 0f;
        bool nextHeavy = EditorGUILayout.Toggle(shownHeavy, GUILayout.Width(HeavyWidth));
        if (nextHeavy != shownHeavy)
        {
            Undo.RecordObject(table, "重击");
            if (!ov)
            {
                ov = true;
                dmg = inheritDmg;
                pos = inheritPos;
            }
            kb = nextHeavy ? Mathf.Max(showKb, HeavyKnockback) : 0f;
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

    static int SegmentCount(BossMoveEntry entry)
    {
        int windows = entry.windows != null ? entry.windows.Length : 0;
        BossAnimSequence seq = FirstSequence(entry);
        int states = seq != null && seq.states != null ? seq.states.Length : 0;
        return Mathf.Max(1, windows, states);
    }

    static BossAnimSequence FirstSequence(BossMoveEntry entry)
    {
        if (entry == null || entry.sequences == null || entry.sequences.Length == 0)
            return null;
        return entry.sequences[0];
    }

    static string StateName(BossMoveEntry entry, int segment)
    {
        BossAnimSequence seq = FirstSequence(entry);
        if (seq == null || seq.states == null || seq.states.Length == 0)
            return null;
        int i = Mathf.Clamp(segment, 0, seq.states.Length - 1);
        return seq.states[i];
    }
}
#endif
