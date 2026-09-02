using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using ARPG.Audio;
using ARPG.Mgr;
using ARPG.Player;
using static ARPG.UI.PauseMenuStyle;

namespace ARPG.UI
{

    // 暂停菜单：Esc / Start 打开 SettingPanel（挂在 CombatCanvas 上，场景里改字体）。
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
        private Button exitGameButton;
        private Button rootCloseButton;
        private Button hubKeybindButton;
        private Button hubBackButton;
        private Button hubCloseButton;
        private Button settingsCloseButton;
        private Image hubCloseIcon;
        private Button keyboardTabButton;
        private Button gamepadTabButton;
        private Button resetButton;
        private Button backButton;
        private Slider bgmSlider;
        private Slider sfxSlider;
        private Button infiniteHealthToggle;
        private Button oneHitPostureBreakToggle;
        private TextMeshProUGUI infiniteHealthValueLabel;
        private TextMeshProUGUI oneHitPostureBreakValueLabel;
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
        private readonly PauseMenuBuilder builder = new PauseMenuBuilder();

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

            CursorController.BindPlayerInput(playerInput);
            InputRebindService.Load(actions);
            AudioVolumeSettings.Load();
            GameplaySettings.Load();
            playerMap = actions.FindActionMap("Player");
            EnsureEventSystem();
            TmpChineseFont.EnsureReady();
            pauseCanvas = EnsureSettingPanelRoot();
            if (!TryBindExisting())
                BuildUIInto(pauseCanvas.transform);
            else
                WireExistingListeners();
            EnsurePauseCloseButtons();
            EnsureGameplayToggleButtons();
            EnsureExitGameButton();
            WireNavigation();
            HideMenu();
            CursorController.Refresh();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                CursorController.Refresh();
        }

        private void OnDestroy()
        {
            CancelRebind();
            SetGameplayInput(true);
            if (GamePause.IsPaused) GamePause.SetPaused(false);
            CursorController.Refresh();
            builder.DestroyGenerated();
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
            Transform dimmer = pauseCanvas.transform.Find("Dimmer");
            if (dimmer != null)
                dimmer.gameObject.SetActive(true);
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

        private void QuitGame()
        {
            CancelRebind();
            CombatPromptStyle.QuitGame();
        }

        private void ShowRoot()
        {
            CancelRebind();
            page = PausePage.Root;
            ResetPanelPose(rootPanel);
            ResetPanelPose(hubPanel);
            ResetPanelPose(settingsPanel);
            rootPanel.SetActive(true);
            hubPanel.SetActive(false);
            settingsPanel.SetActive(false);
            GameplaySettings.Load();
            RefreshGameplayToggles();
            SelectButton(resumeButton);
        }

        private void ShowHub()
        {
            CancelRebind();
            page = PausePage.Hub;
            ResetPanelPose(rootPanel);
            ResetPanelPose(hubPanel);
            ResetPanelPose(settingsPanel);
            rootPanel.SetActive(false);
            hubPanel.SetActive(true);
            settingsPanel.SetActive(false);
            AudioVolumeSettings.Load();
            GameplaySettings.Load();
            bgmSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Bgm * 100f));
            sfxSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Sfx * 100f));
            RefreshAudioLabels();
            SelectSelectable(bgmSlider);
        }

        private void ShowSettings()
        {
            page = PausePage.Keybind;
            ResetPanelPose(rootPanel);
            ResetPanelPose(hubPanel);
            ResetPanelPose(settingsPanel);
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
            ApplyToggleVisual(infiniteHealthToggle, infiniteHealthValueLabel, selected);
            ApplyToggleVisual(oneHitPostureBreakToggle, oneHitPostureBreakValueLabel, selected);
            ApplyButtonVisual(openSettingsButton, false, IsUiSelected(openSettingsButton, selected));
            ApplyButtonVisual(quitButton, false, IsUiSelected(quitButton, selected));
            ApplyButtonVisual(exitGameButton, false, IsUiSelected(exitGameButton, selected));
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

        private void RefreshGameplayToggles()
        {
            if (infiniteHealthValueLabel != null)
                infiniteHealthValueLabel.text = GameplaySettings.InfiniteHealth ? "开" : "关";
            if (oneHitPostureBreakValueLabel != null)
                oneHitPostureBreakValueLabel.text = GameplaySettings.OneHitPostureBreak ? "开" : "关";
        }

        private void ToggleInfiniteHealth()
        {
            GameplaySettings.SetInfiniteHealth(!GameplaySettings.InfiniteHealth);
            RefreshGameplayToggles();
        }

        private void ToggleOneHitPostureBreak()
        {
            GameplaySettings.SetOneHitPostureBreak(!GameplaySettings.OneHitPostureBreak);
            RefreshGameplayToggles();
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

        // 设置页做在 CombatCanvas/SettingPanel 上，场景里能改字体。没有子物体才现拼。
        private GameObject EnsureSettingPanelRoot()
        {
            Transform canvas = FindCombatCanvas();
            Transform existing = canvas != null ? canvas.Find("SettingPanel") : null;
            if (existing != null)
                return existing.gameObject;

            Transform parent = canvas != null ? canvas : transform;
            GameObject panel = new GameObject("SettingPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            builder.StretchFull(panel.GetComponent<RectTransform>());
            panel.transform.SetAsLastSibling();
            return panel;
        }

        private static Transform FindCombatCanvas()
        {
            GameObject named = GameObject.Find("CombatCanvas");
            if (named != null)
                return named.transform;

            Canvas[] canvases = Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < canvases.Length; i++)
            {
                if (canvases[i] != null && canvases[i].name == "CombatCanvas")
                    return canvases[i].transform;
            }
            return null;
        }

        private bool TryBindExisting()
        {
            if (pauseCanvas == null) return false;
            Transform root = pauseCanvas.transform;
            Transform rootTf = root.Find("RootPanel");
            Transform hubTf = root.Find("HubPanel");
            Transform settingsTf = root.Find("SettingsPanel");
            if (rootTf == null || hubTf == null || settingsTf == null) return false;

            rootPanel = rootTf.gameObject;
            hubPanel = hubTf.gameObject;
            settingsPanel = settingsTf.gameObject;

            resumeButton = FindButton(rootTf, "继续战斗");
            openSettingsButton = FindButton(rootTf, "设置");
            quitButton = FindButton(rootTf, "重新开始") ?? FindButton(rootTf, "退出战斗");
            EnsureButtonLabel(quitButton, "重新开始");
            exitGameButton = FindButton(rootTf, "退出游戏");
            rootCloseButton = FindButton(rootTf, "Close");
            BindGameplayToggleButtons(rootTf);
            hubKeybindButton = FindButton(hubTf, "键位设置");
            hubBackButton = FindButton(hubTf, "返回");
            hubCloseButton = FindButton(hubTf, "Close");
            if (hubCloseButton != null)
            {
                Transform icon = hubCloseButton.transform.Find("Icon");
                if (icon != null) hubCloseIcon = icon.GetComponent<Image>();
            }

            keyboardTabButton = FindButton(settingsTf, "键盘鼠标");
            gamepadTabButton = FindButton(settingsTf, "手柄");
            resetButton = FindButton(settingsTf, "恢复默认");
            backButton = FindButton(settingsTf, "返回");
            settingsCloseButton = FindButton(settingsTf, "Close");
            waitingHint = FindTmp(settingsTf, "WaitingHint");

            BindSliderRow(hubTf, "音乐音量Row", true);
            BindSliderRow(hubTf, "音效音量Row", false);

            bindingLabels.Clear();
            rebindRowButtons.Clear();
            for (int i = 0; i < InputRebindService.RemappableActions.Length; i++)
            {
                Transform row = settingsTf.Find("Row_" + InputRebindService.RemappableActions[i]);
                if (row == null) return false;
                Button rowButton = row.GetComponentInChildren<Button>();
                if (rowButton == null) return false;
                rebindRowButtons.Add(rowButton);
                bindingLabels.Add(rowButton.GetComponentInChildren<TextMeshProUGUI>());
            }

            return resumeButton != null && bgmSlider != null && sfxSlider != null;
        }

        private void BindGameplayToggleButtons(Transform panel)
        {
            if (panel == null) return;
            infiniteHealthToggle = FindButton(panel, "无限生命");
            oneHitPostureBreakToggle = FindButton(panel, "一击破防");
            infiniteHealthValueLabel = FindToggleValueLabel(infiniteHealthToggle);
            oneHitPostureBreakValueLabel = FindToggleValueLabel(oneHitPostureBreakToggle);
        }

        private static TextMeshProUGUI FindToggleValueLabel(Button toggle)
        {
            if (toggle == null) return null;
            Transform value = toggle.transform.Find("Value");
            return value != null ? value.GetComponent<TextMeshProUGUI>() : null;
        }

        private void BindSliderRow(Transform hub, string rowName, bool isBgm)
        {
            Transform row = hub.Find(rowName);
            if (row == null) return;
            Slider slider = row.Find("Slider") != null ? row.Find("Slider").GetComponent<Slider>() : null;
            Image handle = row.Find("Slider/Handle Slide Area/Handle/HandleGraphic") != null
                ? row.Find("Slider/Handle Slide Area/Handle/HandleGraphic").GetComponent<Image>()
                : null;
            Image fill = row.Find("Slider/Track/Fill Area/Fill") != null
                ? row.Find("Slider/Track/Fill Area/Fill").GetComponent<Image>()
                : null;
            Image tick = row.Find("Tick") != null ? row.Find("Tick").GetComponent<Image>() : null;
            TextMeshProUGUI title = FindTmp(row, rowName.Replace("Row", string.Empty));
            TextMeshProUGUI value = FindTmp(row, "Value");
            if (isBgm)
            {
                bgmSlider = slider;
                bgmHandle = handle;
                bgmFill = fill;
                bgmTick = tick;
                bgmTitleLabel = title;
                bgmValueLabel = value;
            }
            else
            {
                sfxSlider = slider;
                sfxHandle = handle;
                sfxFill = fill;
                sfxTick = tick;
                sfxTitleLabel = title;
                sfxValueLabel = value;
            }
        }

        private void WireExistingListeners()
        {
            BindClick(resumeButton, Resume);
            BindClick(openSettingsButton, ShowHub);
            BindClick(quitButton, RestartScene);
            BindClick(exitGameButton, QuitGame);
            BindClick(rootCloseButton, Resume);
            BindClick(hubKeybindButton, ShowSettings);
            BindClick(hubBackButton, ShowRoot);
            BindClick(hubCloseButton, ShowRoot);
            BindClick(settingsCloseButton, ShowHub);
            BindClick(keyboardTabButton, () => SwitchGroup(InputRebindService.KeyboardMouseGroup));
            BindClick(gamepadTabButton, () => SwitchGroup(InputRebindService.GamepadGroup));
            BindClick(resetButton, ResetCurrentGroup);
            BindClick(backButton, ShowHub);

            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(v =>
                {
                    AudioVolumeSettings.SetBgm(v / 100f);
                    RefreshAudioLabels();
                });
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(v =>
                {
                    AudioVolumeSettings.SetSfx(v / 100f);
                    RefreshAudioLabels();
                });
            }

            BindClick(infiniteHealthToggle, ToggleInfiniteHealth);
            BindClick(oneHitPostureBreakToggle, ToggleOneHitPostureBreak);
            RefreshGameplayToggles();

            for (int i = 0; i < rebindRowButtons.Count; i++)
            {
                int index = i;
                BindClick(rebindRowButtons[i], () => OnRowClicked(index));
            }
        }

        private void EnsurePauseCloseButtons()
        {
            EnsureKitSprites();
            builder.EnsureGeneratedSprites();
            if (rootPanel != null && rootCloseButton == null)
                rootCloseButton = builder.CreateCloseButton(rootPanel.transform, closeIconSprite, Resume);
            if (hubPanel != null && hubCloseButton == null)
            {
                hubCloseButton = builder.CreateCloseButton(hubPanel.transform, closeIconSprite, ShowRoot);
                Transform icon = hubCloseButton.transform.Find("Icon");
                if (icon != null) hubCloseIcon = icon.GetComponent<Image>();
            }

            if (settingsPanel != null && settingsCloseButton == null)
                settingsCloseButton = builder.CreateCloseButton(settingsPanel.transform, closeIconSprite, ShowHub);

            BindClick(rootCloseButton, Resume);
            BindClick(hubCloseButton, ShowRoot);
            BindClick(settingsCloseButton, ShowHub);
        }

        private void EnsureGameplayToggleButtons()
        {
            if (rootPanel == null) return;

            Transform rootTf = rootPanel.transform;
            Transform hubTf = hubPanel != null ? hubPanel.transform : null;

            RemoveLegacyGameplayToggle(hubTf, "无限生命Row");
            RemoveLegacyGameplayToggle(hubTf, "一击破防Row");
            RemoveLegacyGameplayToggle(hubTf, "无限生命");
            RemoveLegacyGameplayToggle(hubTf, "一击破防");
            RemoveLegacyGameplayToggle(rootTf, "无限生命Row");
            RemoveLegacyGameplayToggle(rootTf, "一击破防Row");

            if (FindButton(rootTf, "无限生命") == null)
                builder.CreateGameplayToggleButton(rootTf, "无限生命", new Vector2(0f, 74f), ToggleInfiniteHealth, out infiniteHealthValueLabel);
            if (FindButton(rootTf, "一击破防") == null)
                builder.CreateGameplayToggleButton(rootTf, "一击破防", new Vector2(0f, 12f), ToggleOneHitPostureBreak, out oneHitPostureBreakValueLabel);

            BindGameplayToggleButtons(rootTf);
            BindClick(infiniteHealthToggle, ToggleInfiniteHealth);
            BindClick(oneHitPostureBreakToggle, ToggleOneHitPostureBreak);
            RefreshGameplayToggles();
        }

        private void EnsureExitGameButton()
        {
            if (rootPanel == null) return;

            Transform rootTf = rootPanel.transform;
            exitGameButton = FindButton(rootTf, "退出游戏");
            if (exitGameButton == null)
                exitGameButton = builder.CreateMenuButton(rootTf, "退出游戏", new Vector2(0f, -180f), QuitGame);
            BindClick(exitGameButton, QuitGame);
        }

        private static void RemoveLegacyGameplayToggle(Transform parent, string name)
        {
            if (parent == null) return;
            Transform node = parent.Find(name);
            if (node == null) return;
    #if UNITY_EDITOR
            if (!Application.isPlaying)
                Object.DestroyImmediate(node.gameObject);
            else
    #endif
                Object.Destroy(node.gameObject);
        }

        public void EditorPreviewAllPages()
        {
            pauseCanvas = EnsureSettingPanelRoot();
            if (!TryBindExisting())
                BuildUIInto(pauseCanvas.transform);
            EnsurePauseCloseButtons();
            EnsureGameplayToggleButtons();
            EnsureExitGameButton();
            pauseCanvas.SetActive(true);
            Transform dimmer = pauseCanvas.transform.Find("Dimmer");
            if (dimmer != null)
                dimmer.gameObject.SetActive(false);
            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
                SetPanelPose(rootPanel, new Vector2(-620f, 200f));
            }

            if (hubPanel != null)
            {
                hubPanel.SetActive(true);
                SetPanelPose(hubPanel, new Vector2(0f, 200f));
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(true);
                SetPanelPose(settingsPanel, new Vector2(680f, 40f));
            }
        }

        // 编辑器预览：Esc 第一级暂停页（含练习开关）。
        public void EditorPreviewPauseRoot()
        {
            pauseCanvas = EnsureSettingPanelRoot();
            if (!TryBindExisting())
                BuildUIInto(pauseCanvas.transform);
            EnsurePauseCloseButtons();
            EnsureGameplayToggleButtons();
            EnsureExitGameButton();
            GameplaySettings.Load();
            RefreshGameplayToggles();
            pauseCanvas.SetActive(true);
            Transform dimmer = pauseCanvas.transform.Find("Dimmer");
            if (dimmer != null)
                dimmer.gameObject.SetActive(true);
            if (rootPanel != null)
            {
                rootPanel.SetActive(true);
                SetPanelPose(rootPanel, Vector2.zero);
            }

            if (hubPanel != null)
            {
                hubPanel.SetActive(false);
                SetPanelPose(hubPanel, Vector2.zero);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
                SetPanelPose(settingsPanel, Vector2.zero);
            }
        }

        public void EditorRebuildSettingPanel()
        {
            pauseCanvas = EnsureSettingPanelRoot();
            Transform root = pauseCanvas.transform;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
    #if UNITY_EDITOR
                if (!Application.isPlaying)
                    Object.DestroyImmediate(child.gameObject);
                else
    #endif
                    Object.Destroy(child.gameObject);
            }

            BuildUIInto(root);
            HideMenu();
        }

        private void BuildUIInto(Transform canvasRoot)
        {
            EnsureKitSprites();
            builder.EnsureGeneratedSprites();
            pauseCanvas = canvasRoot.gameObject;

            Image dimmer = builder.CreateImage(canvasRoot, "Dimmer", DimmerColor);
            dimmer.raycastTarget = false;
            builder.StretchFull(dimmer.rectTransform);

            rootPanel = builder.CreatePanel(canvasRoot, "RootPanel", new Vector2(460f, 600f));
            builder.CreateLabel(rootPanel.transform, "暂停", 42f, TextAlignmentOptions.Center, new Vector2(0f, 250f), new Vector2(400f, 60f));
            resumeButton = builder.CreateMenuButton(rootPanel.transform, "继续战斗", new Vector2(0f, 170f), Resume);
            infiniteHealthToggle = builder.CreateGameplayToggleButton(
                rootPanel.transform, "无限生命", new Vector2(0f, 104f), ToggleInfiniteHealth, out infiniteHealthValueLabel);
            oneHitPostureBreakToggle = builder.CreateGameplayToggleButton(
                rootPanel.transform, "一击破防", new Vector2(0f, 42f), ToggleOneHitPostureBreak, out oneHitPostureBreakValueLabel);
            openSettingsButton = builder.CreateMenuButton(rootPanel.transform, "设置", new Vector2(0f, -22f), ShowHub);
            quitButton = builder.CreateMenuButton(rootPanel.transform, "重新开始", new Vector2(0f, -86f), RestartScene);
            exitGameButton = builder.CreateMenuButton(rootPanel.transform, "退出游戏", new Vector2(0f, -150f), QuitGame);
            rootCloseButton = builder.CreateCloseButton(rootPanel.transform, closeIconSprite, Resume);

            hubPanel = builder.CreatePanel(canvasRoot, "HubPanel", new Vector2(560f, 500f));
            builder.CreateLabel(hubPanel.transform, "设置", 42f, TextAlignmentOptions.Center,
                new Vector2(0f, 190f), new Vector2(500f, 60f));
            hubCloseButton = builder.CreateCloseButton(hubPanel.transform, closeIconSprite, ShowRoot);
            hubCloseIcon = hubCloseButton.transform.Find("Icon").GetComponent<Image>();
            CreateSliderRow(hubPanel.transform, "音乐音量", new Vector2(0f, 100f), true);
            CreateSliderRow(hubPanel.transform, "音效音量", new Vector2(0f, 36f), false);
            hubKeybindButton = builder.CreateMenuButton(hubPanel.transform, "键位设置", new Vector2(0f, -50f), ShowSettings);
            hubBackButton = builder.CreateMenuButton(hubPanel.transform, "返回", new Vector2(0f, -118f), ShowRoot);

            settingsPanel = builder.CreatePanel(canvasRoot, "SettingsPanel", new Vector2(640f, 720f));
            builder.CreateLabel(settingsPanel.transform, "键位设置", 36f, TextAlignmentOptions.Center, new Vector2(0f, 310f), new Vector2(560f, 50f));
            settingsCloseButton = builder.CreateCloseButton(settingsPanel.transform, closeIconSprite, ShowHub);

            keyboardTabButton = builder.CreateMenuButton(settingsPanel.transform, "键盘鼠标", new Vector2(-140f, 250f),
                () => SwitchGroup(InputRebindService.KeyboardMouseGroup), new Vector2(240f, 44f));
            gamepadTabButton = builder.CreateMenuButton(settingsPanel.transform, "手柄", new Vector2(140f, 250f),
                () => SwitchGroup(InputRebindService.GamepadGroup), new Vector2(240f, 44f));

            bindingLabels.Clear();
            rebindRowButtons.Clear();
            for (int i = 0; i < InputRebindService.RemappableActions.Length; i++)
            {
                float y = 180f - i * 58f;
                CreateRebindRow(settingsPanel.transform, i, y);
            }

            waitingHint = builder.CreateLabel(settingsPanel.transform, string.Empty, 22f, TextAlignmentOptions.Center,
                new Vector2(0f, -200f), new Vector2(560f, 36f), "WaitingHint");
            waitingHint.color = AccentColor;

            resetButton = builder.CreateMenuButton(settingsPanel.transform, "恢复默认", new Vector2(-140f, -270f), ResetCurrentGroup, new Vector2(240f, 44f));
            backButton = builder.CreateMenuButton(settingsPanel.transform, "返回", new Vector2(140f, -270f), ShowHub, new Vector2(240f, 44f));
        }

        private void CreateRebindRow(Transform parent, int rowIndex, float y)
        {
            GameObject row = new GameObject($"Row_{InputRebindService.RemappableActions[rowIndex]}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rect = row.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(560f, 50f);
            rect.anchoredPosition = new Vector2(0f, y);

            builder.CreateLabel(row.transform, InputRebindService.RemappableLabels[rowIndex], 26f, TextAlignmentOptions.Left,
                new Vector2(-150f, 0f), new Vector2(200f, 44f));

            Button rowButton = builder.CreateMenuButton(row.transform, "—", new Vector2(140f, 0f),
                () => OnRowClicked(rowIndex), new Vector2(260f, 44f));
            TextMeshProUGUI label = rowButton.GetComponentInChildren<TextMeshProUGUI>();
            bindingLabels.Add(label);
            rebindRowButtons.Add(rowButton);
        }

        private void CreateSliderRow(Transform parent, string title, Vector2 position, bool isBgm)
        {
            EnsureKitSprites();
            builder.EnsureGeneratedSprites();

            GameObject row = new GameObject(title + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(500f, 56f);
            rowRect.anchoredPosition = position;

            Image tick = builder.CreateImage(row.transform, "Tick", AccentColor);
            RectTransform tickRect = tick.rectTransform;
            tickRect.anchorMin = tickRect.anchorMax = tickRect.pivot = new Vector2(0f, 0.5f);
            tickRect.sizeDelta = new Vector2(3f, 22f);
            tickRect.anchoredPosition = new Vector2(4f, 0f);
            tick.raycastTarget = false;
            tick.enabled = false;

            TextMeshProUGUI titleLabel = builder.CreateLabel(row.transform, title, 24f, TextAlignmentOptions.Left,
                new Vector2(-175f, 0f), new Vector2(130f, 36f));
            titleLabel.color = SliderLabelIdle;

            TextMeshProUGUI valueLabel = builder.CreateLabel(row.transform, "80%", 24f, TextAlignmentOptions.Right,
                new Vector2(-78f, 0f), new Vector2(60f, 36f), "Value");
            valueLabel.color = SliderLabelIdle;

            Image hit = builder.CreateImage(row.transform, "Slider", new Color(1f, 1f, 1f, 0f));
            RectTransform hitRect = hit.rectTransform;
            hitRect.anchorMin = hitRect.anchorMax = hitRect.pivot = new Vector2(0.5f, 0.5f);
            hitRect.sizeDelta = new Vector2(236f, 40f);
            hitRect.anchoredPosition = new Vector2(138f, 0f);

            Image track = builder.CreateImage(hit.transform, "Track", TrackTint);
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

            Image fill = builder.CreateImage(fillArea.transform, "Fill", FillIdle);
            fill.sprite = builder.FillBarSprite;
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

            Image handle = builder.CreateImage(handleRoot.transform, "HandleGraphic", HandleIdle);
            handle.sprite = builder.DiamondSprite;
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

            Image divider = builder.CreateImage(row.transform, "Divider", DividerColor);
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

        // 三个套件贴图是 [SerializeField]：允许在 Inspector 里预先拖好，没拖才回退到 Kit 路径。
        // 程序生成的那两张（填充条、菱形手柄）归 PauseMenuBuilder 管，这里不碰。
        private void EnsureKitSprites()
        {
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

        private void WireNavigation()
        {
            SetNav(resumeButton, null, infiniteHealthToggle, null, null);
            Navigation infiniteNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = resumeButton,
                selectOnDown = oneHitPostureBreakToggle != null ? oneHitPostureBreakToggle : openSettingsButton
            };
            if (infiniteHealthToggle != null)
                infiniteHealthToggle.navigation = infiniteNav;
            Navigation oneHitNav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = infiniteHealthToggle,
                selectOnDown = openSettingsButton
            };
            if (oneHitPostureBreakToggle != null)
                oneHitPostureBreakToggle.navigation = oneHitNav;
            SetNav(openSettingsButton, oneHitPostureBreakToggle != null ? oneHitPostureBreakToggle : resumeButton, quitButton, null, null);
            SetNav(quitButton, openSettingsButton, exitGameButton, null, null);
            SetNav(exitGameButton, quitButton, null, null, null);

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

}
