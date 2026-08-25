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
    [SerializeField] private PlayerInput playerInput;

    private InputActionAsset actions;
    private InputActionMap playerMap;
    private InputSystemUIInputModule uiModule;
    private string currentGroup = InputRebindService.KeyboardMouseGroup;
    private bool settingsOpen;
    private bool rebindInProgress;
    private int ignorePauseFrames;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    private GameObject pauseCanvas;
    private GameObject rootPanel;
    private GameObject settingsPanel;
    private TextMeshProUGUI waitingHint;
    private readonly List<TextMeshProUGUI> bindingLabels = new List<TextMeshProUGUI>();
    private Button resumeButton;
    private Button openSettingsButton;
    private Button quitButton;
    private Button keyboardTabButton;
    private Button gamepadTabButton;
    private Button resetButton;
    private Button backButton;
    private readonly List<Button> rebindRowButtons = new List<Button>();

    private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.07f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
    private static readonly Color TabActive = new Color(0.38f, 0.3f, 0.14f, 1f);
    private static readonly Color SelectedFill = new Color(0.95f, 0.78f, 0.22f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.88f, 0.78f, 1f);
    private static readonly Color SelectedTextColor = new Color(0.12f, 0.1f, 0.05f, 1f);
    private static readonly Color AccentColor = new Color(0.95f, 0.78f, 0.22f, 1f);

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
        playerMap = actions.FindActionMap("Player");
        EnsureEventSystem();
        BuildUI();
        WireNavigation();
        HideMenu();
    }

    private void OnDestroy()
    {
        CancelRebind();
        SetGameplayInput(true);
        if (GamePause.IsPaused) GamePause.SetPaused(false);
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

        if (settingsOpen)
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
        settingsOpen = false;
        CancelRebind();
        rootPanel.SetActive(true);
        settingsPanel.SetActive(false);
        SelectButton(resumeButton);
    }

    private void ShowSettings()
    {
        settingsOpen = true;
        rootPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshBindingLabels();
        RefreshTabs();
        SelectButton(keyboardTabButton);
    }

    private void HideMenu()
    {
        settingsOpen = false;
        rootPanel.SetActive(false);
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
        openSettingsButton = CreateMenuButton(rootPanel.transform, "设置", new Vector2(0f, -20f), ShowSettings);
        quitButton = CreateMenuButton(rootPanel.transform, "退出战斗", new Vector2(0f, -90f), RestartScene);

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
        backButton = CreateMenuButton(settingsPanel.transform, "返回", new Vector2(140f, -270f), ShowRoot, new Vector2(240f, 44f));
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

    private void SelectButton(Button button)
    {
        if (button == null || EventSystem.current == null) return;
        EventSystem.current.SetSelectedGameObject(null);
        EventSystem.current.SetSelectedGameObject(button.gameObject);
        StartCoroutine(SelectButtonNextFrame(button));
    }

    private IEnumerator SelectButtonNextFrame(Button button)
    {
        yield return null;
        if (button == null || EventSystem.current == null) yield break;
        if (!button.gameObject.activeInHierarchy) yield break;
        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }
}
