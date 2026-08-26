# 暂停菜单音量设置 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法跟踪进度。

**目标：** 暂停「设置」拆成键位 / 声音两页；声音页两条滑条分别调音乐和音效，立刻生效并写入 PlayerPrefs。

**架构：** 静态类 `AudioVolumeSettings` 存两档 0–1。`AudioManager` 只改 `AudioSource.volume`。`BGMManager` 用 `用户音量 × fadeWeight`，DOTween 只 tween `fadeWeight`。暂停菜单加分类页和声音页，不上 AudioMixer。

**技术栈：** Unity 2022 LTS、uGUI Slider、PlayerPrefs、DOTween。本项目无自动化测试；每个任务用 Unity 编译 0 error + 手测。不要新建 Test Runner。不要擅自 git commit（仅当用户本会话明确要求时才提交）。

**规格：** `Docs/superpowers/specs/2026-08-26-audio-volume-settings-design.md`  
**本任务只改这些架构文档：** `Docs/architecture/05-input-lockon.md`、`Docs/architecture/05-input-lockon-test.md`、`Docs/architecture/06-presentation.md`、`Docs/architecture/06-presentation-test.md`

**不要改：** CombatEventBus、PlayOneShot 调用点、AudioMixer 资源、改键逻辑本身。

**现有 API（必须按此写，不要发明别名）：**

| 用途 | 名字 |
| --- | --- |
| 音效 | `AudioManager.audioSource.PlayOneShot` |
| BGM 淡入淡出 | `BGMManager.FadeVolume`（本计划改名为对 `fadeWeight` 的 tween） |
| 暂停页 | `PauseMenuController` 的 `ShowRoot` / `ShowSettings` / `HandleMenuKeys` |
| 存档范例 | `InputRebindService` + `PlayerPrefs` |

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Scripts/Audio/AudioVolumeSettings.cs` | 两档音量、PlayerPrefs、`OnChanged` |
| 修改 `Assets/Scripts/Audio/AudioManager.cs` | Source.volume = Sfx |
| 修改 `Assets/Scripts/Audio/BGMManager.cs` | `source.volume = Bgm * fadeWeight` |
| 修改 `Assets/Scripts/UI/PauseMenuController.cs` | 分类页 + 声音滑条 + B 返回多层 |
| 修改上述 4 份 `Docs/architecture/` | 结构和验收 |

不要做：Mixer、总音量、音效拖条预览音、声音页「恢复默认」。

---

### 任务 1：AudioVolumeSettings

**文件：**
- 创建：`Assets/Scripts/Audio/AudioVolumeSettings.cs`

- [x] **步骤 1：写入下列完整文件**

```csharp
using System;
using UnityEngine;

// 音乐 / 音效两档用户音量。表现层设置，不进战斗逻辑。
public static class AudioVolumeSettings
{
    public const string BgmKey = "audio.bgm";
    public const string SfxKey = "audio.sfx";
    public const float DefaultBgm = 0.8f;
    public const float DefaultSfx = 1f;

    public static event Action OnChanged;

    public static float Bgm { get; private set; } = DefaultBgm;
    public static float Sfx { get; private set; } = DefaultSfx;

    private static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        Bgm = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmKey, DefaultBgm));
        Sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxKey, DefaultSfx));
        loaded = true;
    }

    public static void SetBgm(float value)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(Bgm, value)) return;
        Bgm = value;
        PlayerPrefs.SetFloat(BgmKey, Bgm);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    public static void SetSfx(float value)
    {
        Load();
        value = Mathf.Clamp01(value);
        if (Mathf.Approximately(Sfx, value)) return;
        Sfx = value;
        PlayerPrefs.SetFloat(SfxKey, Sfx);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
```

- [ ] **步骤 2：Unity MCP `refresh_unity`（`compile: request`, `scope: scripts`）后 `read_console` types=error**

预期：0 条 error。`AudioVolumeSettings` 可被 `execute_code` 取到 `DefaultBgm == 0.8f`。

---

### 任务 2：接到两个 AudioSource

**文件：**
- 修改：`Assets/Scripts/Audio/AudioManager.cs`
- 修改：`Assets/Scripts/Audio/BGMManager.cs`

- [ ] **步骤 1：改 AudioManager**

`Awake` 末尾：

```csharp
AudioVolumeSettings.Load();
ApplySfxVolume();
```

`OnEnable` 增加：`AudioVolumeSettings.OnChanged += ApplySfxVolume;`  
`OnDisable` 增加：`AudioVolumeSettings.OnChanged -= ApplySfxVolume;`

新方法：

```csharp
private void ApplySfxVolume()
{
    if (audioSource == null) return;
    audioSource.volume = AudioVolumeSettings.Sfx;
}
```

不要改任何 `PlayOneShot` 调用。

- [ ] **步骤 2：改 BGMManager**

保留检视器 `volume` 字段以免场景序列化丢失，但运行时忽略它。把淡入淡出改成 tween `fadeWeight`：

在字段区增加：

```csharp
private float fadeWeight;
```

`Awake` 末尾：`AudioVolumeSettings.Load();`

`OnEnable` / `OnDisable` 增加：

```csharp
AudioVolumeSettings.OnChanged += ApplyOutputVolume;
// OnDisable:
AudioVolumeSettings.OnChanged -= ApplyOutputVolume;
```

`PlayBGM` 里所有 `FadeVolume(volume, …)` 和 `source.volume = 0f` 换成：

```csharp
public void PlayBGM(AudioClip clip)
{
    if (clip == null || clip == currentClip) return;

    fadeTween?.Kill();

    if (source.clip == null)
    {
        source.clip = clip;
        currentClip = clip;
        fadeWeight = 0f;
        ApplyOutputVolume();
        source.Play();
        fadeTween = FadeWeight(1f, fadeDuration);
        return;
    }

    fadeTween = FadeWeight(0f, fadeDuration * 0.5f)
        .OnComplete(() =>
        {
            source.clip = clip;
            currentClip = clip;
            source.Play();
            fadeTween = FadeWeight(1f, fadeDuration * 0.5f);
        });
}

public void StopBGM()
{
    fadeTween?.Kill();
    fadeTween = FadeWeight(0f, fadeDuration)
        .OnComplete(() =>
        {
            source.Stop();
            currentClip = null;
        });
}

private Tween FadeWeight(float target, float duration)
{
    return DOTween.To(() => fadeWeight, w =>
    {
        fadeWeight = w;
        ApplyOutputVolume();
    }, target, duration).SetEase(Ease.Linear);
}

private void ApplyOutputVolume()
{
    if (source == null) return;
    source.volume = AudioVolumeSettings.Bgm * fadeWeight;
}
```

删除旧的 `FadeVolume`。禁止再把 `AudioVolumeSettings.Bgm` 或检视器 `volume` 当作 tween 的绝对目标。

- [ ] **步骤 3：编译**

`refresh_unity` + `read_console` error = 0。Play 不进菜单时 BGM 仍应淡入（默认 0.8）。

---

### 任务 3：暂停菜单分类页 + 声音滑条

**文件：**
- 修改：`Assets/Scripts/UI/PauseMenuController.cs`

- [ ] **步骤 1：用页枚举替换 `settingsOpen`**

在类里：

```csharp
private enum PausePage { Root, Hub, Keybind, Audio }

private PausePage page = PausePage.Root;
```

删掉 `bool settingsOpen`。所有 `settingsOpen = …` 改成设 `page`。

- [ ] **步骤 2：新增面板 / 控件字段**

```csharp
private GameObject hubPanel;
private GameObject audioPanel;
private Button hubKeybindButton;
private Button hubAudioButton;
private Button hubBackButton;
private Button audioBackButton;
private Slider bgmSlider;
private Slider sfxSlider;
private Image bgmFill;
private Image sfxFill;
private TextMeshProUGUI bgmValueLabel;
private TextMeshProUGUI sfxValueLabel;
```

`settingsPanel` 仍是改键页。

- [ ] **步骤 3：改页面切换与 B 返回**

`HandleMenuKeys` 里暂停中、非 Start 的返回：

```csharp
if (page == PausePage.Keybind || page == PausePage.Audio)
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
```

`ShowRoot`：关掉 hub / settings / audio，打开 root，`page = Root`，选中继续战斗。

根页「设置」按钮改为 `ShowHub`（不要再直接 `ShowSettings`）。

```csharp
private void ShowHub()
{
    CancelRebind();
    page = PausePage.Hub;
    rootPanel.SetActive(false);
    hubPanel.SetActive(true);
    settingsPanel.SetActive(false);
    audioPanel.SetActive(false);
    SelectButton(hubKeybindButton);
}

private void ShowSettings()
{
    page = PausePage.Keybind;
    rootPanel.SetActive(false);
    hubPanel.SetActive(false);
    settingsPanel.SetActive(true);
    audioPanel.SetActive(false);
    RefreshBindingLabels();
    RefreshTabs();
    SelectButton(keyboardTabButton);
}

private void ShowAudio()
{
    page = PausePage.Audio;
    rootPanel.SetActive(false);
    hubPanel.SetActive(false);
    settingsPanel.SetActive(false);
    audioPanel.SetActive(true);
    AudioVolumeSettings.Load();
    bgmSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Bgm * 100f));
    sfxSlider.SetValueWithoutNotify(Mathf.Round(AudioVolumeSettings.Sfx * 100f));
    RefreshAudioLabels();
    SelectSlider(bgmSlider);
}

private void HideMenu()
{
    CancelRebind();
    page = PausePage.Root;
    rootPanel.SetActive(false);
    hubPanel.SetActive(false);
    settingsPanel.SetActive(false);
    audioPanel.SetActive(false);
    if (EventSystem.current != null)
        EventSystem.current.SetSelectedGameObject(null);
    pauseCanvas.SetActive(false);
}
```

`SelectSlider` 与 `SelectButton` 相同套路（先清再选，下一帧再选一次）。

- [ ] **步骤 4：BuildUI 里加分类页和声音页**

根页三个按钮不变。`openSettingsButton` 的 onClick 是 `ShowHub`。

在 `settingsPanel` 创建之后追加（分类页尺寸与根页类似 `460×420`，声音页 `560×420`）：

```csharp
hubPanel = CreatePanel(canvasObject.transform, "HubPanel", new Vector2(460f, 420f));
CreateLabel(hubPanel.transform, "设置", 42f, TextAlignmentOptions.Center,
    new Vector2(0f, 150f), new Vector2(400f, 60f));
hubKeybindButton = CreateMenuButton(hubPanel.transform, "键位设置", new Vector2(0f, 50f), ShowSettings);
hubAudioButton = CreateMenuButton(hubPanel.transform, "声音设置", new Vector2(0f, -20f), ShowAudio);
hubBackButton = CreateMenuButton(hubPanel.transform, "返回", new Vector2(0f, -90f), ShowRoot);

audioPanel = CreatePanel(canvasObject.transform, "AudioPanel", new Vector2(560f, 420f));
CreateLabel(audioPanel.transform, "声音设置", 36f, TextAlignmentOptions.Center,
    new Vector2(0f, 150f), new Vector2(500f, 50f));
CreateSliderRow(audioPanel.transform, "音乐", new Vector2(0f, 40f), true);
CreateSliderRow(audioPanel.transform, "音效", new Vector2(0f, -40f), false);
audioBackButton = CreateMenuButton(audioPanel.transform, "返回",
    new Vector2(0f, -130f), ShowHub, new Vector2(320f, 56f));
```

`CreateSliderRow` 完整实现（`isBgm == true` 赋给 `bgmSlider` 等，否则赋给 sfx）：

```csharp
private void CreateSliderRow(Transform parent, string title, Vector2 position, bool isBgm)
{
    GameObject row = new GameObject(title + "Row", typeof(RectTransform));
    row.transform.SetParent(parent, false);
    RectTransform rowRect = row.GetComponent<RectTransform>();
    rowRect.anchorMin = rowRect.anchorMax = rowRect.pivot = new Vector2(0.5f, 0.5f);
    rowRect.sizeDelta = new Vector2(500f, 56f);
    rowRect.anchoredPosition = position;

    CreateLabel(row.transform, title, 26f, TextAlignmentOptions.Left,
        new Vector2(-190f, 0f), new Vector2(100f, 44f));

    Image track = CreateImage(row.transform, "Track", ButtonColor);
    RectTransform trackRect = track.rectTransform;
    trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0.5f, 0.5f);
    trackRect.sizeDelta = new Vector2(260f, 18f);
    trackRect.anchoredPosition = new Vector2(20f, 0f);

    GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
    fillArea.transform.SetParent(track.transform, false);
    RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
    StretchFull(fillAreaRect);

    Image fill = CreateImage(fillArea.transform, "Fill", AccentColor);
    RectTransform fillRect = fill.rectTransform;
    StretchFull(fillRect);

    Slider slider = track.gameObject.AddComponent<Slider>();
    slider.transition = Selectable.Transition.None;
    slider.minValue = 0f;
    slider.maxValue = 100f;
    slider.wholeNumbers = true;
    slider.fillRect = fillRect;
    slider.targetGraphic = track;
    slider.direction = Slider.Direction.LeftToRight;
    slider.navigation = new Navigation { mode = Navigation.Mode.Explicit };

    TextMeshProUGUI valueLabel = CreateLabel(row.transform, "80", 26f, TextAlignmentOptions.Right,
        new Vector2(220f, 0f), new Vector2(70f, 44f));

    if (isBgm)
    {
        bgmSlider = slider;
        bgmFill = fill;
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
        sfxFill = fill;
        sfxValueLabel = valueLabel;
        slider.onValueChanged.AddListener(v =>
        {
            AudioVolumeSettings.SetSfx(v / 100f);
            RefreshAudioLabels();
        });
    }
}

private void RefreshAudioLabels()
{
    if (bgmValueLabel != null)
        bgmValueLabel.text = Mathf.RoundToInt(bgmSlider.value).ToString();
    if (sfxValueLabel != null)
        sfxValueLabel.text = Mathf.RoundToInt(sfxSlider.value).ToString();
}
```

滑条 **左右不设 `selectOnLeft` / `selectOnRight`**（保持 null）。Unity `Slider.OnMove` 在左右无邻居时改值，有邻居会把焦点带走。

- [ ] **步骤 5：导航与选中色**

`WireNavigation` 在现有根页三条之后增加：

```csharp
SetNav(hubKeybindButton, null, hubAudioButton, null, null);
SetNav(hubAudioButton, hubKeybindButton, hubBackButton, null, null);
SetNav(hubBackButton, hubAudioButton, null, null, null);

SetSliderNav(bgmSlider, null, sfxSlider);
SetSliderNav(sfxSlider, bgmSlider, audioBackButton);
SetNav(audioBackButton, null, null, null, null);
// audioBack 的 selectOnUp = sfxSlider 需要单独写，因为 SetNav 只接 Button：
Navigation audioBackNav = audioBackButton.navigation;
audioBackNav.mode = Navigation.Mode.Explicit;
audioBackNav.selectOnUp = sfxSlider;
audioBackButton.navigation = audioBackNav;
```

```csharp
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
```

`RefreshSelectionVisual` 增加 hub 三个按钮、`audioBackButton`，以及：

```csharp
ApplySliderVisual(bgmSlider, bgmFill, bgmValueLabel);
ApplySliderVisual(sfxSlider, sfxFill, sfxValueLabel);
```

```csharp
private void ApplySliderVisual(Slider slider, Image fill, TextMeshProUGUI valueLabel)
{
    if (slider == null) return;
    bool selected = EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == slider.gameObject;
    Image track = slider.GetComponent<Image>();
    if (track != null) track.color = selected ? SelectedFill : ButtonColor;
    if (fill != null) fill.color = selected ? SelectedTextColor : AccentColor;
    if (valueLabel != null) valueLabel.color = selected ? SelectedTextColor : TextColor;
}
```

选中时轨是亮金、填充用深色才能在金底上看出进度；未选中轨深灰、填充亮金。

`Start` 里 `BuildUI` 之后所有新面板默认 `SetActive(false)`（`HideMenu` 会关，但构建当下要先关 hub/audio）。

- [ ] **步骤 6：编译**

`refresh_unity` + error = 0。不要进 Play 替用户验收手柄，交给任务 4 的验收表。

---

### 任务 4：架构文档

**文件：**
- 修改：`Docs/architecture/05-input-lockon.md`「三、暂停与自定义键位」
- 修改：`Docs/architecture/05-input-lockon-test.md` 暂停表
- 修改：`Docs/architecture/06-presentation.md` 文末
- 修改：`Docs/architecture/06-presentation-test.md` M15

- [ ] **步骤 1：05 暂停段改成**

Esc / 手柄 Start 打开暂停。点「设置」先进入分类页（键位设置 / 声音设置 / 返回），不再直接改键。Esc / B 返回上一级：声音或改键 → 分类 → 根页 → 继续战斗。Start 任意页直接关暂停。声音页两条滑条（音乐 / 音效，0–100），左右调值，立刻作用到 `BGMManager` / `AudioManager` 的 Source 音量，存 `PlayerPrefs`（`audio.bgm` / `audio.sfx`）。不上 AudioMixer。

- [ ] **步骤 2：05-test 表改/增**

| # | 操作 | 预期 |
|---|------|------|
| P2b | 分类页按 B | 回到根页，不关暂停 |
| P2e | 声音页按 B | 回到分类页 |
| P3 | 点「设置」 | 分类页：键位设置 / 声音设置 / 返回 |
| P3b | 点「键位设置」 | 现有改键页，默认键盘鼠标 Tab |
| P11 | 点「声音设置」，拖音乐条 | 旁标 0–100；BGM 立刻变响或变轻 |
| P12 | 音效拉到 0，继续后弹反 | 无音效；BGM 仍按音乐条 |
| P13 | 退出 Play 再进声音页 | 两条滑条仍是上次的值 |
| P14 | 手柄选中音乐滑条左右 | 音量变，焦点不跳到音效条 |

原 P3「点设置进入键位页」必须改掉，否则和分类页矛盾。

- [ ] **步骤 3：06-presentation.md 在「震屏」后追加**

### 用户音量（暂停菜单）

- `AudioVolumeSettings`：音乐默认 0.8，音效默认 1.0，`PlayerPrefs` 键 `audio.bgm` / `audio.sfx`。
- 音效：`AudioManager` 的 `AudioSource.volume`。
- 音乐：`BGMManager` 实际音量 = 用户音乐音量 × 淡入淡出 `fadeWeight`。不要用 Mixer。

- [ ] **步骤 4：06-presentation-test.md M15 增加**

| # | 操作 | 预期 |
|---|------|------|
| 23 | 暂停声音页把音乐拉到 0 | 战斗 BGM 静音 |
| 24 | 音效拉到 0 后弹反 / 受击 | 无音效 |
| 25 | 两条都不是 0 | 弹反叮、BGM 都在，响度跟滑条走 |

---

## 自检

| 规格项 | 任务 |
|--------|------|
| 分类页键位 / 声音 | 3 |
| 两条独立滑条 0–100 | 3 |
| B 多层返回 | 3 |
| PlayerPrefs | 1 |
| 音效走 Source.volume | 2 |
| BGM = 用户音量 × fadeWeight | 2 |
| 无 Mixer、无预览音、无声音恢复默认 | 全计划未包含 |
| 架构文档 | 4 |

无 TODO / 待定。类型名统一为 `AudioVolumeSettings.Bgm` / `Sfx` / `SetBgm` / `SetSfx` / `fadeWeight` / `PausePage`。
