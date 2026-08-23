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
    private string currentGroup = InputRebindService.KeyboardMouseGroup;
    private bool settingsOpen;
    private bool rebindInProgress;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;

    private GameObject rootPanel;
    private GameObject settingsPanel;
    private TextMeshProUGUI waitingHint;
    private readonly List<TextMeshProUGUI> bindingLabels = new List<TextMeshProUGUI>();
    private Image keyboardTabImage;
    private Image gamepadTabImage;

    private static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.07f, 0.96f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color TabIdle = new Color(0.16f, 0.16f, 0.16f, 1f);
    private static readonly Color TabActive = new Color(0.32f, 0.26f, 0.14f, 1f);
    private static readonly Color TextColor = new Color(0.92f, 0.88f, 0.78f, 1f);
    private static readonly Color AccentColor = new Color(0.82f, 0.68f, 0.32f, 1f);

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
        EnsureEventSystem();
        BuildUI();
        HideMenu();
    }

    private void OnDestroy()
    {
        CancelRebind();
        if (GamePause.IsPaused) GamePause.SetPaused(false);
    }

    private void Update()
    {
        if (WasPausePressed())
            HandleEscape();
    }

    private bool WasPausePressed()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return true;

        Gamepad gamepad = Gamepad.current;
        return gamepad != null && gamepad.startButton.wasPressedThisFrame;
    }

    private void HandleEscape()
    {
        if (rebindInProgress)
        {
            CancelRebind();
            return;
        }

        if (!GamePause.IsPaused)
        {
            OpenPause();
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
        ShowRoot();
        rootPanel.transform.parent.gameObject.SetActive(true);
    }

    private void Resume()
    {
        CancelRebind();
        HideMenu();
        GamePause.SetPaused(false);
    }

    private void RestartScene()
    {
        CancelRebind();
        GamePause.SetPaused(false);
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void ShowRoot()
    {
        settingsOpen = false;
        CancelRebind();
        rootPanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    private void ShowSettings()
    {
        settingsOpen = true;
        rootPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshBindingLabels();
        RefreshTabs();
    }

    private void HideMenu()
    {
        settingsOpen = false;
        rootPanel.SetActive(false);
        settingsPanel.SetActive(false);
        rootPanel.transform.parent.gameObject.SetActive(false);
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
        keyboardTabImage.color = currentGroup == InputRebindService.KeyboardMouseGroup ? TabActive : TabIdle;
        gamepadTabImage.color = currentGroup == InputRebindService.GamepadGroup ? TabActive : TabIdle;
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

        InputAction action = actions.FindAction(InputRebindService.RemappableActions[rowIndex]);
        int bindingIndex = InputRebindService.FindBindingIndex(action, currentGroup);
        if (action == null || bindingIndex < 0) yield break;

        rebindInProgress = true;
        bindingLabels[rowIndex].text = "按下新按键…";
        bindingLabels[rowIndex].color = AccentColor;
        waitingHint.text = currentGroup == InputRebindService.KeyboardMouseGroup
            ? "等待键盘或鼠标…  Esc 取消"
            : "等待手柄按键…  B 取消";

        rebindOperation = InputRebindService.BeginRebind(
            action,
            bindingIndex,
            currentGroup,
            () => FinishRebind(),
            () => FinishRebind());
    }

    private void FinishRebind()
    {
        rebindInProgress = false;
        rebindOperation = null;
        RefreshBindingLabels();
    }

    private void CancelRebind()
    {
        if (rebindOperation == null) return;
        rebindOperation.Cancel();
        rebindOperation = null;
        rebindInProgress = false;
    }

    private void ResetCurrentGroup()
    {
        if (rebindInProgress) return;
        InputRebindService.ResetGroup(actions, currentGroup);
        RefreshBindingLabels();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;

        GameObject eventSystem = new GameObject("EventSystem");
        eventSystem.AddComponent<EventSystem>();
        eventSystem.AddComponent<InputSystemUIInputModule>();
    }

    private void BuildUI()
    {
        GameObject canvasObject = new GameObject("PauseCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasObject.AddComponent<GraphicRaycaster>();

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image dimmer = CreateImage(canvasObject.transform, "Dimmer", new Color(0f, 0f, 0f, 0.72f));
        StretchFull(dimmer.rectTransform);

        rootPanel = CreatePanel(canvasObject.transform, "RootPanel", new Vector2(460f, 420f));
        CreateLabel(rootPanel.transform, "暂停", 42f, TextAlignmentOptions.Center, new Vector2(0f, 150f), new Vector2(400f, 60f));
        CreateMenuButton(rootPanel.transform, "继续战斗", new Vector2(0f, 50f), Resume);
        CreateMenuButton(rootPanel.transform, "设置", new Vector2(0f, -20f), ShowSettings);
        CreateMenuButton(rootPanel.transform, "退出战斗", new Vector2(0f, -90f), RestartScene);

        settingsPanel = CreatePanel(canvasObject.transform, "SettingsPanel", new Vector2(640f, 720f));
        CreateLabel(settingsPanel.transform, "键位设置", 36f, TextAlignmentOptions.Center, new Vector2(0f, 310f), new Vector2(560f, 50f));

        keyboardTabImage = CreateMenuButton(settingsPanel.transform, "键盘鼠标", new Vector2(-140f, 250f),
            () => SwitchGroup(InputRebindService.KeyboardMouseGroup), new Vector2(240f, 44f));
        gamepadTabImage = CreateMenuButton(settingsPanel.transform, "手柄", new Vector2(140f, 250f),
            () => SwitchGroup(InputRebindService.GamepadGroup), new Vector2(240f, 44f));

        for (int i = 0; i < InputRebindService.RemappableActions.Length; i++)
        {
            float y = 180f - i * 58f;
            CreateRebindRow(settingsPanel.transform, i, y);
        }

        waitingHint = CreateLabel(settingsPanel.transform, string.Empty, 22f, TextAlignmentOptions.Center,
            new Vector2(0f, -200f), new Vector2(560f, 36f));
        waitingHint.color = AccentColor;

        CreateMenuButton(settingsPanel.transform, "恢复默认", new Vector2(-140f, -270f), ResetCurrentGroup, new Vector2(240f, 44f));
        CreateMenuButton(settingsPanel.transform, "返回", new Vector2(140f, -270f), ShowRoot, new Vector2(240f, 44f));
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

        Image buttonImage = CreateMenuButton(row.transform, "—", new Vector2(140f, 0f),
            () => OnRowClicked(rowIndex), new Vector2(260f, 44f));
        TextMeshProUGUI label = buttonImage.GetComponentInChildren<TextMeshProUGUI>();
        bindingLabels.Add(label);
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

    private Image CreateMenuButton(Transform parent, string text, Vector2 position, UnityEngine.Events.UnityAction onClick, Vector2? size = null)
    {
        Image image = CreateImage(parent, text, ButtonColor);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size ?? new Vector2(320f, 56f);
        rect.anchoredPosition = position;

        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = TabActive;
        colors.pressedColor = AccentColor;
        colors.selectedColor = TabActive;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        CreateLabel(image.transform, text, 26f, TextAlignmentOptions.Center, Vector2.zero, rect.sizeDelta);
        return image;
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
}
