using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 场景里没有战斗 UI 时：菜单一键生成 Canvas + 各 View，并接到 CombatUIController。
// 用法：打开 GameScene → Tools/战斗/生成战斗 HUD
public static class CombatHUDBuilder
{
    [MenuItem("Tools/战斗/生成战斗 HUD")]
    public static void Build()
    {
        CharacterBody player = Object.FindObjectOfType<PlayerBrain>()?.GetComponent<CharacterBody>();
        CharacterBody boss = Object.FindObjectOfType<BTBrain>()?.GetComponent<CharacterBody>();
        if (player == null || boss == null)
        {
            EditorUtility.DisplayDialog("生成战斗 HUD",
                "场景里找不到玩家或 Boss（需要 PlayerBrain / BTBrain）。请先打开 GameScene。",
                "确定");
            return;
        }

        GameObject old = GameObject.Find("GamePanel");
        if (old == null) old = GameObject.Find("CombatHUD");
        if (old != null) Undo.DestroyObjectImmediate(old);
        DestroyNamed("EndPanel");
        DestroyNamed("RespawnPanel");
        DestroyNamed("Victory");
        DestroyNamed("RevivePrompt");

        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            GameObject canvasGo = new GameObject("CombatCanvas");
            Undo.RegisterCreatedObjectUndo(canvasGo, "Create CombatCanvas");
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();
        }

        GameObject hud = CreateUi("GamePanel", canvas.transform);
        RectStretch(hud.GetComponent<RectTransform>());

        BossStatusView bossStatus = BuildBossStatus(hud.transform, uiSprite);
        BossPostureBarView bossPosture = BuildPostureBar(hud.transform, uiSprite, "BossPosture",
            new Vector2(0.5f, 1f), new Vector2(0, -16));
        BossPostureBarView playerPosture = BuildPostureBar(hud.transform, uiSprite, "PlayerPosture",
            new Vector2(0.5f, 0f), new Vector2(0, 16));
        PlayerStatusView playerStatus = BuildPlayerStatus(hud.transform, uiSprite);
        ItemSlotView items = BuildItemSlot(hud.transform, uiSprite);
        PerilousWarningView perilous = Object.FindObjectOfType<PerilousWarningView>(true);
        if (perilous == null)
            Debug.LogWarning("[CombatHUD] 场景里没有危字 Billboard。先跑 Tools/战斗/生成危字特效。");
        HealKanjiView healKanji = Object.FindObjectOfType<HealKanjiView>(true);
        if (healKanji == null)
            Debug.LogWarning("[CombatHUD] 场景里没有治愈 Billboard。先跑 Tools/战斗/生成治愈特效。");
        RevivePromptView revive = BuildRespawnPanel(canvas.transform);
        GameOverView gameOver = BuildPromptAsGameOver(hud.transform);
        VictoryView victory = BuildVictory(canvas.transform);

        LockOnIndicatorView lockOn = BuildLockOn(canvas.transform, boss.transform, knob);

        CombatUIController controller = hud.GetComponent<CombatUIController>();
        if (controller == null) controller = hud.AddComponent<CombatUIController>();
        if (hud.GetComponent<BossVoiceDirector>() == null)
            hud.AddComponent<BossVoiceDirector>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("playerBody").objectReferenceValue = player;
        so.FindProperty("bossBody").objectReferenceValue = boss;
        so.FindProperty("bossStatusView").objectReferenceValue = bossStatus;
        so.FindProperty("bossPostureBarView").objectReferenceValue = bossPosture;
        so.FindProperty("playerPostureBarView").objectReferenceValue = playerPosture;
        so.FindProperty("playerStatusView").objectReferenceValue = playerStatus;
        so.FindProperty("itemSlotView").objectReferenceValue = items;
        so.FindProperty("lockOnIndicatorView").objectReferenceValue = lockOn;
        so.FindProperty("perilousWarningView").objectReferenceValue = perilous;
        so.FindProperty("healKanjiView").objectReferenceValue = healKanji;
        so.FindProperty("revivePromptView").objectReferenceValue = revive;
        so.FindProperty("gameOverView").objectReferenceValue = gameOver;
        so.FindProperty("victoryView").objectReferenceValue = victory;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        Selection.activeGameObject = hud;
        Debug.Log("[CombatHUD] 已生成。Play 后应看到左上 Boss 血条、顶栏 Boss 架势、底栏正中玩家架势、左下血条、右下葫芦。");
    }

    [MenuItem("Tools/战斗/生成 SettingPanel")]
    public static void BuildSettingPanel()
    {
        OrganizeCombatCanvasPanels(rebuildSettings: true);
    }

    [MenuItem("Tools/战斗/整理 CombatCanvas 面板")]
    public static void OrganizePanels()
    {
        OrganizeCombatCanvasPanels(rebuildSettings: true);
    }

    private static void OrganizeCombatCanvasPanels(bool rebuildSettings)
    {
        Canvas canvas = FindCombatCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("整理 CombatCanvas",
                "场景里找不到 CombatCanvas。请先打开 GameScene。", "确定");
            return;
        }

        Transform canvasTf = canvas.transform;
        if (PrefabUtility.IsOutermostPrefabInstanceRoot(canvas.gameObject))
            PrefabUtility.UnpackPrefabInstance(canvas.gameObject, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

        Transform hud = canvasTf.Find("GamePanel");
        if (hud == null) hud = canvasTf.Find("CombatHUD");
        if (hud != null && hud.name != "GamePanel")
        {
            Undo.RecordObject(hud.gameObject, "Rename GamePanel");
            hud.gameObject.name = "GamePanel";
        }

        ReparentNamed(canvasTf, hud, "RespawnPanel", "RevivePrompt");
        ReparentNamed(canvasTf, hud, "EndPanel", "Victory");

        if (hud != null)
            EnsureVoiceLine(hud);

        if (rebuildSettings)
        {
            PauseMenuController pause = Object.FindObjectOfType<PauseMenuController>(true);
            if (pause == null)
            {
                EditorUtility.DisplayDialog("生成 SettingPanel",
                    "场景里找不到 PauseMenuController（通常在 Mgr 上）。", "确定");
                return;
            }

            pause.EditorRebuildSettingPanel();
            EditorUtility.SetDirty(pause);
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        PrefabUtility.RecordPrefabInstancePropertyModifications(canvas.gameObject);

        Selection.activeGameObject = canvas.gameObject;
        BakeUiPreview();
        Debug.Log("[CombatHUD] CombatCanvas 面板：GamePanel / SettingPanel / EndPanel / RespawnPanel。字体在场景里改 TMP Font Asset，Play 不会再盖掉。");
    }

    [MenuItem("Tools/战斗/预览全部 UI")]
    public static void BakeUiPreview()
    {
        Canvas canvas = FindCombatCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("预览全部 UI", "场景里找不到 CombatCanvas。", "确定");
            return;
        }

        Transform canvasTf = canvas.transform;
        Transform hud = canvasTf.Find("GamePanel");
        ReparentNamed(canvasTf, hud, "GameOver", "GameOver");

        PauseMenuController pause = Object.FindObjectOfType<PauseMenuController>(true);
        if (pause != null)
            pause.EditorPreviewAllPages();

        ActivateRespawnPreview(canvasTf.Find("RespawnPanel"));
        BakeCombatOverlay(hud != null ? hud.Find("GameOver") : null, false, new Vector2(0f, -240f));
        BakeCombatOverlay(canvasTf.Find("GameOver"), false, new Vector2(0f, -240f));
        BakeCombatOverlay(canvasTf.Find("EndPanel"), true, new Vector2(560f, -240f));

        VoiceLineView voice = hud != null ? hud.GetComponentInChildren<VoiceLineView>(true) : null;
        if (voice != null)
        {
            voice.gameObject.SetActive(true);
            voice.OnViewInit();
            CanvasGroup vg = voice.GetComponent<CanvasGroup>();
            if (vg != null) vg.alpha = 1f;
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Selection.activeGameObject = canvas.gameObject;
        Debug.Log("[CombatHUD] 已把暂停/回生/真死/胜利铺到 GameScene。回生屏用场景里的倒地布局，不再生成。Play 后会收回。");
    }

    private static void BakeCombatOverlay(Transform root, bool withActions, Vector2 panelOffset)
    {
        if (root == null) return;

        UIView view = root.GetComponent<UIView>();
        if (view != null)
            view.OnViewInit();

        root.gameObject.SetActive(true);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        Transform dimmer = root.Find("Dimmer");
        if (dimmer == null) dimmer = root.Find("Vignette");
        if (dimmer != null)
            dimmer.gameObject.SetActive(false);

        Transform panel = root.Find("Panel");
        if (panel != null)
        {
            RectTransform rect = panel as RectTransform;
            if (rect != null)
                rect.anchoredPosition = panelOffset;
        }

        if (withActions)
        {
            CombatPromptStyle.EnsureActionButton(
                root, "再来一局", "Replay", new Vector2(-140f, -108f), new Vector2(220f, 52f));
            CombatPromptStyle.EnsureActionButton(
                root, "退出游戏", "Quit", new Vector2(140f, -108f), new Vector2(220f, 52f));
        }
    }

    private static Canvas FindCombatCanvas()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].name == "CombatCanvas")
                return canvases[i];
        }
        return Object.FindObjectOfType<Canvas>();
    }

    private static void ReparentNamed(Transform canvas, Transform hud, string name, string oldName)
    {
        Transform panel = canvas.Find(name);
        if (panel == null && hud != null) panel = hud.Find(name);
        if (panel == null) panel = canvas.Find(oldName);
        if (panel == null && hud != null) panel = hud.Find(oldName);
        if (panel == null) return;

        if (panel.name != name)
        {
            Undo.RecordObject(panel.gameObject, "Rename " + name);
            panel.gameObject.name = name;
        }

        if (panel.parent != canvas)
            Undo.SetTransformParent(panel, canvas, "Reparent " + name);
        panel.SetAsLastSibling();
    }

    private static void EnsureVoiceLine(Transform hud)
    {
        if (hud.GetComponentInChildren<VoiceLineView>(true) != null) return;
        GameObject go = CreateUi("VoiceLine", hud);
        go.AddComponent<CanvasGroup>();
        VoiceLineView view = go.AddComponent<VoiceLineView>();
        view.OnViewInit();
    }

    private static void DestroyNamed(string name)
    {
        GameObject go = FindSceneObject(name);
        if (go != null) Undo.DestroyObjectImmediate(go);
    }

    private static GameObject FindSceneObject(string name)
    {
        Transform[] all = Resources.FindObjectsOfTypeAll<Transform>();
        for (int i = 0; i < all.Length; i++)
        {
            Transform t = all[i];
            if (t == null || t.name != name) continue;
            if (!t.gameObject.scene.IsValid()) continue;
            return t.gameObject;
        }
        return null;
    }

    private static BossStatusView BuildBossStatus(Transform parent, Sprite sprite)
    {
        GameObject root = CreateUi("BossStatus", parent);
        Rect(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24, -24), new Vector2(420, 90));

        TextMeshProUGUI name = CreateText(root.transform, "Name", "苇名弦一郎", 28, TextAlignmentOptions.Left);
        Rect(name.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1),
            new Vector2(0, 0), new Vector2(0, 32));
        name.color = Color.white;

        GameObject dots = CreateUi("LifeDots", root.transform);
        Rect(dots, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(0, -36), new Vector2(80, 18));
        Image dot0 = CreateImage(CreateUi("Dot0", dots.transform).transform, sprite, new Color(0.75f, 0.08f, 0.08f));
        Image dot1 = CreateImage(CreateUi("Dot1", dots.transform).transform, sprite, new Color(0.75f, 0.08f, 0.08f));
        Rect(dot0.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(10, 0), new Vector2(16, 16));
        Rect(dot1.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(34, 0), new Vector2(16, 16));

        Slider hp = CreateSlider(root.transform, "HPBar", sprite, new Color(0.55f, 0.08f, 0.08f));
        Rect(hp.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 8), new Vector2(0, 22));

        BossStatusView view = root.AddComponent<BossStatusView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("hpBar").objectReferenceValue = hp;
        so.FindProperty("lifeSlots").arraySize = 2;
        so.FindProperty("lifeSlots").GetArrayElementAtIndex(0).objectReferenceValue = dot0.transform;
        so.FindProperty("lifeSlots").GetArrayElementAtIndex(1).objectReferenceValue = dot1.transform;
        so.FindProperty("nameText").objectReferenceValue = name;
        so.ApplyModifiedProperties();
        return view;
    }

    private static BossPostureBarView BuildPostureBar(Transform parent, Sprite sprite, string name,
        Vector2 anchor, Vector2 anchoredPos)
    {
        GameObject root = CreateUi(name, parent);
        Rect(root, anchor, anchor, anchor, anchoredPos, new Vector2(640, 18));

        Image left = CreateFilled(root.transform, "Left", sprite, new Color(0.95f, 0.75f, 0.2f),
            Image.OriginHorizontal.Right);
        Image right = CreateFilled(root.transform, "Right", sprite, new Color(0.95f, 0.75f, 0.2f),
            Image.OriginHorizontal.Left);
        Rect(left.gameObject, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero);
        Rect(right.gameObject, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);

        Image spike = CreateImage(CreateUi("Spike", root.transform).transform, sprite, new Color(1f, 0.45f, 0.1f));
        Rect(spike.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(12, 28));
        spike.gameObject.SetActive(false);

        BossPostureBarView view = root.AddComponent<BossPostureBarView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("leftFill").objectReferenceValue = left;
        so.FindProperty("rightFill").objectReferenceValue = right;
        so.FindProperty("spike").objectReferenceValue = spike;
        so.ApplyModifiedProperties();
        return view;
    }

    private static PlayerStatusView BuildPlayerStatus(Transform parent, Sprite sprite)
    {
        GameObject root = CreateUi("PlayerStatus", parent);
        Rect(root, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(24, 24), new Vector2(380, 64));

        Image revive = CreateImage(CreateUi("ReviveDot", root.transform).transform, sprite,
            new Color(1f, 0.45f, 0.6f));
        Rect(revive.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, -8), new Vector2(18, 18));

        Slider hp = CreateSlider(root.transform, "HPBar", sprite, new Color(0.7f, 0.12f, 0.12f));
        Rect(hp.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 8), new Vector2(-8, 18));

        PlayerStatusView view = root.AddComponent<PlayerStatusView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("reviveDots").arraySize = 1;
        so.FindProperty("reviveDots").GetArrayElementAtIndex(0).objectReferenceValue = revive;
        so.FindProperty("hpBar").objectReferenceValue = hp;
        so.ApplyModifiedProperties();
        return view;
    }

    private static ItemSlotView BuildItemSlot(Transform parent, Sprite sprite)
    {
        GameObject root = CreateUi("ItemSlot", parent);
        Rect(root, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0),
            new Vector2(-24, 24), new Vector2(120, 80));

        Image icon = CreateImage(CreateUi("GourdIcon", root.transform).transform, sprite,
            new Color(0.55f, 0.85f, 0.4f));
        Rect(icon.gameObject, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(48, 48));

        TextMeshProUGUI count = CreateText(root.transform, "Count", "10", 28, TextAlignmentOptions.Center);
        Rect(count.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 4), new Vector2(0, 28));

        ItemSlotView view = root.AddComponent<ItemSlotView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("gourdIcon").objectReferenceValue = icon;
        so.FindProperty("countText").objectReferenceValue = count;
        so.ApplyModifiedProperties();
        return view;
    }

    [MenuItem("Tools/战斗/同步回生倒地屏到场景")]
    public static void BakeRespawnDeathUi()
    {
        Canvas canvas = FindCombatCanvas();
        if (canvas == null)
        {
            EditorUtility.DisplayDialog("同步回生倒地屏", "场景里找不到 CombatCanvas。请先打开 GameScene。", "确定");
            return;
        }

        Transform root = canvas.transform.Find("RespawnPanel");
        if (root == null)
            root = canvas.transform.Find("GamePanel/RespawnPanel");
        if (root == null)
        {
            EditorUtility.DisplayDialog("同步回生倒地屏", "场景里找不到 RespawnPanel。", "确定");
            return;
        }

        BakeDeathLayout(root);
        root.gameObject.SetActive(true);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[CombatHUD] 已把倒地屏写进 GameScene/RespawnPanel。勾选该物体即可预览，Play 不会再生成子物体。");
    }

    private static RevivePromptView BuildRespawnPanel(Transform parent)
    {
        GameObject root = CreatePromptRoot(parent, "RespawnPanel");
        RevivePromptView view = root.AddComponent<RevivePromptView>();
        BakeDeathLayout(root.transform);
        root.SetActive(true);
        return view;
    }

    // 把只狼倒地屏写进场景。运行时 RevivePromptView 只读这些引用，不再 Instantiate。
    private static void BakeDeathLayout(Transform root)
    {
        if (root == null) return;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "Bake Respawn Death UI");

        while (root.childCount > 0)
            Undo.DestroyObjectImmediate(root.GetChild(0).gameObject);

        CanvasGroup rootGroup = root.GetComponent<CanvasGroup>();
        if (rootGroup == null)
            rootGroup = Undo.AddComponent<CanvasGroup>(root.gameObject);
        rootGroup.alpha = 1f;
        rootGroup.interactable = false;
        rootGroup.blocksRaycasts = false;

        RectStretch(root.GetComponent<RectTransform>());

        Image vignette = CreateRawImage(root, "Vignette", RevivePromptView.VignetteColor);
        vignette.raycastTarget = true;
        RectStretch(vignette.rectTransform);
        vignette.transform.SetAsFirstSibling();

        GameObject contentGo = CreateUi("DeathContent", root);
        CanvasGroup contentGroup = contentGo.AddComponent<CanvasGroup>();
        contentGroup.alpha = 1f;
        contentGroup.interactable = true;
        contentGroup.blocksRaycasts = true;
        RectStretch(contentGo.GetComponent<RectTransform>());

        TextMeshProUGUI deathKanji = CreateText(contentGo.transform, "DeathKanji", "死", 168f, TextAlignmentOptions.Center);
        deathKanji.color = RevivePromptView.DeathRed;
        PlaceUi(deathKanji.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 36f), new Vector2(520f, 220f));

        TextMeshProUGUI deathSub = CreateText(contentGo.transform, "DeathSub", "D E A T H", 22f, TextAlignmentOptions.Center);
        deathSub.color = RevivePromptView.DeathRed;
        deathSub.characterSpacing = 18f;
        PlaceUi(deathSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(400f, 40f));

        GameObject choicesGo = CreateUi("Choices", contentGo.transform);
        RectTransform choicesRect = choicesGo.GetComponent<RectTransform>();
        choicesRect.anchorMin = new Vector2(0.5f, 0f);
        choicesRect.anchorMax = new Vector2(0.5f, 0f);
        choicesRect.pivot = new Vector2(0.5f, 0f);
        choicesRect.anchoredPosition = new Vector2(0f, 88f);
        choicesRect.sizeDelta = new Vector2(900f, 48f);

        Button revive = CreateChoice(choicesGo.transform, "ReviveChoice", new Vector2(-220f, 0f), "起死回生", out TextMeshProUGUI reviveLabel);
        Button giveUp = CreateChoice(choicesGo.transform, "GiveUp", new Vector2(220f, 0f), "就此死去", out TextMeshProUGUI giveUpLabel);

        TmpChineseFont.ApplyAll(root);

        RevivePromptView view = root.GetComponent<RevivePromptView>();
        if (view == null)
            view = Undo.AddComponent<RevivePromptView>(root.gameObject);

        SerializedObject so = new SerializedObject(view);
        so.FindProperty("deathKanji").objectReferenceValue = deathKanji;
        so.FindProperty("deathSub").objectReferenceValue = deathSub;
        so.FindProperty("giveUpLabel").objectReferenceValue = giveUpLabel;
        so.FindProperty("reviveLabel").objectReferenceValue = reviveLabel;
        so.FindProperty("canvasGroup").objectReferenceValue = rootGroup;
        so.FindProperty("contentGroup").objectReferenceValue = contentGroup;
        so.FindProperty("vignette").objectReferenceValue = vignette;
        so.FindProperty("giveUpButton").objectReferenceValue = giveUp;
        so.FindProperty("reviveButton").objectReferenceValue = revive;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(view);
    }

    private static void ActivateRespawnPreview(Transform root)
    {
        if (root == null) return;
        if (root.Find("DeathContent") == null)
            BakeDeathLayout(root);

        root.gameObject.SetActive(true);
        CanvasGroup group = root.GetComponent<CanvasGroup>();
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }

        Transform content = root.Find("DeathContent");
        if (content != null)
        {
            CanvasGroup contentGroup = content.GetComponent<CanvasGroup>();
            if (contentGroup != null) contentGroup.alpha = 1f;
        }
    }

    private static Image CreateRawImage(Transform parent, string name, Color color)
    {
        GameObject go = CreateUi(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button CreateChoice(Transform parent, string name, Vector2 position, string labelText, out TextMeshProUGUI label)
    {
        GameObject go = CreateUi(name, parent);
        Image hit = go.AddComponent<Image>();
        hit.color = new Color(1f, 1f, 1f, 0f);
        hit.raycastTarget = true;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(360f, 44f);

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = hit;

        label = CreateText(go.transform, "Label", labelText, 26f, TextAlignmentOptions.Center);
        label.color = RevivePromptView.ChoiceIdle;
        RectStretch(label.rectTransform);
        return button;
    }

    private static void PlaceUi(RectTransform rect, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    private static GameOverView BuildPromptAsGameOver(Transform parent)
    {
        GameObject root = CreatePromptRoot(parent, "GameOver");
        TextMeshProUGUI death = CreateText(root.transform, "Title", "死", 42, TextAlignmentOptions.Center);
        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "按攻击键重新开始", 26, TextAlignmentOptions.Center);
        CombatPromptStyle.EnsureChrome(root.transform, death, hint, new Vector2(500f, 280f));
        root.SetActive(false);

        GameOverView view = root.AddComponent<GameOverView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("deathText").objectReferenceValue = death;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        so.ApplyModifiedProperties();
        return view;
    }

    private static VictoryView BuildVictory(Transform parent)
    {
        GameObject root = CreatePromptRoot(parent, "EndPanel");
        TextMeshProUGUI text = CreateText(root.transform, "Title", "胜利", 42, TextAlignmentOptions.Center);
        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "击败 苇名弦一郎", 26, TextAlignmentOptions.Center);
        CombatPromptStyle.EnsureChrome(root.transform, text, hint, new Vector2(500f, 340f), withActionButton: true);
        CombatPromptStyle.EnsureActionButton(root.transform, "再来一局");
        root.SetActive(false);

        VictoryView view = root.AddComponent<VictoryView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("victoryText").objectReferenceValue = text;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("canvasGroup").objectReferenceValue = root.GetComponent<CanvasGroup>();
        so.ApplyModifiedProperties();
        return view;
    }

    private static GameObject CreatePromptRoot(Transform parent, string name)
    {
        GameObject root = CreateUi(name, parent);
        RectStretch(root.GetComponent<RectTransform>());
        root.AddComponent<CanvasGroup>();
        return root;
    }

    private static LockOnIndicatorView BuildLockOn(Transform canvas, Transform boss, Sprite knob)
    {
        Transform old = canvas.Find("LockOnIndicator");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        GameObject go = CreateUi("LockOnIndicator", canvas);
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(64, 64);
        rt.localScale = Vector3.one;

        Image focus = CreateImage(CreateUi("FocusOn", go.transform).transform, knob, Color.white);
        Rect(focus.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(24, 24));
        focus.raycastTarget = false;

        LockOnIndicatorView view = go.AddComponent<LockOnIndicatorView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("focusOn").objectReferenceValue = focus.gameObject;
        Transform spine = FindNamed(boss, "Spine1") ?? FindNamed(boss, "Spine");
        so.FindProperty("followTarget").objectReferenceValue = spine;
        so.ApplyModifiedProperties();
        return view;
    }

    private static Transform FindNamed(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindNamed(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static GameObject CreateUi(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void RectStretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static void Rect(GameObject go, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 size)
    {
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static Image CreateImage(Transform parent, Sprite sprite, Color color)
    {
        Image img = parent.gameObject.GetComponent<Image>();
        if (img == null) img = parent.gameObject.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    private static Image CreateFilled(Transform parent, string name, Sprite sprite, Color color,
        Image.OriginHorizontal origin)
    {
        GameObject go = CreateUi(name, parent);
        Image img = go.AddComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)origin;
        img.fillAmount = 0f;
        img.raycastTarget = false;
        return img;
    }

    private static Slider CreateSlider(Transform parent, string name, Sprite sprite, Color fillColor)
    {
        GameObject go = CreateUi(name, parent);
        Image bg = go.AddComponent<Image>();
        bg.sprite = sprite;
        bg.color = new Color(0.12f, 0.12f, 0.12f, 0.85f);
        bg.raycastTarget = false;

        GameObject fillArea = CreateUi("Fill Area", go.transform);
        RectStretch(fillArea.GetComponent<RectTransform>());
        RectTransform fa = fillArea.GetComponent<RectTransform>();
        fa.offsetMin = new Vector2(2, 2);
        fa.offsetMax = new Vector2(-2, -2);

        GameObject fillGo = CreateUi("Fill", fillArea.transform);
        Image fill = fillGo.AddComponent<Image>();
        fill.sprite = sprite;
        fill.color = fillColor;
        fill.raycastTarget = false;
        RectStretch(fillGo.GetComponent<RectTransform>());

        Slider slider = go.AddComponent<Slider>();
        slider.fillRect = fillGo.GetComponent<RectTransform>();
        slider.targetGraphic = fill;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        slider.interactable = false;
        slider.transition = Selectable.Transition.None;
        return slider;
    }

    private static TextMeshProUGUI CreateText(Transform parent, string name, string content,
        float size, TextAlignmentOptions align)
    {
        GameObject go = CreateUi(name, parent);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = content;
        tmp.fontSize = size;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.color = Color.white;
        return tmp;
    }
}
