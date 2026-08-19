using TMPro;
using UnityEditor;
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

        GameObject old = GameObject.Find("CombatHUD");
        if (old != null) Undo.DestroyObjectImmediate(old);

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

        GameObject hud = CreateUi("CombatHUD", canvas.transform);
        RectStretch(hud.GetComponent<RectTransform>());

        BossStatusView bossStatus = BuildBossStatus(hud.transform, uiSprite);
        BossPostureBarView bossPosture = BuildBossPosture(hud.transform, uiSprite);
        PlayerStatusView playerStatus = BuildPlayerStatus(hud.transform, uiSprite);
        ItemSlotView items = BuildItemSlot(hud.transform, uiSprite);
        PerilousWarningView perilous = BuildCenterText(hud.transform, "PerilousWarning", "危",
            new Color(0.85f, 0.05f, 0.05f), 160);
        RevivePromptView revive = BuildPrompt(hud.transform, "RevivePrompt", "回生", "按攻击键复活",
            new Color(1f, 0.4f, 0.55f));
        GameOverView gameOver = BuildPromptAsGameOver(hud.transform);
        VictoryView victory = BuildVictory(hud.transform);

        LockOnIndicatorView lockOn = BuildLockOn(canvas.transform, boss.transform, knob);

        CombatUIController controller = hud.GetComponent<CombatUIController>();
        if (controller == null) controller = hud.AddComponent<CombatUIController>();

        SerializedObject so = new SerializedObject(controller);
        so.FindProperty("playerBody").objectReferenceValue = player;
        so.FindProperty("bossBody").objectReferenceValue = boss;
        so.FindProperty("bossStatusView").objectReferenceValue = bossStatus;
        so.FindProperty("bossPostureBarView").objectReferenceValue = bossPosture;
        so.FindProperty("playerStatusView").objectReferenceValue = playerStatus;
        so.FindProperty("itemSlotView").objectReferenceValue = items;
        so.FindProperty("lockOnIndicatorView").objectReferenceValue = lockOn;
        so.FindProperty("perilousWarningView").objectReferenceValue = perilous;
        so.FindProperty("revivePromptView").objectReferenceValue = revive;
        so.FindProperty("gameOverView").objectReferenceValue = gameOver;
        so.FindProperty("victoryView").objectReferenceValue = victory;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(controller);
        Selection.activeGameObject = hud;
        Debug.Log("[CombatHUD] 已生成。Play 后应看到左上 Boss 血条、顶栏架势、左下玩家血条/架势、右下葫芦。");
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

    private static BossPostureBarView BuildBossPosture(Transform parent, Sprite sprite)
    {
        GameObject root = CreateUi("BossPosture", parent);
        Rect(root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0, -16), new Vector2(640, 18));

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
            new Vector2(24, 24), new Vector2(380, 86));

        Image revive = CreateImage(CreateUi("ReviveDot", root.transform).transform, sprite,
            new Color(1f, 0.45f, 0.6f));
        Rect(revive.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1),
            new Vector2(8, -8), new Vector2(18, 18));

        Slider hp = CreateSlider(root.transform, "HPBar", sprite, new Color(0.7f, 0.12f, 0.12f));
        Rect(hp.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(0, 4), new Vector2(-8, 18));

        GameObject posture = CreateUi("Posture", root.transform);
        Rect(posture, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0),
            new Vector2(0, 8), new Vector2(0, 14));
        Image left = CreateFilled(posture.transform, "Left", sprite, new Color(0.95f, 0.75f, 0.2f),
            Image.OriginHorizontal.Right);
        Image right = CreateFilled(posture.transform, "Right", sprite, new Color(0.95f, 0.75f, 0.2f),
            Image.OriginHorizontal.Left);
        Rect(left.gameObject, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(1, 0.5f),
            Vector2.zero, Vector2.zero);
        Rect(right.gameObject, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 0.5f),
            Vector2.zero, Vector2.zero);

        PlayerStatusView view = root.AddComponent<PlayerStatusView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("reviveDots").arraySize = 1;
        so.FindProperty("reviveDots").GetArrayElementAtIndex(0).objectReferenceValue = revive;
        so.FindProperty("hpBar").objectReferenceValue = hp;
        so.FindProperty("postureLeftFill").objectReferenceValue = left;
        so.FindProperty("postureRightFill").objectReferenceValue = right;
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

    private static PerilousWarningView BuildCenterText(Transform parent, string name, string text,
        Color color, float fontSize)
    {
        GameObject root = CreateUi(name, parent);
        Rect(root, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(400, 200));
        TextMeshProUGUI tmp = CreateText(root.transform, "Text", text, fontSize, TextAlignmentOptions.Center);
        RectStretch(tmp.rectTransform);
        tmp.color = color;
        tmp.fontStyle = FontStyles.Bold;
        root.SetActive(false);

        PerilousWarningView view = root.AddComponent<PerilousWarningView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("warningText").objectReferenceValue = tmp;
        so.ApplyModifiedProperties();
        return view;
    }

    private static RevivePromptView BuildPrompt(Transform parent, string name, string title, string hint, Color color)
    {
        GameObject root = CreateUi(name, parent);
        RectStretch(root.GetComponent<RectTransform>());
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        TextMeshProUGUI titleT = CreateText(root.transform, "Title", title, 96, TextAlignmentOptions.Center);
        Rect(titleT.gameObject, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 140));
        titleT.color = color;
        titleT.fontStyle = FontStyles.Bold;
        TextMeshProUGUI hintT = CreateText(root.transform, "Hint", hint, 32, TextAlignmentOptions.Center);
        Rect(hintT.gameObject, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 50));
        hintT.color = Color.white;
        root.SetActive(false);

        RevivePromptView view = root.AddComponent<RevivePromptView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("reviveText").objectReferenceValue = titleT;
        so.FindProperty("hintText").objectReferenceValue = hintT;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.ApplyModifiedProperties();
        return view;
    }

    private static GameOverView BuildPromptAsGameOver(Transform parent)
    {
        GameObject root = CreateUi("GameOver", parent);
        RectStretch(root.GetComponent<RectTransform>());
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        TextMeshProUGUI death = CreateText(root.transform, "Title", "死", 120, TextAlignmentOptions.Center);
        Rect(death.gameObject, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(400, 160));
        death.color = new Color(0.8f, 0.05f, 0.05f);
        death.fontStyle = FontStyles.Bold;
        TextMeshProUGUI hint = CreateText(root.transform, "Hint", "按攻击键重新开始", 32, TextAlignmentOptions.Center);
        Rect(hint.gameObject, new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.42f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 50));
        root.SetActive(false);

        GameOverView view = root.AddComponent<GameOverView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("deathText").objectReferenceValue = death;
        so.FindProperty("hintText").objectReferenceValue = hint;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.ApplyModifiedProperties();
        return view;
    }

    private static VictoryView BuildVictory(Transform parent)
    {
        GameObject root = CreateUi("Victory", parent);
        RectStretch(root.GetComponent<RectTransform>());
        CanvasGroup group = root.AddComponent<CanvasGroup>();
        TextMeshProUGUI text = CreateText(root.transform, "Title", "胜利", 96, TextAlignmentOptions.Center);
        Rect(text.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(600, 160));
        text.color = new Color(1f, 0.85f, 0.3f);
        text.fontStyle = FontStyles.Bold;
        root.SetActive(false);

        VictoryView view = root.AddComponent<VictoryView>();
        SerializedObject so = new SerializedObject(view);
        so.FindProperty("victoryText").objectReferenceValue = text;
        so.FindProperty("canvasGroup").objectReferenceValue = group;
        so.ApplyModifiedProperties();
        return view;
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
