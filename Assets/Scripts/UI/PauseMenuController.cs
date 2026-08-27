using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// 暂停菜单：Esc / Start 打开。战斗 HUD 不管这里。
public class PauseMenuController : MonoBehaviour
{
    private enum PausePage { Root, Hub, Keybind }

    [SerializeField] private PlayerInput playerInput;
    [SerializeField] private Sprite volumeTrackSprite;
    [SerializeField] private Sprite settingsDividerSprite;
    [SerializeField] private Sprite closeIconSprite;

    private InputActionAsset actions;
    private InputActionMap playerMap;
    private InputSystemUIInputModule uiModule;
    private string currentGroup = InputRebindService.KeyboardMouseGroup;
    private PausePage page = PausePage.Root;
    private bool rebindInProgress;
    private int ignorePauseFrames;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    private GameObject pauseCanvas;
    private GameObject rootPanel;
    private GameObject hubPanel;
    private GameObject settingsPanel;
    private TextMeshProUGUI waitingHint;
    private readonly List<TextMeshProUGUI> bindingLabels = new List<TextMeshProUGUI>();
    private Button resumeButton;
    private Button openSettingsButton;
    private Button quitButton;
    private Button hubKeybindButton;
    private Button hubBackButton;
    private Button hubCloseButton;
    private Image hubCloseIcon;
    private Button keyboardTabButton;
    private Button gamepadTabButton;
    private Button resetButton;
    private Button backButton;
    private Slider bgmSlider;
    private Slider sfxSlider;
    private Image bgmHandle;
    private Image sfxHandle;
    private Image bgmFill;
    private Image sfxFill;
    private Image bgmTick;
    private Image sfxTick;
    private TextMeshProUGUI bgmTitleLabel;
    private TextMeshProUGUI sfxTitleLabel;
    private TextMeshProUGUI bgmValueLabel;
    private TextMeshProUGUI sfxValueLabel;
    private readonly List<Button> rebindRowButtons = new List<Button>();
    private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
    private Sprite diamondSprite;
    private Sprite fillBarSprite;

    private const string KitTrackPath =
        "Assets/Space_Exploration_GUI_Kit/Settings_&_Menu_Components/Large/sound-bar-container-large.png";
    private const string KitDividerPath =
        "Assets/Space_Exploration_GUI_Kit/Settings_&_Menu_Components/Large/settings-divider-large.png";
    private const string KitClosePath =
        "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/cross-128.png";

    private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.07f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color TabActive = new Color(0.38f, 0.3f, 0.14f, 1f);
    private static readonly Color SelectedFill = new Color(0.95f, 0.78f, 0.22f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.88f, 0.78f, 1f);
    private static readonly Color SelectedTextColor = new Color(0.12f, 0.1f, 0.05f, 1f);
    private static readonly Color AccentColor = new Color(0.89f, 0.64f, 0.22f, 1f);
    private static readonly Color HandleIdle = new Color(0.86f, 0.6f, 0.18f, 1f);
    private static readonly Color HandleSelected = new Color(1f, 0.82f, 0.38f, 1f);
    private static readonly Color FillIdle = new Color(0.9f, 0.68f, 0.24f, 1f);
    private static readonly Color FillSelected = new Color(1f, 0.82f, 0.4f, 1f);
    // 套件槽是深紫，乘暖色压掉青紫，只留暗槽形
    private static readonly Color TrackTint = new Color(0.92f, 0.78f, 0.48f, 1f);
    private static readonly Color DividerColor = new Color(0.89f, 0.64f, 0.22f, 0.4f);
    private static readonly Color SliderLabelIdle = new Color(0.93f, 0.93f, 0.93f, 1f);

    private void Start()
    {
        if (playerInput == null)
            playerInput = FindObjectOfType<PlayerInput>();

        actions = playerInput != null ? playerInput.actions : null;
        if (actions == null)
        {
            Debug.LogError("PauseMenuController 找不到 PlayerInput.actions，暂停菜单无法改键。");
            enabled = false;
            return;
        }

        InputRebindService.Load(actions);
        AudioVolumeSettings.Load();
        playerMap = actions.FindActionMap("Player");
        EnsureEventSystem();
        TmpChineseFont.EnsureReady();
        BuildUI();
        TmpChineseFont.ApplyAll(pauseCanvas.transform);
        WireNavigation();
        HideMenu();
    }

    private void OnDestroy()
    {
        CancelRebind();
        SetGameplayInput(true);
        if (GamePause.IsPaused) GamePause.SetPaused(false);
        for (int i = 0; i < generatedTextures.Count; i++)
        {
            if (generatedTextures[i] != null)
                Destroy(generatedTextures[i]);
        }
        generatedTextures.Clear();
    }

    private void Update()
    {
        if (ignorePauseFrames > 0) ignorePauseFrames--;

        bool escape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool start = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        bool back = GamePause.IsPaused
                    && !rebindInProgress
                    && Gamepad.current != null
                    && Gamepad.current.buttonEast.wasPressedThisFrame;

        if (escape || start || back)
            HandleMenuKeys(escape, start);

        if (GamePause.IsPaused)
            RefreshSelectionVisual();
    }

    // Esc：开暂停 / 返回上一级。Start：开关暂停。B：只在菜单里返回上一级。
    private void HandleMenuKeys(bool escape, bool start)
    {
        if (ignorePauseFrames > 0) return;

        if (rebindInProgress)
        {
            CancelRebind();
            return;
        }

        if (start)
        {
            if (GamePause.IsPaused) Resume();
            else OpenPause();
            return;
        }

        if (!GamePause.IsPaused)
        {
            if (escape) OpenPause();
            return;
        }

        if (page == PausePage.Keybind)
        {
            ShowHub();
            return;
        }

        if (page == PausePage.Hub)
        {
            ShowRoot();
            return;
        }

        Resume();
    }

    private void OpenPause()
    {
        GamePause.SetPaused(true);
        SetGameplayInput(false);
        pauseCanvas.SetActive(true);
        ShowRoot();
    }

    private void Resume()
    {
        CancelRebind();
        HideMenu();
        SetGameplayInput(true);
        GamePause.SetPaused(false);
    }

    private void RestartScene()
    {
        CancelRebind();
        SetGameplayInput(true);
        GamePause.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ShowRoot()
    {
        CancelRebind();
        page = PausePage.Root;
        rootPanel.SetActive(true);
        hubPanel.SetActive(false);
        settingsPanel.SetActive(false);
        SelectButton(resumeButton);
    }

    private void ShowHub()
    {
        CancelRebind();
        page = PausePage.Hub;
        rootPanel.SetActive(false);
        hubPanel.SetActive(true);
        settingsPanel.SetActive(false);
        AudioVolumeSettings.Load();
        bgmSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Bgm * 100f));
        sfxSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Sfx * 100f));
        RefreshAudioLabels();
        SelectSelectable(bgmSlider);
    }

    private void ShowSettings()
    {
        page = PausePage.Keybind;
        rootPanel.SetActive(false);
        hubPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshBindingLabels();
        RefreshTabs();
        SelectButton(keyboardTabButton);
    }

    private void HideMenu()
    {
        CancelRebind();
        page = PausePage.Root;
        rootPanel.SetActive(false);
        hubPanel.SetActive(false);
        settingsPanel.SetActive(false);
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(null);
        pauseCanvas.SetActive(false);
    }

    private void SwitchGroup(string group)
    {
        if (rebindInProgress) return;
        currentGroup = group;
        RefreshBindingLabels();
        RefreshTabs();
    }

    private void RefreshTabs()
    {
        ApplyButtonVisual(keyboardTabButton,
            currentGroup == InputRebindService.KeyboardMouseGroup,
            IsUiSelected(keyboardTabButton));
        ApplyButtonVisual(gamepadTabButton,
            currentGroup == InputRebindService.GamepadGroup,
            IsUiSelected(gamepadTabButton));
    }

    private void RefreshSelectionVisual()
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        ApplyButtonVisual(resumeButton, false, IsUiSelected(resumeButton, selected));
        ApplyButtonVisual(openSettingsButton, false, IsUiSelected(openSettingsButton, selected));
        ApplyButtonVisual(quitButton, false, IsUiSelected(quitButton, selected));
        ApplyButtonVisual(hubKeybindButton, false, IsUiSelected(hubKeybindButton, selected));
        ApplyButtonVisual(hubBackButton, false, IsUiSelected(hubBackButton, selected));
        ApplyCloseVisual(hubCloseButton, hubCloseIcon, selected);
        ApplyButtonVisual(keyboardTabButton,
            currentGroup == InputRebindService.KeyboardMouseGroup,
            IsUiSelected(keyboardTabButton, selected));
        ApplyButtonVisual(gamepadTabButton,
            currentGroup == InputRebindService.GamepadGroup,
            IsUiSelected(gamepadTabButton, selected));
        for (int i = 0; i < rebindRowButtons.Count; i++)
            ApplyButtonVisual(rebindRowButtons[i], false, IsUiSelected(rebindRowButtons[i], selected));
        ApplyButtonVisual(resetButton, false, IsUiSelected(resetButton, selected));
        ApplyButtonVisual(backButton, false, IsUiSelected(backButton, selected));
        ApplySliderVisual(bgmSlider, bgmHandle, bgmFill, bgmTick, bgmTitleLabel, bgmValueLabel);
        ApplySliderVisual(sfxSlider, sfxHandle, sfxFill, sfxTick, sfxTitleLabel, sfxValueLabel);
    }

    private static bool IsUiSelected(Button button)
    {
        GameObject selected = EventSystem.current != null
            ? EventSystem.current.currentSelectedGameObject
            : null;
        return IsUiSelected(button, selected);
    }

    private static bool IsUiSelected(Button button, GameObject selected)
    {
        return button != null && selected != null && button.gameObject == selected;
    }

    private static void ApplyButtonVisual(Button button, bool schemeActive, bool uiSelected)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
        if (uiSelected)
        {
            if (image != null) image.color = SelectedFill;
            if (label != null) label.color = SelectedTextColor;
            return;
        }

        if (image != null) image.color = schemeActive ? TabActive : ButtonColor;
        if (label != null) label.color = TextColor;
    }

    private static void ApplyCloseVisual(Button button, Image icon, GameObject selected)
    {
        if (icon == null) return;
        icon.color = IsUiSelected(button, selected) ? AccentColor : TextColor;
    }

    private void ApplySliderVisual(
        Slider slider,
        Image handle,
        Image fill,
        Image tick,
        TextMeshProUGUI titleLabel,
        TextMeshProUGUI valueLabel)
    {
        if (slider == null) return;
        bool selected = EventSystem.current != null
                        && EventSystem.current.currentSelectedGameObject == slider.gameObject;
        Color text = selected ? AccentColor : SliderLabelIdle;
        if (titleLabel != null) titleLabel.color = text;
        if (valueLabel != null) valueLabel.color = text;
        if (handle != null) handle.color = selected ? HandleSelected : HandleIdle;
        if (fill != null) fill.color = selected ? FillSelected : FillIdle;
        if (tick != null) tick.enabled = selected;
    }

    private void RefreshBindingLabels()
    {
        for (int i = 0; i < InputRebindService.RemappableActions.Length; i++)
        {
            InputAction action = actions.FindAction(InputRebindService.RemappableActions[i]);
            bindingLabels[i].text = InputRebindService.GetBindingDisplay(action, currentGroup);
            bindingLabels[i].color = TextColor;
        }

        waitingHint.text = string.Empty;
    }

    private void RefreshAudioLabels()
    {
        if (bgmValueLabel != null && bgmSlider != null)
            bgmValueLabel.text = Mathf.RoundToInt(bgmSlider.value) + "%";
        if (sfxValueLabel != null && sfxSlider != null)
            sfxValueLabel.text = Mathf.RoundToInt(sfxSlider.value) + "%";
    }

    private void OnRowClicked(int rowIndex)
    {
        if (rebindInProgress) return;
        StartCoroutine(StartRebindAfterPointerUp(rowIndex));
    }

    private IEnumerator StartRebindAfterPointerUp(int rowIndex)
    {
        yield return null;
        while (Mouse.current != null && Mouse.current.leftButton.isPressed)
            yield return null;
        while (Gamepad.current != null && Gamepad.current.buttonSouth.isPressed)
            yield return null;

        InputAction action = actions.FindAction(InputRebindService.RemappableActions[rowIndex]);
        int bindingIndex = InputRebindService.FindBindingIndex(action, currentGroup);
        if (action == null || bindingIndex < 0) yield break;

        rebindInProgress = true;
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = false;
        bindingLabels[rowIndex].text = "按下新按键…";
        bindingLabels[rowIndex].color = AccentColor;
        waitingHint.text = currentGroup == InputRebindService.KeyboardMouseGroup
            ? "等待键盘或鼠标…  Esc 取消"
            : "等待手柄按键…  B 取消";

        try
        {
            rebindOperation = InputRebindService.BeginRebind(
                action,
                bindingIndex,
                currentGroup,
                () => FinishRebind(),
                () => FinishRebind());
        }
        catch (System.Exception e)
        {
            Debug.LogError($"改键失败：{e.Message}");
            FinishRebind();
        }
    }

    private void FinishRebind()
    {
        rebindInProgress = false;
        rebindOperation = null;
        ignorePauseFrames = 2;
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
        RefreshBindingLabels();
    }

    private void CancelRebind()
    {
        if (rebindOperation == null) return;
        rebindOperation.Cancel();
        rebindOperation = null;
        rebindInProgress = false;
        if (EventSystem.current != null)
            EventSystem.current.sendNavigationEvents = true;
    }

    private void ResetCurrentGroup()
    {
        if (rebindInProgress) return;
        InputRebindService.ResetGroup(actions, currentGroup);
        RefreshBindingLabels();
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject go = new GameObject("EventSystem");
            eventSystem = go.AddComponent<EventSystem>();
            uiModule = go.AddComponent<InputSystemUIInputModule>();
        }
        else
        {
            uiModule = eventSystem.GetComponent<InputSystemUIInputModule>();
            if (uiModule == null)
                uiModule = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        eventSystem.sendNavigationEvents = true;
        uiModule.deselectOnBackgroundClick = false;
        uiModule.moveRepeatDelay = 0.3f;
        uiModule.moveRepeatRate = 0.1f;
    }

    private void SetGameplayInput(bool enabled)
    {
        // 胜利结算仍要挡住玩家，并把设备留给胜利按钮
        if (enabled && CombatInputGate.Blocked)
            enabled = false;

        // 暂停时把设备从 PlayerInput 上放开，否则手柄被独占，默认 UI 模块收不到摇杆
        if (playerInput != null)
        {
            if (enabled) playerInput.ActivateInput();
            else playerInput.DeactivateInput();
            return;
        }

        if (playerMap == null) return;
        if (enabled) playerMap.Enable();
        else playerMap.Disable();
    }

    private void BuildUI()
    {
        EnsureSliderSprites();
        GameObject canvasObject = new GameObject("PauseCanvas");
        canvasObject.transform.SetParent(transform, false);
        pauseCanvas = canvasObject;

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image dimmer = CreateImage(canvasObject.transform, "Dimmer", new Color(0f, 0f, 0f, 0.72f));
        dimmer.raycastTarget = false;
        StretchFull(dimmer.rectTransform);

        rootPanel = CreatePanel(canvasObject.transform, "RootPanel", new Vector2(460f, 420f));
        CreateLabel(rootPanel.transform, "暂停", 42f, TextAlignmentOptions.Center, new Vector2(0f, 150f), new Vector2(400f, 60f));
        resumeButton = CreateMenuButton(rootPanel.transform, "继续战斗", new Vector2(0f, 50f), Resume);
        openSettingsButton = CreateMenuButton(rootPanel.transform, "设置", new Vector2(0f, -20f), ShowHub);
        quitButton = CreateMenuButton(rootPanel.transform, "退出战斗", new Vector2(0f, -90f), RestartScene);

        hubPanel = CreatePanel(canvasObject.transform, "HubPanel", new Vector2(560f, 500f));
        CreateLabel(hubPanel.transform, "设置", 42f, TextAlignmentOptions.Center,
            new Vector2(0f, 190f), new Vector2(500f, 60f));
        hubCloseButton = CreateCloseButton(hubPanel.transform, ShowRoot);
        hubCloseIcon = hubCloseButton.transform.Find("Icon").GetComponent<Image>();
        CreateSliderRow(hubPanel.transform, "音乐音量", new Vector2(0f, 100f), true);
        CreateSliderRow(hubPanel.transform, "音效音量", new Vector2(0f, 36f), false);
        hubKeybindButton = CreateMenuButton(hubPanel.transform, "键位设置", new Vector2(0f, -50f), ShowSettings);
        hubBackButton = CreateMenuButton(hubPanel.transform, "返回", new Vector2(0f, -118f), ShowRoot);

        settingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel", new Vector2(640f, 720f));
        CreateLabel(settingsPanel.transform, "键位设置", 36f, TextAlignmentOptions.Center, new Vector2(0f, 310f), new Vector2(560f, 50f));

        keyboardTabButton = CreateMenuButton(settingsPanel.transform, "键盘鼠标", new Vector2(-140f, 250f),
            () => SwitchGroup(InputRebindService.KeyboardMouseGroup), new Vector2(240f, 44f));
        gamepadTabButton = CreateMenuButton(settingsPanel.transform, "手柄", new Vector2(140f, 250f),
            () => SwitchGroup(InputRebindService.GamepadGroup), new Vector2(240f, 44f));

        for (int i = 0; i < InputRebindService.RemappableActions.Length; i++)
        {
            float y = 180f - i * 58f;
            CreateRebindRow(settingsPanel.transform, i, y);
        }

        waitingHint = CreateLabel(settingsPanel.transform, string.Empty, 22f, TextAlignmentOptions.Center,
            new Vector2(0f, -200f), new Vector2(560f, 36f));
        waitingHint.color = AccentColor;

        resetButton = CreateMenuButton(settingsPanel.transform, "恢复默认", new Vector2(-140f, -270f), ResetCurrentGroup, new Vector2(240f, 44f));
        backButton = CreateMenuButton(settingsPanel.transform, "返回", new Vector2(140f, -270f), ShowHub, new Vector2(240f, 44f));
    }

    private void CreateRebindRow(Transform parent, int rowIndex, float y)
    {
        GameObject row = new GameObject($"Row_{InputRebindService.RemappableActions[rowIndex]}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(560f, 50f);
        rect.anchoredPosition = new Vector2(0f, y);

        CreateLabel(row.transform, InputRebindService.RemappableLabels[rowIndex], 26f, TextAlignmentOptions.Left,
            new Vector2(-150f, 0f), new Vector2(200f, 44f));

        Button rowButton = CreateMenuButton(row.transform, "—", new Vector2(140f, 0f),
            () => OnRowClicked(rowIndex), new Vector2(260f, 44f));
        TextMeshProUGUI label = rowButton.GetComponentInChildren<TextMeshProUGUI>();
        bindingLabels.Add(label);
        rebindRowButtons.Add(rowButton);
    }

    private void CreateSliderRow(Transform parent, string title, Vector2 position, bool isBgm)
    {
        EnsureSliderSprites();

        GameObject row = new GameObject(title + "Row", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        RectTransform rowRect = row.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
        rowRect.sizeDelta = new Vector2(500f, 56f);
        rowRect.anchoredPosition = position;

        Image tick = CreateImage(row.transform, "Tick", AccentColor);
        RectTransform tickRect = tick.rectTransform;
        tickRect.anchorMin = tickRect.anchorMax = tickRect.pivot = new Vector2(0f, 0.5f);
        tickRect.sizeDelta = new Vector2(3f, 22f);
        tickRect.anchoredPosition = new Vector2(4f, 0f);
        tick.raycastTarget = false;
        tick.enabled = false;

        TextMeshProUGUI titleLabel = CreateLabel(row.transform, title, 24f, TextAlignmentOptions.Left,
            new Vector2(-175f, 0f), new Vector2(130f, 36f));
        titleLabel.color = SliderLabelIdle;

        TextMeshProUGUI valueLabel = CreateLabel(row.transform, "80%", 24f, TextAlignmentOptions.Right,
            new Vector2(-78f, 0f), new Vector2(60f, 36f));
        valueLabel.color = SliderLabelIdle;

        Image hit = CreateImage(row.transform, "Slider", new Color(1f, 1f, 1f, 0f));
        RectTransform hitRect = hit.rectTransform;
        hitRect.anchorMin = hitRect.anchorMax = hitRect.pivot = new Vector2(0.5f, 0.5f);
        hitRect.sizeDelta = new Vector2(236f, 40f);
        hitRect.anchoredPosition = new Vector2(138f, 0f);

        Image track = CreateImage(hit.transform, "Track", TrackTint);
        track.sprite = volumeTrackSprite;
        track.preserveAspect = false;
        track.raycastTarget = false;
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = new Vector2(0f, 0.5f);
        trackRect.anchorMax = new Vector2(1f, 0.5f);
        trackRect.pivot = new Vector2(0.5f, 0.5f);
        trackRect.sizeDelta = new Vector2(0f, 26f);
        trackRect.anchoredPosition = Vector2.zero;
        Mask trackMask = track.gameObject.AddComponent<Mask>();
        trackMask.showMaskGraphic = true;

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(track.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = new Vector2(0f, 0.5f);
        fillAreaRect.anchorMax = new Vector2(1f, 0.5f);
        fillAreaRect.pivot = new Vector2(0.5f, 0.5f);
        fillAreaRect.sizeDelta = new Vector2(-16f, 16f);
        fillAreaRect.anchoredPosition = Vector2.zero;

        Image fill = CreateImage(fillArea.transform, "Fill", FillIdle);
        fill.sprite = fillBarSprite;
        fill.type = Image.Type.Simple;
        fill.raycastTarget = false;
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillRect.sizeDelta = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(hit.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = new Vector2(0f, 0f);
        handleAreaRect.anchorMax = new Vector2(1f, 1f);
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        GameObject handleRoot = new GameObject("Handle", typeof(RectTransform));
        handleRoot.transform.SetParent(handleArea.transform, false);
        RectTransform handleRect = handleRoot.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = Vector2.zero;

        Image handle = CreateImage(handleRoot.transform, "HandleGraphic", HandleIdle);
        handle.sprite = diamondSprite;
        handle.preserveAspect = true;
        handle.raycastTarget = false;
        RectTransform handleGraphicRect = handle.rectTransform;
        handleGraphicRect.anchorMin = handleGraphicRect.anchorMax = handleGraphicRect.pivot = new Vector2(0.5f, 0.5f);
        handleGraphicRect.sizeDelta = new Vector2(16f, 26f);

        Slider slider = hit.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.minValue = 0f;
        slider.maxValue = 100f;
        slider.wholeNumbers = true;
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.Explicit };

        Image divider = CreateImage(row.transform, "Divider", DividerColor);
        divider.sprite = settingsDividerSprite;
        divider.preserveAspect = false;
        divider.raycastTarget = false;
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = new Vector2(0.5f, 0f);
        dividerRect.anchorMax = new Vector2(0.5f, 0f);
        dividerRect.pivot = new Vector2(0.5f, 0.5f);
        dividerRect.sizeDelta = new Vector2(480f, 6f);
        dividerRect.anchoredPosition = Vector2.zero;

        if (isBgm)
        {
            bgmSlider = slider;
            bgmHandle = handle;
            bgmFill = fill;
            bgmTick = tick;
            bgmTitleLabel = titleLabel;
            bgmValueLabel = valueLabel;
            slider.onValueChanged.AddListener(v =>
            {
                AudioVolumeSettings.SetBgm(v / 100f);
                RefreshAudioLabels();
            });
        }
        else
        {
            sfxSlider = slider;
            sfxHandle = handle;
            sfxFill = fill;
            sfxTick = tick;
            sfxTitleLabel = titleLabel;
            sfxValueLabel = valueLabel;
            slider.onValueChanged.AddListener(v =>
            {
                AudioVolumeSettings.SetSfx(v / 100f);
                RefreshAudioLabels();
            });
        }
    }

    private void EnsureSliderSprites()
    {
        if (diamondSprite == null)
            diamondSprite = CreateDiamondSprite();
        if (fillBarSprite == null)
            fillBarSprite = CreateFillBarSprite();
        if (volumeTrackSprite == null)
            volumeTrackSprite = LoadKitSprite(KitTrackPath);
        if (settingsDividerSprite == null)
            settingsDividerSprite = LoadKitSprite(KitDividerPath);
        if (closeIconSprite == null)
            closeIconSprite = LoadKitSprite(KitClosePath);
    }

    private static Sprite LoadKitSprite(string assetPath)
    {
#if UNITY_EDITOR
        UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            Sprite sprite = assets[i] as Sprite;
            if (sprite != null)
                return sprite;
        }
#endif
        return null;
    }

    private Sprite CreateFillBarSprite()
    {
        const int size = 8;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply(false, false);
        generatedTextures.Add(tex);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
    }

    private Sprite CreateDiamondSprite()
    {
        const int size = 64;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        Color[] pixels = new Color[size * size];
        float cx = (size - 1) * 0.5f;
        float cy = (size - 1) * 0.5f;
        float hw = 15.5f;
        float hh = 26.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Abs(x - cx) / hw + Mathf.Abs(y - cy) / hh;
                float alpha = Mathf.Clamp01((1.08f - d) / 0.12f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, false);
        generatedTextures.Add(tex);
        return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
    }

    private GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        Image image = CreateImage(parent, name, PanelColor);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        return image.gameObject;
    }

    private Button CreateMenuButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
    {
        Image image = CreateImage(parent, text, ButtonColor);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size ?? new Vector2(320f, 56f);
        rect.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        // ColorTint 会每帧盖掉 Image.color，选中态必须自己画
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
        button.onClick.AddListener(onClick);

        CreateLabel(image.transform, text, 26f, TextAlignmentOptions.Center, Vector2.zero, rect.sizeDelta);
        return button;
    }

    private Button CreateCloseButton(Transform parent, UnityEngine.Events.UnityAction onClick)
    {
        Image hit = CreateImage(parent, "Close", new Color(1f, 1f, 1f, 0f));
        RectTransform rect = hit.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(48f, 48f);
        rect.anchoredPosition = new Vector2(-8f, -8f);

        Image icon = CreateImage(hit.transform, "Icon", TextColor);
        icon.sprite = closeIconSprite;
        icon.preserveAspect = true;
        icon.raycastTarget = false;
        RectTransform iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(28f, 28f);
        iconRect.anchoredPosition = Vector2.zero;

        Button button = hit.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.targetGraphic = icon;
        button.onClick.AddListener(onClick);
        return button;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent,
        string text,
        float fontSize,
        TextAlignmentOptions align,
        Vector2 position,
        Vector2 size)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        TmpChineseFont.Apply(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = TextColor;
        tmp.alignment = align;
        tmp.raycastTarget = false;

        RectTransform rect = tmp.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return tmp;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private void WireNavigation()
    {
        SetNav(resumeButton, null, openSettingsButton, null, null);
        SetNav(openSettingsButton, resumeButton, quitButton, null, null);
        SetNav(quitButton, openSettingsButton, null, null, null);

        SetSliderNav(bgmSlider, null, sfxSlider);
        SetSliderNav(sfxSlider, bgmSlider, hubKeybindButton);
        Navigation keybindNav = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = sfxSlider,
            selectOnDown = hubBackButton
        };
        hubKeybindButton.navigation = keybindNav;
        SetNav(hubBackButton, hubKeybindButton, null, null, null);

        Button firstRow = rebindRowButtons.Count > 0 ? rebindRowButtons[0] : resetButton;
        Button lastRow = rebindRowButtons.Count > 0 ? rebindRowButtons[rebindRowButtons.Count - 1] : keyboardTabButton;

        SetNav(keyboardTabButton, null, firstRow, null, gamepadTabButton);
        SetNav(gamepadTabButton, null, firstRow, keyboardTabButton, null);

        for (int i = 0; i < rebindRowButtons.Count; i++)
        {
            Button up = i == 0 ? keyboardTabButton : rebindRowButtons[i - 1];
            Button down = i == rebindRowButtons.Count - 1 ? resetButton : rebindRowButtons[i + 1];
            SetNav(rebindRowButtons[i], up, down, null, null);
        }

        SetNav(resetButton, lastRow, null, null, backButton);
        SetNav(backButton, lastRow, null, resetButton, null);
    }

    private static void SetNav(Button button, Button up, Button down, Button left, Button right)
    {
        if (button == null) return;
        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down,
            selectOnLeft = left,
            selectOnRight = right
        };
        button.navigation = navigation;
    }

    private static void SetSliderNav(Slider slider, Selectable up, Selectable down)
    {
        if (slider == null) return;
        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnUp = up,
            selectOnDown = down
        };
        slider.navigation = navigation;
    }

    private void SelectButton(Button button)
    {
        SelectSelectable(button);
    }

    private void SelectSelectable(Selectable selectable)
    {
        if (selectable == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        StartCoroutine(SelectNextFrame(selectable.gameObject));
    }

    private IEnumerator SelectNextFrame(GameObject target)
    {
        yield return null;
        if (target == null || EventSystem.current == null) yield break;
        if (!target.activeInHierarchy) yield break;
        EventSystem.current.SetSelectedGameObject(target);
    }
}
