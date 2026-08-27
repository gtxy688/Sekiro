using UnityEditor;
using UnityEngine;

public class AttackTimelineWindow : EditorWindow
{
    AttackConfig playerConfig;
    BossMoveTable bossTable;
    int moveIndex;
    int segmentIndex;
    float scrub;
    readonly AttackTimelinePreview preview = new AttackTimelinePreview();
    Vector2 scroll;

    HitPulse[] workingPulses;
    AttackSfxCue[] workingSfx;
    Object loadedSource;
    int loadedMove = -1;
    int loadedSeg = -1;

    int dragPulse = -1;
    int dragEdge;
    int dragSfx = -1;
    int pendingRemovePulse = -1;
    int pendingRemoveSfx = -1;
    bool pendingAddPulse;
    bool pendingAddSfx;
    bool pendingShrinkPulse;
    bool pendingClearMelee;
    bool forceNoHit;
    float previewHeight = 220f;
    int previewControlId;
    int resizeControlId;

    [MenuItem("ARPG/攻击时间轴")]
    public static void OpenEmpty()
    {
        GetWindow<AttackTimelineWindow>("攻击时间轴");
    }

    public static void Open(AttackConfig config)
    {
        AttackTimelineWindow w = GetWindow<AttackTimelineWindow>("攻击时间轴");
        w.playerConfig = config;
        w.bossTable = null;
        w.Show();
        w.ReloadWorking();
    }

    public static void Open(BossMoveTable table)
    {
        AttackTimelineWindow w = GetWindow<AttackTimelineWindow>("攻击时间轴");
        w.bossTable = table;
        w.playerConfig = null;
        w.moveIndex = 0;
        w.segmentIndex = 0;
        w.Show();
        w.ReloadWorking();
    }

    void OnEnable()
    {
        previewHeight = EditorPrefs.GetFloat("ARPG.AttackTimeline.PreviewHeight", 220f);
    }

    void OnDisable()
    {
        EditorPrefs.SetFloat("ARPG.AttackTimeline.PreviewHeight", previewHeight);
        preview.Dispose();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        DrawPrefabSettings();
        DrawTargetPicker();
        ReloadWorkingIfNeeded();

        string animName = CurrentAnimName();
        GameObject prefab = bossTable != null
            ? AttackTimelinePrefs.BossPrefab
            : AttackTimelinePrefs.PlayerPrefab;

        preview.Ensure(prefab);

        AnimationClip clip = AttackTimelineClipFinder.Find(preview.Animator, animName);
        float clipLength = clip != null && clip.length > 0.01f ? clip.length : 1f;
        if (clip == null && !string.IsNullOrEmpty(animName) && prefab != null)
            EditorGUILayout.HelpBox("预览 Animator 上找不到状态：" + animName, MessageType.Warning);

        EditorGUILayout.LabelField("按住左侧名字左右拖可改时间，Shift 细调；右侧数字可手填。", EditorStyles.miniLabel);
        scrub = DragNameTime("进度（秒）", scrub, 0f, clipLength);

        float n = clipLength > 0.0001f ? scrub / clipLength : 0f;
        if (!string.IsNullOrEmpty(animName))
            preview.Sample(animName, n);

        Rect previewRect = GUILayoutUtility.GetRect(16f, previewHeight, GUILayout.ExpandWidth(true));
        preview.Draw(previewRect);
        HandlePreviewView(previewRect);

        Rect resizeRect = GUILayoutUtility.GetRect(16f, 8f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(resizeRect, new Color(0.25f, 0.25f, 0.25f));
        EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeVertical);
        HandlePreviewResize(resizeRect);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("预览：左键拖旋转，滚轮缩放，底边拖高度。", EditorStyles.miniLabel);
        if (GUILayout.Button("复位视角", GUILayout.Width(72)))
            preview.ResetView();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawHitTimeSliders(clipLength);
        EditorGUILayout.Space();
        DrawHitTrack(clipLength);
        EditorGUILayout.Space();
        DrawSfxTrack(clipLength);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("用动画长度填写时长"))
            ApplyClipLength(clipLength);
        if (GUILayout.Button("保存"))
            Save(clipLength);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndScrollView();
        ApplyPendingEdits(clipLength);
        if (Event.current.type == EventType.MouseDrag)
            Repaint();
    }

    void DrawPrefabSettings()
    {
        EditorGUILayout.LabelField("预览模型", EditorStyles.boldLabel);
        GameObject player = (GameObject)EditorGUILayout.ObjectField(
            "玩家 Prefab", AttackTimelinePrefs.PlayerPrefab, typeof(GameObject), false);
        if (player != AttackTimelinePrefs.PlayerPrefab)
            AttackTimelinePrefs.PlayerPrefab = player;
        GameObject boss = (GameObject)EditorGUILayout.ObjectField(
            "Boss Prefab", AttackTimelinePrefs.BossPrefab, typeof(GameObject), false);
        if (boss != AttackTimelinePrefs.BossPrefab)
            AttackTimelinePrefs.BossPrefab = boss;
        if (AttackTimelinePrefs.PlayerPrefab == null && AttackTimelinePrefs.BossPrefab == null)
            EditorGUILayout.HelpBox("第一次把玩家、Boss Prefab 各拖进来一次。不会写进场景。", MessageType.Info);
    }

    void DrawTargetPicker()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("正在编辑", EditorStyles.boldLabel);
        AttackConfig nextPlayer = (AttackConfig)EditorGUILayout.ObjectField(
            "玩家招式", playerConfig, typeof(AttackConfig), false);
        BossMoveTable nextBoss = (BossMoveTable)EditorGUILayout.ObjectField(
            "Boss 招式表", bossTable, typeof(BossMoveTable), false);
        if (nextPlayer != playerConfig)
        {
            playerConfig = nextPlayer;
            if (playerConfig != null) bossTable = null;
        }
        if (nextBoss != bossTable)
        {
            bossTable = nextBoss;
            if (bossTable != null) playerConfig = null;
            moveIndex = 0;
            segmentIndex = 0;
        }

        if (bossTable != null && bossTable.moves != null && bossTable.moves.Length > 0)
        {
            string[] ids = new string[bossTable.moves.Length];
            for (int i = 0; i < ids.Length; i++)
                ids[i] = MovePopupLabel(bossTable.moves[i], i);
            moveIndex = EditorGUILayout.Popup("招式", Mathf.Clamp(moveIndex, 0, ids.Length - 1), ids);

            BossMoveEntry entry = bossTable.moves[Mathf.Clamp(moveIndex, 0, bossTable.moves.Length - 1)];
            int segCount = SegmentCount(entry);
            segmentIndex = Mathf.Clamp(segmentIndex, 0, Mathf.Max(0, segCount - 1));

            if (segCount <= 1)
            {
                EditorGUILayout.LabelField("动画", StateName(entry, 0) ?? "—");
            }
            else
            {
                string[] segs = new string[segCount];
                for (int i = 0; i < segCount; i++)
                    segs[i] = (i + 1) + "/" + segCount + "  " + (StateName(entry, i) ?? "—");
                segmentIndex = EditorGUILayout.Popup("第几段动画", segmentIndex, segs);
            }

            BossMoveWindow window = CurrentWindow();
            if (window != null)
            {
                EditorGUI.BeginChangeCheck();
                AttackHitboxSlot slot = (AttackHitboxSlot)EditorGUILayout.EnumPopup(
                    "判定 Hitbox", window.hitboxSlot);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(bossTable, "Attack Timeline Hitbox");
                    window.hitboxSlot = slot;
                    EditorUtility.SetDirty(bossTable);
                }
            }

            if (entry != null && entry.sequences != null && entry.sequences.Length > 1)
                EditorGUILayout.LabelField(
                    "另有 " + (entry.sequences.Length - 1) + " 套动画分支，预览第一套；时间窗各套共用。",
                    EditorStyles.miniLabel);
            if (GUILayout.Button("打开招式伤害表", GUILayout.Width(130)))
                BossMoveDamageWindow.Open(bossTable);
        }
    }

    void HandlePreviewView(Rect rect)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.ScrollWheel && rect.Contains(e.mousePosition))
        {
            preview.Zoom(-e.delta.y);
            e.Use();
            Repaint();
            return;
        }

        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            GUIUtility.hotControl = id;
            previewControlId = id;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == previewControlId)
        {
            preview.Orbit(e.delta.x, -e.delta.y);
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && GUIUtility.hotControl == previewControlId)
        {
            GUIUtility.hotControl = 0;
            previewControlId = 0;
            e.Use();
        }
    }

    void HandlePreviewResize(Rect rect)
    {
        Event e = Event.current;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
        {
            GUIUtility.hotControl = id;
            resizeControlId = id;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == resizeControlId)
        {
            previewHeight = Mathf.Clamp(previewHeight + e.delta.y, 120f, 560f);
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && GUIUtility.hotControl == resizeControlId)
        {
            GUIUtility.hotControl = 0;
            resizeControlId = 0;
            EditorPrefs.SetFloat("ARPG.AttackTimeline.PreviewHeight", previewHeight);
            e.Use();
        }
    }

    void ReloadWorkingIfNeeded()
    {
        Object src = playerConfig != null ? (Object)playerConfig : bossTable;
        if (src != loadedSource || moveIndex != loadedMove || segmentIndex != loadedSeg)
            ReloadWorking();
    }

    void ReloadWorking()
    {
        loadedSource = playerConfig != null ? (Object)playerConfig : bossTable;
        loadedMove = moveIndex;
        loadedSeg = segmentIndex;
        workingPulses = ClonePulses(CurrentStoredPulses(), CurrentHitStart(), CurrentRecover());
        workingSfx = CloneSfx(CurrentStoredSfx());
        forceNoHit = false;
    }

    void DrawHitTimeSliders(float clipLength)
    {
        if (workingPulses == null) return;

        EditorGUILayout.LabelField("判定时间", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("加段", GUILayout.Width(60)))
            pendingAddPulse = true;
        if (workingPulses.Length > 0 && GUILayout.Button("删末段", GUILayout.Width(70)))
            pendingShrinkPulse = true;
        if (GUILayout.Button("关闭近战判定", GUILayout.Width(110)))
            pendingClearMelee = true;
        EditorGUILayout.EndHorizontal();
        if (workingPulses.Length == 0)
            EditorGUILayout.HelpBox(
                "本段无近战红条。弓/垫步点「关闭近战判定」再保存：HitStart=Recover=时长，全程不开刀。不要把红条缩成 0.01s。",
                MessageType.Info);

        for (int i = 0; i < workingPulses.Length; i++)
        {
            HitPulse p = workingPulses[i];
            if (p == null)
                p = workingPulses[i] = new HitPulse();

            string prefix = workingPulses.Length > 1 ? (i + 1) + " " : "";
            float newStart = DragNameTime(prefix + "开始（秒）", p.start, 0f, clipLength);
            if (!Mathf.Approximately(newStart, p.start))
            {
                p.start = newStart;
                scrub = newStart;
            }
            float newEnd = DragNameTime(prefix + "结束（秒）", p.end, 0f, clipLength);
            if (!Mathf.Approximately(newEnd, p.end))
            {
                p.end = newEnd;
                scrub = newEnd;
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("开始=当前进度", GUILayout.Width(110)))
                p.start = scrub;
            if (GUILayout.Button("结束=当前进度", GUILayout.Width(110)))
                p.end = scrub;
            if (GUILayout.Button("删这段", GUILayout.Width(60)))
                pendingRemovePulse = i;
            EditorGUILayout.EndHorizontal();
        }
    }

    void DrawHitTrack(float clipLength)
    {
        EditorGUILayout.LabelField("判定范围", EditorStyles.miniLabel);

        Rect track = GUILayoutUtility.GetRect(16f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(track, new Color(0.18f, 0.18f, 0.18f));
        HandlePlayhead(track, clipLength);

        if (workingPulses == null) return;
        for (int i = 0; i < workingPulses.Length; i++)
        {
            HitPulse p = workingPulses[i];
            if (p == null) continue;
            Rect bar = PulseRect(track, p.start, p.end, clipLength);
            EditorGUI.DrawRect(bar, new Color(1f, 0.27f, 0.23f, 0.85f));
            HandlePulseDrag(track, clipLength, i, bar);
        }
    }

    void DrawSfxTrack(float clipLength)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("音效（♪）", EditorStyles.boldLabel);
        if (GUILayout.Button("加音符", GUILayout.Width(70)))
            pendingAddSfx = true;
        EditorGUILayout.EndHorizontal();

        Rect track = GUILayoutUtility.GetRect(16f, 22f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(track, new Color(0.18f, 0.18f, 0.18f));
        HandlePlayhead(track, clipLength);

        if (workingSfx != null)
        {
            for (int i = 0; i < workingSfx.Length; i++)
            {
                AttackSfxCue cue = workingSfx[i];
                if (cue == null) continue;
                float x = track.x + (clipLength > 0.0001f ? cue.time / clipLength : 0f) * track.width;
                Rect mark = new Rect(x - 7f, track.y, 14f, track.height);
                GUI.Label(mark, "♪");
                HandleSfxDrag(track, clipLength, i, mark);
            }
        }

        if (workingSfx == null) return;
        for (int i = 0; i < workingSfx.Length; i++)
        {
            AttackSfxCue cue = workingSfx[i] ?? new AttackSfxCue();
            workingSfx[i] = cue;
            string sfxPrefix = workingSfx.Length > 1 ? (i + 1) + " " : "";
            float newTime = DragNameTime(sfxPrefix + "音效（秒）", cue.time, 0f, clipLength);
            if (!Mathf.Approximately(newTime, cue.time))
            {
                cue.time = newTime;
                scrub = newTime;
            }
            EditorGUILayout.BeginHorizontal();
            cue.clip = (AudioClip)EditorGUILayout.ObjectField(cue.clip, typeof(AudioClip), false);
            if (GUILayout.Button("×", GUILayout.Width(22)))
                pendingRemoveSfx = i;
            EditorGUILayout.EndHorizontal();
        }
    }

    float DragNameTime(string label, float value, float min, float max)
    {
        Rect row = EditorGUILayout.GetControlRect();
        GUIContent labelContent = new GUIContent(label);
        float labelW = Mathf.Clamp(EditorStyles.label.CalcSize(labelContent).x + 6f, 72f, row.width * 0.5f);
        Rect labelRect = new Rect(row.x, row.y, labelW, row.height);
        Rect fieldRect = new Rect(labelRect.xMax + 4f, row.y, Mathf.Max(40f, row.width - labelW - 4f), row.height);

        int id = GUIUtility.GetControlID(FocusType.Passive);
        EditorGUIUtility.AddCursorRect(labelRect, MouseCursor.SlideArrow);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 0 && labelRect.Contains(e.mousePosition))
        {
            GUIUtility.hotControl = id;
            GUIUtility.keyboardControl = 0;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && GUIUtility.hotControl == id)
        {
            float range = Mathf.Max(0.01f, max - min);
            float speed = range / 360f;
            if (e.shift) speed *= 0.1f;
            value = Mathf.Clamp(value + e.delta.x * speed, min, max);
            GUI.changed = true;
            e.Use();
            Repaint();
        }
        else if (e.type == EventType.MouseUp && GUIUtility.hotControl == id)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }

        EditorGUI.LabelField(labelRect, labelContent);
        value = EditorGUI.FloatField(fieldRect, value);
        return Mathf.Clamp(value, min, max);
    }

    void HandlePlayhead(Rect track, float clipLength)
    {
        float x = track.x + (clipLength > 0.0001f ? scrub / clipLength : 0f) * track.width;
        EditorGUI.DrawRect(new Rect(x, track.y, 2f, track.height), Color.white);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && track.Contains(e.mousePosition) && dragPulse < 0 && dragSfx < 0)
        {
            scrub = TimeAt(track, e.mousePosition.x, clipLength);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && dragPulse < 0 && dragSfx < 0 && track.Contains(e.mousePosition))
        {
            scrub = TimeAt(track, e.mousePosition.x, clipLength);
            e.Use();
        }
    }

    void HandlePulseDrag(Rect track, float clipLength, int index, Rect bar)
    {
        Rect left = new Rect(bar.x - 8f, bar.y, 16f, bar.height);
        Rect right = new Rect(bar.xMax - 8f, bar.y, 16f, bar.height);
        EditorGUIUtility.AddCursorRect(left, MouseCursor.ResizeHorizontal);
        EditorGUIUtility.AddCursorRect(right, MouseCursor.ResizeHorizontal);

        Event e = Event.current;
        if (e.type == EventType.MouseDown)
        {
            if (left.Contains(e.mousePosition)) { dragPulse = index; dragEdge = 0; e.Use(); }
            else if (right.Contains(e.mousePosition)) { dragPulse = index; dragEdge = 1; e.Use(); }
        }
        else if (e.type == EventType.MouseDrag && dragPulse == index)
        {
            float t = TimeAt(track, e.mousePosition.x, clipLength);
            HitPulse p = workingPulses[index];
            if (dragEdge == 0) p.start = t;
            else p.end = t;
            workingPulses[index] = p;
            scrub = t;
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            dragPulse = -1;
        }
    }

    void HandleSfxDrag(Rect track, float clipLength, int index, Rect mark)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && mark.Contains(e.mousePosition))
        {
            dragSfx = index;
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && dragSfx == index)
        {
            workingSfx[index].time = TimeAt(track, e.mousePosition.x, clipLength);
            e.Use();
        }
        else if (e.type == EventType.MouseUp)
        {
            dragSfx = -1;
        }
    }

    static float TimeAt(Rect track, float mouseX, float clipLength)
    {
        float u = track.width > 0.0001f ? Mathf.InverseLerp(track.x, track.xMax, mouseX) : 0f;
        return Mathf.Clamp(u * clipLength, 0f, clipLength);
    }

    static Rect PulseRect(Rect track, float start, float end, float clipLength)
    {
        float len = Mathf.Max(clipLength, 0.0001f);
        float x0 = track.x + start / len * track.width;
        float x1 = track.x + end / len * track.width;
        return Rect.MinMaxRect(Mathf.Min(x0, x1), track.y + 3f, Mathf.Max(x0, x1), track.yMax - 3f);
    }

    void Save(float clipLength)
    {
        HitPulse[] clamped = AttackWindowSync.ClampPulses(workingPulses, clipLength);
        if (playerConfig != null)
        {
            Undo.RecordObject(playerConfig, "Attack Timeline");
            SaveMeleeWindow(playerConfig, clamped, forceNoHit);
            playerConfig.sfxCues = CloneSfx(workingSfx);
            EditorUtility.SetDirty(playerConfig);
            forceNoHit = false;
            workingPulses = ClonePulses(playerConfig.hitPulses, playerConfig.HitStartTime, playerConfig.RecoveryWindowStart);
            return;
        }

        BossMoveWindow w = CurrentWindow();
        if (bossTable == null || w == null) return;
        Undo.RecordObject(bossTable, "Attack Timeline");
        SaveMeleeWindow(w, clamped, forceNoHit);
        w.sfxCues = CloneSfx(workingSfx);
        EditorUtility.SetDirty(bossTable);
        forceNoHit = false;
        workingPulses = ClonePulses(w.hitPulses, w.hitStartTime, w.recoverStart);
    }

    static void SaveMeleeWindow(AttackConfig cfg, HitPulse[] clamped, bool noHit)
    {
        if (noHit || !AttackWindowSync.CanMeleeHit(cfg.HitStartTime, cfg.RecoveryWindowStart, clamped))
            AttackWindowSync.ApplyNoHit(cfg);
        else
            AttackWindowSync.ApplyPulses(cfg, clamped);
    }

    static void SaveMeleeWindow(BossMoveWindow w, HitPulse[] clamped, bool noHit)
    {
        if (noHit || !AttackWindowSync.CanMeleeHit(w.hitStartTime, w.recoverStart, clamped))
            AttackWindowSync.ApplyNoHit(w);
        else
            AttackWindowSync.ApplyPulses(w, clamped);
    }

    void ApplyClipLength(float clipLength)
    {
        if (playerConfig != null)
        {
            Undo.RecordObject(playerConfig, "Attack Timeline Duration");
            playerConfig.StateDuration = clipLength;
            if (playerConfig.ComboWindowEnd > clipLength)
                playerConfig.ComboWindowEnd = clipLength;
            EditorUtility.SetDirty(playerConfig);
            return;
        }
        BossMoveWindow w = CurrentWindow();
        if (bossTable == null || w == null) return;
        Undo.RecordObject(bossTable, "Attack Timeline Duration");
        w.stateDuration = clipLength;
        if (w.comboWindowEnd > clipLength)
            w.comboWindowEnd = clipLength;
        EditorUtility.SetDirty(bossTable);
    }

    static string MovePopupLabel(BossMoveEntry entry, int index)
    {
        if (entry == null) return "空 " + index;
        string id = string.IsNullOrEmpty(entry.id) ? ("空 " + index) : entry.id;
        int segs = SegmentCount(entry);
        if (segs <= 1) return id;

        string chain = SequenceChain(entry);
        if (string.IsNullOrEmpty(chain))
            return id + "  ·" + segs + "段";
        return id + "  ·" + segs + "段  " + chain;
    }

    static string SequenceChain(BossMoveEntry entry)
    {
        BossAnimSequence seq = FirstSequence(entry);
        if (seq == null || seq.states == null || seq.states.Length == 0)
            return "";
        return string.Join(" → ", seq.states);
    }

    static int SegmentCount(BossMoveEntry entry)
    {
        if (entry == null) return 1;
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

    string CurrentAnimName()
    {
        if (playerConfig != null) return playerConfig.AnimName;
        if (bossTable == null || bossTable.moves == null || bossTable.moves.Length == 0)
            return null;
        int mi = Mathf.Clamp(moveIndex, 0, bossTable.moves.Length - 1);
        return StateName(bossTable.moves[mi], segmentIndex);
    }

    BossMoveWindow CurrentWindow()
    {
        if (bossTable == null || bossTable.moves == null || bossTable.moves.Length == 0)
            return null;
        BossMoveEntry entry = bossTable.moves[Mathf.Clamp(moveIndex, 0, bossTable.moves.Length - 1)];
        return BossMovePicker.WindowFor(entry, segmentIndex);
    }

    HitPulse[] CurrentStoredPulses()
    {
        if (playerConfig != null) return playerConfig.hitPulses;
        BossMoveWindow w = CurrentWindow();
        return w != null ? w.hitPulses : null;
    }

    AttackSfxCue[] CurrentStoredSfx()
    {
        if (playerConfig != null) return playerConfig.sfxCues;
        BossMoveWindow w = CurrentWindow();
        return w != null ? w.sfxCues : null;
    }

    float CurrentHitStart()
    {
        if (playerConfig != null) return playerConfig.HitStartTime;
        BossMoveWindow w = CurrentWindow();
        return w != null ? w.hitStartTime : 0.2f;
    }

    float CurrentRecover()
    {
        if (playerConfig != null) return playerConfig.RecoveryWindowStart;
        BossMoveWindow w = CurrentWindow();
        return w != null ? w.recoverStart : 0.8f;
    }

    static HitPulse[] DisplayPulses(HitPulse[] stored, float hitStart, float recover)
    {
        if (!AttackWindowSync.CanMeleeHit(hitStart, recover, stored))
            return new HitPulse[0];
        if (stored != null && stored.Length > 0)
            return stored;
        return new[] { new HitPulse { start = hitStart, end = recover } };
    }

    static HitPulse[] ClonePulses(HitPulse[] stored, float hitStart, float recover)
    {
        HitPulse[] src = DisplayPulses(stored, hitStart, recover);
        HitPulse[] copy = new HitPulse[src.Length];
        for (int i = 0; i < src.Length; i++)
        {
            HitPulse p = src[i];
            copy[i] = p == null ? new HitPulse { start = 0f, end = 0f } : p.Clone();
        }
        return copy;
    }

    static AttackSfxCue[] CloneSfx(AttackSfxCue[] stored)
    {
        if (stored == null || stored.Length == 0)
            return new AttackSfxCue[0];
        AttackSfxCue[] copy = new AttackSfxCue[stored.Length];
        for (int i = 0; i < stored.Length; i++)
        {
            AttackSfxCue s = stored[i];
            copy[i] = s == null
                ? new AttackSfxCue()
                : new AttackSfxCue { time = s.time, clip = s.clip };
        }
        return copy;
    }

    void ApplyPendingEdits(float clipLength)
    {
        if (pendingAddPulse)
        {
            forceNoHit = false;
            float t = scrub;
            AppendPulse(new HitPulse { start = t, end = Mathf.Min(clipLength, t + 0.12f) });
        }
        if (pendingClearMelee)
        {
            workingPulses = new HitPulse[0];
            forceNoHit = true;
        }
        if (pendingShrinkPulse)
            ShrinkPulses();
        if (pendingRemovePulse >= 0)
            RemovePulse(pendingRemovePulse);
        if (pendingAddSfx)
            AppendSfx(new AttackSfxCue { time = scrub });
        if (pendingRemoveSfx >= 0)
            RemoveSfx(pendingRemoveSfx);

        pendingAddPulse = false;
        pendingShrinkPulse = false;
        pendingClearMelee = false;
        pendingRemovePulse = -1;
        pendingAddSfx = false;
        pendingRemoveSfx = -1;
    }

    void RemovePulse(int index)
    {
        if (workingPulses == null || index < 0 || index >= workingPulses.Length) return;
        if (workingPulses.Length == 1)
        {
            workingPulses = new HitPulse[0];
            return;
        }
        HitPulse[] next = new HitPulse[workingPulses.Length - 1];
        int w = 0;
        for (int i = 0; i < workingPulses.Length; i++)
        {
            if (i == index) continue;
            next[w++] = workingPulses[i];
        }
        workingPulses = next;
    }

    void AppendPulse(HitPulse pulse)
    {
        int n = workingPulses == null ? 0 : workingPulses.Length;
        HitPulse[] next = new HitPulse[n + 1];
        if (workingPulses != null)
            System.Array.Copy(workingPulses, next, n);
        next[n] = pulse;
        workingPulses = next;
    }

    void ShrinkPulses()
    {
        if (workingPulses == null || workingPulses.Length == 0) return;
        if (workingPulses.Length == 1)
        {
            workingPulses = new HitPulse[0];
            return;
        }
        HitPulse[] next = new HitPulse[workingPulses.Length - 1];
        System.Array.Copy(workingPulses, next, next.Length);
        workingPulses = next;
    }

    void AppendSfx(AttackSfxCue cue)
    {
        int n = workingSfx == null ? 0 : workingSfx.Length;
        AttackSfxCue[] next = new AttackSfxCue[n + 1];
        if (workingSfx != null)
            System.Array.Copy(workingSfx, next, n);
        next[n] = cue;
        workingSfx = next;
    }

    void RemoveSfx(int index)
    {
        if (workingSfx == null || index < 0 || index >= workingSfx.Length) return;
        AttackSfxCue[] next = new AttackSfxCue[workingSfx.Length - 1];
        int w = 0;
        for (int i = 0; i < workingSfx.Length; i++)
        {
            if (i == index) continue;
            next[w++] = workingSfx[i];
        }
        workingSfx = next;
    }
}
