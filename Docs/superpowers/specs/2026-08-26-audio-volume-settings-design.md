# 暂停菜单音量设置 设计

日期：2026-08-26  
状态：已确认，按此实现

关系：表现层设置。不改战斗事件、不改 Mixer、不改改键逻辑。暂停菜单结构在 `05-input-lockon.md`；音量作用在 `06-presentation.md`。

## 目标

暂停「设置」里能分别调节 **音乐** 和 **音效** 大小。两条互不影响。拖条立刻生效，下次进游戏仍是上次的值。

## 不做

- 不上 `AudioMixer`（场景里只有两个 `AudioSource`）
- 不加总音量、不加语音轨
- 拖音效条不播预览音
- 不改 `CombatEventBus`，不改 `PlayOneShot` 调用点
- 「恢复默认」只属于改键页，声音页不做恢复按钮（滑到最右即最大）

## 菜单结构

根页不变。点「设置」先进入分类页，不再直接进改键。

```
暂停根页
  继续战斗
  设置 → 分类页
           键位设置 → 现有改键页（键盘 / 手柄 Tab）
           声音设置 → 音乐滑条、音效滑条、返回
           返回
  退出战斗
```

返回规则与现有一致：

- **B / Esc**：声音或改键 → 分类；分类 → 根页；根页 → 继续战斗
- **Start**：任意页直接关暂停
- 改键等待中：Esc / B / Start 仍只取消本次改键

声音页导航（手柄 / 键盘）：

1. 音乐滑条（默认选中）
2. 音效滑条
3. 返回

选中滑条后 **左右** 调值（`Slider` 作为 `Selectable`，左右不跳到别的控件）。上下在三条之间移动。选中态沿用暂停菜单：亮金 + 深色字；滑条填充条用亮金，未选中为深灰。

旁标显示整数 **0–100**（内部值 `0–1`，四舍五入到整数再显示）。`0` 为静音。

拖条立刻写音量。暂停不暂停音频（`timeScale = 0` 不影响 `AudioSource`），拖音乐条能当场听到 BGM 变响或变轻。

## 数据与作用

新建 `Assets/Scripts/Audio/AudioVolumeSettings.cs`（静态类，对标 `InputRebindService`）。

| 通道 | 默认 | PlayerPrefs 键 | 作用对象 |
|------|------|----------------|----------|
| 音乐 | `0.8` | `audio.bgm` | `BGMManager` 的播放音量 |
| 音效 | `1.0` | `audio.sfx` | `AudioManager` 的 `AudioSource.volume` |

API：

- `Load()`：读档；缺键用默认值
- `Bgm` / `Sfx`：当前 `0–1`
- `SetBgm(float)` / `SetSfx(float)`：夹到 `0–1`，立刻 `PlayerPrefs.Save()`，并通知监听者
- `event Action OnChanged`：两个 Manager 和菜单滑条都只靠这次回调/主动读取，不每帧轮询战斗

`AudioManager.Awake`：`Load()` 后 `audioSource.volume = AudioVolumeSettings.Sfx`。订阅 `OnChanged`，之后只改 `volume`，现有 `PlayOneShot(clip)` 自动乘上 Source 音量。

`BGMManager`：运行时忽略检视器 `volume`，缺档一律用 `0.8`，避免和 PlayerPrefs 两套默认打架。实际音量：

```
source.volume = AudioVolumeSettings.Bgm * fadeWeight
```

`fadeWeight` 为 `0–1`，由现有 DOTween 淡入淡出驱动。禁止再把用户音量直接写成 `source.volume` 的绝对目标，否则淡入会把用户调低的值冲回 `0.8`。

换曲流程保持「淡出 → 换 clip → 淡入」，只把 tween 目标从绝对音量改成 `fadeWeight`。用户在淡入淡出中途拖音乐条：不打断 tween，只重算 `source.volume`（新用户音量 × 当前 `fadeWeight`）。

进 Play 时 `BGMManager.Start` 在 `PlayBGM` 前 `Load()`，首播淡入目标是用户音量而不是检视器残留值。

## UI 落点

改 `PauseMenuController`：

- 新增分类面板（键位设置 / 声音设置 / 返回）
- 新增声音面板（两条 `Slider` + 数值标签 + 返回）
- 现有改键面板不动，只是从分类页进入
- `settingsOpen` 改为页枚举：`Root` / `Hub` / `Keybind` / `Audio`，供 B 返回判断
- 滑条 `transition = None`，选中色由现有 `RefreshSelectionVisual` 扩展到滑条填充 / 标签

不新建独立 Canvas。不把滑条塞进改键页。

## 文档

- `Docs/architecture/05-input-lockon.md`：设置 = 分类页（键位 / 声音）；B 返回多一层
- `Docs/architecture/05-input-lockon-test.md`：分类页、声音滑条、B 从声音页回到分类
- `Docs/architecture/06-presentation.md`：M15 增加用户音量（音乐 / 音效，PlayerPrefs，无 Mixer）
- `Docs/architecture/06-presentation-test.md`：拖音乐条 BGM 变；音效条拉到 0 后弹反无声

## 验收

| 操作 | 预期 |
|------|------|
| 暂停 → 设置 | 看到「键位设置 / 声音设置 / 返回」，不是直接改键页 |
| 声音设置：拖音乐条 | BGM 立刻变响或变轻；旁标 0–100 |
| 声音设置：音效拉到 0，继续战斗后弹反 | 无音效；BGM 仍按音乐条 |
| 音乐拉到 0，弹反 | 无 BGM；弹反仍有声 |
| 退出 Play 再进 | 两条滑条仍是上次的值 |
| 声音页按 B | 回到分类页，不关暂停 |
| 手柄选中滑条左右 | 音量变化，焦点不跑到另一条 |

## 风险

- DOTween 若仍 tween `source.volume`，会和滑条抢值 → 必须改成 tween `fadeWeight`
- `PlayOneShot` 第二参数若以后被写成 `1`，会盖过 Source 音量 → 本方案不改调用点，只改 `audioSource.volume`
