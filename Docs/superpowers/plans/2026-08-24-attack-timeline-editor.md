# 攻击时间轴编辑器实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法跟踪进度。

**目标：** 给玩家 `AttackConfig` 和 Boss `BossMoveWindow` 做同一个「对着动画拖判定 / 音效」窗口；运行时出招时钟改为这一招动画已播放时间。

**架构：** 时间数据留在招式 SO / 招式表。`AttackAnimClock` 从 Animator 第 0 层读秒数；`AttackState` 用该秒数开关刀、出声、取消、连招、结束。出声走 `CombatEventBus.OnAttackSfx` → `AudioManager`。编辑器隔离预览，不写 Animation Event、不上 Timeline。

**技术栈：** Unity 2022.3、C#、HFSM、`PreviewRenderUtility`、ScriptableObject。本项目无自动化测试；每个任务用 Unity 编译 0 error + 手测。不要新建 Test Runner 程序集。不要擅自 git commit（仅当用户本会话明确要求时才提交）。

**规格：** `Docs/superpowers/specs/2026-08-24-attack-timeline-editor-design.md`

**本任务只改这些架构文档：** `01-states.md`、`03-hit-detection.md`、`06-presentation.md`、`06-presentation-test.md`、`07-anim-events.md`、`07-anim-events-test.md`

**现有 API（必须按此写，不要发明别名）：**

| 用途 | 名字 |
| --- | --- |
| 动画短名 | `AnimUtil.IsPlaying(info, shortName)` / `AnimUtil.HasState(animator, shortName)` |
| 开/关刀 | `body.EnableWeaponHit(config)` / `body.DisableWeaponHit()` |
| 出招 CrossFade | `body.Animator.CrossFade(config.AnimName, config.TransitionDuration, 0)` |
| 判定多段 | `HitPulse.start` / `HitPulse.end`，数组字段名 `hitPulses` |
| 窗口不等式 | `0 <= HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration` |
| 总线 | `CombatEventBus.TriggerX` + `OnX` 成对 |

不要做：Unity Timeline、Animation Event 开关 Hitbox、改 CombatManager 结算、改行为树选招、把连招/转向画进时间轴。

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 修改 `Assets/Scripts/SO/AttackConfig.cs` | `AttackSfxCue`、`sfxCues`；`hitPulses` 注释改为玩家也可 1 段 |
| 创建 `Assets/Scripts/SO/AttackWindowSync.cs` | 从 pulse 回填开/关/连招上限 |
| 修改 `Assets/Scripts/Boss/BossMoveWindow.cs` | `sfxCues` |
| 修改 `Assets/Scripts/Boss/BossAttackBaker.cs` | 拷 `sfxCues` |
| 创建 `Assets/Scripts/FrameWork/AttackAnimClock.cs` | 从 Animator 读本招秒数 |
| 修改 `Assets/Scripts/Mgr/CombatEventBus.cs` | `OnAttackSfx` / `TriggerAttackSfx` |
| 修改 `Assets/Scripts/Audio/AudioManager.cs` | 订阅并 `PlayOneShot` |
| 修改 `Assets/Scripts/FrameWork/States/Ground/AttackState.cs` | 秒表换成 `AttackAnimClock`；播 `sfxCues` |
| 创建 `Assets/Editor/AttackTimelinePrefs.cs` | 玩家/Boss 预览 Prefab GUID |
| 创建 `Assets/Editor/AttackTimelineClipFinder.cs` | Controller 里按短名找 `AnimationClip` |
| 创建 `Assets/Editor/AttackTimelinePreview.cs` | `PreviewRenderUtility` 隔离预览 |
| 创建 `Assets/Editor/AttackTimelineWindow.cs` | 窗口：预览 + 判定轨 + 音效轨 |
| 创建 `Assets/Editor/AttackConfigEditor.cs` | Inspector 按钮打开窗口 |
| 修改 `Assets/Editor/BossMoveTableEditor.cs` | Inspector 按钮打开窗口 |
| 修改上列架构文档 + 对应 test | 时钟、玩家 pulse、新事件、手测清单 |

---

### 任务 1：招式数据（sfxCues + 回填）

**文件：**
- 修改：`Assets/Scripts/SO/AttackConfig.cs`
- 创建：`Assets/Scripts/SO/AttackWindowSync.cs`
- 修改：`Assets/Scripts/Boss/BossMoveWindow.cs`
- 修改：`Assets/Scripts/Boss/BossAttackBaker.cs`

- [ ] **步骤 1：在 `HitPulse` 后加入 `AttackSfxCue`，并给 `AttackConfig` 加字段**

把 `hitPulses` 的 Header/Tooltip 改成玩家 1 段、Boss 多段。在 `hitPulses` 后追加：

```csharp
[Serializable]
public class AttackSfxCue
{
    public float time;
    public AudioClip clip;
}
```

```csharp
    [Header("出招音效（可选）")]
    [Tooltip("相对本招动画 0 点的秒。与判定窗独立。clip 为空则跳过")]
    public AttackSfxCue[] sfxCues;
```

- [ ] **步骤 2：创建 `AttackWindowSync.cs`（后续任务禁止改方法名）**

```csharp
using UnityEngine;

public static class AttackWindowSync
{
    public static void ApplyPulses(AttackConfig cfg, HitPulse[] pulses)
    {
        if (cfg == null) return;
        cfg.hitPulses = pulses;
        if (pulses == null || pulses.Length == 0) return;
        HitPulse first = pulses[0];
        HitPulse last = pulses[pulses.Length - 1];
        if (first == null || last == null) return;
        cfg.HitStartTime = first.start;
        cfg.RecoveryWindowStart = last.end;
        if (cfg.ComboWindowEnd < cfg.RecoveryWindowStart)
            cfg.ComboWindowEnd = cfg.RecoveryWindowStart;
    }

    public static void ApplyPulses(BossMoveWindow w, HitPulse[] pulses)
    {
        if (w == null) return;
        w.hitPulses = pulses;
        if (pulses == null || pulses.Length == 0) return;
        HitPulse first = pulses[0];
        HitPulse last = pulses[pulses.Length - 1];
        if (first == null || last == null) return;
        w.hitStartTime = first.start;
        w.recoverStart = last.end;
        if (w.comboWindowEnd < w.recoverStart)
            w.comboWindowEnd = w.recoverStart;
    }

    public static HitPulse[] ClampPulses(HitPulse[] pulses, float clipLength)
    {
        if (pulses == null) return null;
        float len = Mathf.Max(0.01f, clipLength);
        HitPulse[] result = new HitPulse[pulses.Length];
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (p == null)
            {
                result[i] = new HitPulse { start = 0f, end = 0.01f };
                continue;
            }
            float start = Mathf.Clamp(p.start, 0f, len);
            float end = Mathf.Clamp(p.end, 0f, len);
            if (start >= end)
                end = Mathf.Min(len, start + 0.01f);
            result[i] = new HitPulse { start = start, end = end };
        }
        return result;
    }
}
```

- [ ] **步骤 3：`BossMoveWindow` 增加 `sfxCues`**

```csharp
    [Tooltip("相对本段动画 0 点。clip 为空则跳过")]
    public AttackSfxCue[] sfxCues;
```

- [ ] **步骤 4：`BossAttackBaker.Bake` 在拷完 `hitPulses` 之后拷音效**

```csharp
        if (w.sfxCues != null && w.sfxCues.Length > 0)
        {
            cfg.sfxCues = new AttackSfxCue[w.sfxCues.Length];
            for (int i = 0; i < w.sfxCues.Length; i++)
            {
                AttackSfxCue src = w.sfxCues[i];
                if (src == null) continue;
                cfg.sfxCues[i] = new AttackSfxCue { time = src.time, clip = src.clip };
            }
        }
```

- [ ] **步骤 5：验证**

Unity 回到 Editor 等域重载。Console 0 error。`AttackConfig` Inspector 出现「出招音效」；Boss 招式表窗口项出现 `sfxCues`。

- [ ] **步骤 6：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/SO/AttackConfig.cs Assets/Scripts/SO/AttackWindowSync.cs Assets/Scripts/Boss/BossMoveWindow.cs Assets/Scripts/Boss/BossAttackBaker.cs
git commit -m "feat: 招式配置增加独立出招音效点，并从 hitPulses 回填开关窗"
```

---

### 任务 2：动画时钟

**文件：**
- 创建：`Assets/Scripts/FrameWork/AttackAnimClock.cs`

- [ ] **步骤 1：实现（后续禁止改类名/方法名）**

```csharp
using UnityEngine;

public static class AttackAnimClock
{
    public const int Layer = 0;

    public static float ReadSeconds(Animator animator, string animName)
    {
        if (animator == null || string.IsNullOrEmpty(animName))
            return 0f;

        int hash = Animator.StringToHash(animName);

        if (animator.IsInTransition(Layer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(Layer);
            if (next.shortNameHash == hash)
                return SecondsFrom(next);
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(Layer);
        if (current.shortNameHash == hash)
            return SecondsFrom(current);

        return 0f;
    }

    static float SecondsFrom(AnimatorStateInfo info)
    {
        if (info.length <= 0.0001f)
            return 0f;
        return info.normalizedTime * info.length;
    }
}
```

- [ ] **步骤 2：验证**

编译 0 error。本任务不改 `AttackState`，进 Play 行为应与改前相同。

- [ ] **步骤 3：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/FrameWork/AttackAnimClock.cs
git commit -m "feat: 从 Animator 读取本招已播放秒数"
```

---

### 任务 3：出招音效总线

**文件：**
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
- 修改：`Assets/Scripts/Audio/AudioManager.cs`

- [ ] **步骤 1：在 `OnCameraShake` 后增加事件与 Trigger**

```csharp
    public static event Action<AudioClip, Vector3> OnAttackSfx;

    public static void TriggerAttackSfx(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null) return;
        OnAttackSfx?.Invoke(clip, worldPos);
    }
```

- [ ] **步骤 2：`AudioManager` 增加字段并订阅**

在现有 clip 字段后（不必新 AudioClip 资源，出招音由 `AttackSfxCue.clip` 带来）：

`OnEnable` 增加：`CombatEventBus.OnAttackSfx += HandleAttackSfx;`  
`OnDisable` 对称取消。

```csharp
    private void HandleAttackSfx(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
```

第一版忽略 `worldPos`（规格允许 2D 播）。

- [ ] **步骤 3：验证**

编译 0 error。进 Play 打人，原有弹反/受击音仍在。本任务还没有人调用 `TriggerAttackSfx`，不应凭空出声。

- [ ] **步骤 4：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/Mgr/CombatEventBus.cs Assets/Scripts/Audio/AudioManager.cs
git commit -m "feat: 出招音效走 CombatEventBus"
```

---

### 任务 4：AttackState 改用动画时间

**文件：**
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`

- [ ] **步骤 1：用 `animTime` 替换 `stateTimer` 的读写**

字段：

```csharp
    private float animTime;
    private bool weaponHitEnabled;
    private bool[] sfxFired;
```

`OnEnter`：`animTime = 0f;` 并 `sfxFired = null;`（下一帧按 `sfxCues` 长度分配）。仍 `CrossFade(..., 0)`。`ApplyHitbox()` 仍在 Enter 末尾调一次。

`OnUpdate` 开头：

```csharp
        animTime = AttackAnimClock.ReadSeconds(body.Animator, config.AnimName);
        body.IsAttackRecoveryOpen = animTime >= config.RecoveryWindowStart;
        ApplyHitbox();
        ApplySfx();
```

后面所有 `stateTimer` 改成 `animTime`（转向、结束、`HandleCommand` 取消/连招/移动）。

`IsInHitPulse` 里比较 `animTime`。`ApplyHitbox` 空 pulse 回退仍用 `HitStartTime` / `RecoveryWindowStart`（旧 SO）。

- [ ] **步骤 2：增加 `ApplySfx`**

```csharp
    private void ApplySfx()
    {
        AttackSfxCue[] cues = config.sfxCues;
        if (cues == null || cues.Length == 0) return;
        if (sfxFired == null || sfxFired.Length != cues.Length)
            sfxFired = new bool[cues.Length];

        for (int i = 0; i < cues.Length; i++)
        {
            if (sfxFired[i]) continue;
            AttackSfxCue cue = cues[i];
            if (cue == null || cue.clip == null) continue;
            if (animTime < cue.time) continue;
            sfxFired[i] = true;
            CombatEventBus.TriggerAttackSfx(cue.clip, body.transform.position);
        }
    }
```

- [ ] **步骤 3：验证（手测）**

进 Play 打玩家轻击：仍有判定、仍能弹刀取消、连招仍能接。若某招 `StateDuration` 明显短于动画，会更早回 Idle（这是规格：结束也跟 `t`）。旧 SO 空 `hitPulses` 仍能开刀。

- [ ] **步骤 4：Commit（仅用户明确要求时）**

```bash
git add Assets/Scripts/FrameWork/States/Ground/AttackState.cs
git commit -m "feat: 攻击窗口与出招音效改为跟本招动画时间"
```

---

### 任务 5：编辑器预览基础设施

**文件：**
- 创建：`Assets/Editor/AttackTimelinePrefs.cs`
- 创建：`Assets/Editor/AttackTimelineClipFinder.cs`
- 创建：`Assets/Editor/AttackTimelinePreview.cs`

全部包在 `#if UNITY_EDITOR` 或放在 `Assets/Editor/`（本项目 Editor 脚本已在该目录，无需再包一层）。

- [ ] **步骤 1：`AttackTimelinePrefs.cs`**

```csharp
using UnityEditor;
using UnityEngine;

public static class AttackTimelinePrefs
{
    const string PlayerGuidKey = "ARPG.AttackTimeline.PlayerPrefabGuid";
    const string BossGuidKey = "ARPG.AttackTimeline.BossPrefabGuid";

    public static GameObject PlayerPrefab
    {
        get { return Load(PlayerGuidKey); }
        set { Save(PlayerGuidKey, value); }
    }

    public static GameObject BossPrefab
    {
        get { return Load(BossGuidKey); }
        set { Save(BossGuidKey, value); }
    }

    static GameObject Load(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static void Save(string key, GameObject prefab)
    {
        if (prefab == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }
        string path = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(key, guid);
    }
}
```

- [ ] **步骤 2：`AttackTimelineClipFinder.cs`**

```csharp
using UnityEditor.Animations;
using UnityEngine;

public static class AttackTimelineClipFinder
{
    public static AnimationClip Find(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return null;
        return Find(animator.runtimeAnimatorController, stateName);
    }

    public static AnimationClip Find(RuntimeAnimatorController runtime, string stateName)
    {
        if (runtime == null || string.IsNullOrEmpty(stateName))
            return null;

        AnimatorOverrideController ov = runtime as AnimatorOverrideController;
        if (ov != null)
            runtime = ov.runtimeAnimatorController;

        AnimatorController ac = runtime as AnimatorController;
        if (ac == null) return null;

        int hash = Animator.StringToHash(stateName);
        for (int i = 0; i < ac.layers.Length; i++)
        {
            AnimationClip clip = FindInMachine(ac.layers[i].stateMachine, hash);
            if (clip != null) return clip;
        }
        return null;
    }

    static AnimationClip FindInMachine(AnimatorStateMachine machine, int shortNameHash)
    {
        if (machine == null) return null;

        ChildAnimatorState[] states = machine.states;
        for (int i = 0; i < states.Length; i++)
        {
            AnimatorState st = states[i].state;
            if (st == null) continue;
            if (Animator.StringToHash(st.name) != shortNameHash) continue;
            return st.motion as AnimationClip;
        }

        ChildAnimatorStateMachine[] children = machine.stateMachines;
        for (int i = 0; i < children.Length; i++)
        {
            AnimationClip clip = FindInMachine(children[i].stateMachine, shortNameHash);
            if (clip != null) return clip;
        }
        return null;
    }
}
```

- [ ] **步骤 3：`AttackTimelinePreview.cs`**

```csharp
using UnityEditor;
using UnityEngine;

public class AttackTimelinePreview
{
    PreviewRenderUtility utility;
    GameObject instance;
    Animator animator;
    GameObject boundPrefab;

    public Animator Animator { get { return animator; } }

    public void Ensure(GameObject prefab)
    {
        if (prefab == boundPrefab && instance != null)
            return;
        CleanupInstance();
        boundPrefab = prefab;
        if (prefab == null) return;

        if (utility == null)
        {
            utility = new PreviewRenderUtility();
            utility.cameraFieldOfView = 25f;
            utility.camera.nearClipPlane = 0.01f;
            utility.camera.farClipPlane = 50f;
        }

        instance = Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.position = Vector3.zero;
        instance.transform.rotation = Quaternion.identity;
        utility.AddSingleGO(instance);
        animator = instance.GetComponentInChildren<Animator>();
        FrameCamera();
    }

    public void Sample(string stateName, float normalizedTime)
    {
        if (animator == null || string.IsNullOrEmpty(stateName))
            return;
        if (!AnimUtil.HasState(animator, stateName))
            return;
        animator.Play(stateName, 0, Mathf.Clamp01(normalizedTime));
        animator.Update(0f);
    }

    public void Draw(Rect rect)
    {
        if (utility == null || instance == null)
        {
            EditorGUI.HelpBox(rect, "未指定预览 Prefab，或 Prefab 上没有可预览的物体。", MessageType.Info);
            return;
        }

        utility.BeginPreview(rect, GUIStyle.none);
        utility.camera.Render();
        Texture tex = utility.EndPreview();
        GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, false);
    }

    public void Dispose()
    {
        CleanupInstance();
        if (utility != null)
        {
            utility.Cleanup();
            utility = null;
        }
        boundPrefab = null;
    }

    void CleanupInstance()
    {
        animator = null;
        if (instance != null)
        {
            Object.DestroyImmediate(instance);
            instance = null;
        }
    }

    void FrameCamera()
    {
        if (utility == null || instance == null) return;
        Bounds b = new Bounds(instance.transform.position, Vector3.one);
        Renderer[] rs = instance.GetComponentsInChildren<Renderer>();
        bool has = false;
        for (int i = 0; i < rs.Length; i++)
        {
            if (!has)
            {
                b = rs[i].bounds;
                has = true;
            }
            else b.Encapsulate(rs[i].bounds);
        }
        Vector3 center = b.center;
        float size = Mathf.Max(b.extents.magnitude, 0.5f);
        utility.camera.transform.position = center + new Vector3(0f, size * 0.2f, -size * 2.4f);
        utility.camera.transform.LookAt(center);
    }
}
```

若 `AddSingleGO` 编译失败：改用把 `instance.transform` 挂到 `utility.camera.transform` 下并设本地坐标，不要改场景里的物体。

- [ ] **步骤 4：验证**

编译 0 error。此时还没有窗口，菜单里不会出现新项。

- [ ] **步骤 5：Commit（仅用户明确要求时）**

```bash
git add Assets/Editor/AttackTimelinePrefs.cs Assets/Editor/AttackTimelineClipFinder.cs Assets/Editor/AttackTimelinePreview.cs
git commit -m "feat: 攻击时间轴预览与 Clip 查找"
```

---

### 任务 6：攻击时间轴窗口

**文件：**
- 创建：`Assets/Editor/AttackTimelineWindow.cs`
- 创建：`Assets/Editor/AttackConfigEditor.cs`
- 修改：`Assets/Editor/BossMoveTableEditor.cs`

- [ ] **步骤 1：窗口打开入口**

```csharp
using UnityEditor;
using UnityEngine;

public class AttackTimelineWindow : EditorWindow
{
    AttackConfig playerConfig;
    BossMoveTable bossTable;
    int moveIndex;
    int segmentIndex;
    float scrub;
    AttackTimelinePreview preview = new AttackTimelinePreview();
    Vector2 scroll;

    int dragPulse = -1;
    int dragEdge; // 0=start, 1=end
    int dragSfx = -1;

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
    }

    public static void Open(BossMoveTable table)
    {
        AttackTimelineWindow w = GetWindow<AttackTimelineWindow>("攻击时间轴");
        w.bossTable = table;
        w.playerConfig = null;
        w.moveIndex = 0;
        w.segmentIndex = 0;
        w.Show();
    }

    void OnDisable()
    {
        preview.Dispose();
    }
```

后面 `OnGUI` 顺序必须是：Prefab 设置 → 当前招式选择 → 预览 Rect（约 220px 高）→ 时间轴 → 保存。

当前动画名：

- 玩家：`playerConfig.AnimName`
- Boss：`bossTable.moves[moveIndex].sequences[0].states[segmentIndex]`（缺则空，HelpBox）

预览 Prefab：玩家用 `AttackTimelinePrefs.PlayerPrefab`，Boss 用 `BossPrefab`。用 `EditorGUILayout.ObjectField` 显示并可改，改了写回 Prefab 属性。

`clip = AttackTimelineClipFinder.Find(preview.Animator, animName)`；`clipLength = clip != null ? clip.length : 1f`。

`preview.Ensure(prefab)` 后 `preview.Sample(animName, clipLength > 0f ? scrub / clipLength : 0f)`，再 `preview.Draw(previewRect)`。

找不到状态：`Debug.LogError` 一句中文 + HelpBox，不要抛异常。

- [ ] **步骤 2：判定轨与音效轨（IMGUI）**

显示用的 pulse：若 `hitPulses` 为空，用一对开/关拼一条（只用于显示，点保存才写入数组）。

```csharp
    static HitPulse[] DisplayPulses(HitPulse[] stored, float hitStart, float recover)
    {
        if (stored != null && stored.Length > 0)
            return stored;
        float end = Mathf.Max(hitStart + 0.01f, recover);
        return new[] { new HitPulse { start = hitStart, end = end } };
    }
```

玩家：显示一条，按钮「加段」仍可用（Boss 才需要；玩家也可加，YAGNI 不禁止）。

时间轴：在 `GUILayoutUtility.GetRect(18, 22)` 得到条带 Rect。`x = rect.x + time / clipLength * rect.width`。

拖动：`EventType.MouseDown` 命中红条左/右缘 6px 设 `dragPulse`/`dragEdge`；命中音符设 `dragSfx`；否则拖 playhead（改 `scrub`）。`MouseDrag` 更新对应时间并 `Repaint`。`MouseUp` 清 drag。

pulse 拖完用 `AttackWindowSync.ClampPulses`。sfx 时间钳制到 `[0, clipLength]`。

音效：`EditorGUILayout` 列表，每行 time 滑条 + `ObjectField<AudioClip>` + 删除。按钮「加音符」追加 `new AttackSfxCue { time = scrub }`。

按钮「用动画长度填写 StateDuration / stateDuration」：把当前 clip.length 写进正在编的配置。

- [ ] **步骤 3：保存**

玩家：

```csharp
        Undo.RecordObject(playerConfig, "Attack Timeline");
        HitPulse[] clamped = AttackWindowSync.ClampPulses(workingPulses, clipLength);
        AttackWindowSync.ApplyPulses(playerConfig, clamped);
        playerConfig.sfxCues = workingSfx;
        EditorUtility.SetDirty(playerConfig);
```

Boss：

```csharp
        BossMoveEntry entry = bossTable.moves[moveIndex];
        BossMoveWindow w = BossMovePicker.WindowFor(entry, segmentIndex);
        Undo.RecordObject(bossTable, "Attack Timeline");
        HitPulse[] clamped = AttackWindowSync.ClampPulses(workingPulses, clipLength);
        AttackWindowSync.ApplyPulses(w, clamped);
        w.sfxCues = workingSfx;
        // WindowFor 返回的是数组元素（struct 会丢）。必须写回 entry.windows[i]
```

`BossMoveWindow` 是 class（引用类型），`WindowFor` 返回的是数组里的同一对象，写字段即可，不必拷回。打开 `BossMoveWindow.cs` 确认是 `class` 再写；若被改成 struct，按索引写 `entry.windows[i] = w`。

- [ ] **步骤 4：Inspector 按钮**

`AttackConfigEditor.cs`：

```csharp
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(AttackConfig))]
public class AttackConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("打开攻击时间轴"))
            AttackTimelineWindow.Open((AttackConfig)target);
    }
}
```

`BossMoveTableEditor.OnInspectorGUI` 在「填入默认」按钮旁增加：

```csharp
        if (GUILayout.Button("打开攻击时间轴"))
            AttackTimelineWindow.Open((BossMoveTable)target);
```

在该按钮下方加一句 `EditorGUILayout.HelpBox("「填入弦一郎默认招式表」会覆盖已对过的判定和音效。", MessageType.Warning);`

- [ ] **步骤 5：验证（手测）**

1. `ARPG/攻击时间轴` 能打开。第一次没 Prefab 时预览区提示去填，不改场景。
2. 选中玩家 `AttackConfig` 点按钮，拖进度，刀姿势变化。
3. 拖红条两端，点保存，SO 上 `hitPulses.Length >= 1`，`HitStartTime`/`RecoveryWindowStart` 已回填。
4. 加一个音符拖 clip，保存后 `sfxCues` 有数据。
5. Boss 表选飞舟某一段，能加多段红。
6. 非法拖反（起点超过终点）保存后仍 `start < end`。

- [ ] **步骤 6：Commit（仅用户明确要求时）**

```bash
git add Assets/Editor/AttackTimelineWindow.cs Assets/Editor/AttackConfigEditor.cs Assets/Editor/BossMoveTableEditor.cs
git commit -m "feat: 攻击时间轴窗口，对着动画编辑判定与出招音效"
```

---

### 任务 7：架构文档

只改本计划列出的文档，不要通读 `Docs/`。

- [ ] **步骤 1：`01-states.md` 攻击前摇小节**

把 `stateTimer` 全部改成「本招动画已播放时间 `t`（`AttackAnimClock.ReadSeconds`）」。保留窗口字段名。补一句：有 `hitPulses` 时开/关刀按各段 `[start,end)`，并回填 `HitStartTime`/`RecoveryWindowStart` 给取消/连招用。

- [ ] **步骤 2：`03-hit-detection.md`**

- 技术方案开头的 Animation Event 开关改为：`AttackState` 按动画时间 `t` 开关。扫描方式仍是每帧 Cast，不要改成 OnTrigger。
- 「玩家招式不要填」改为：玩家通常 `hitPulses` 长度 1；空数组仍回退一对开/关。

- [ ] **步骤 3：`07-anim-events.md` 与 `07-anim-events-test.md`**

- 删除/改写「在 Clip 上加 EnableHitbox」作为攻击判定流程。注意事项已有的 M8 句扩展为：判定与出招音效由 `AttackState` 读动画时间；时间轴工具写 SO。动画事件只保留射箭生成帧。
- test 表第 1、2、4 行和「伤害提前/延后」改为：用 `ARPG/攻击时间轴` 拖红条，进 Play 对刀。

- [ ] **步骤 4：`06-presentation.md` 与 `06-presentation-test.md`**

事件列表增加：`OnAttackSfx(AudioClip clip, Vector3 worldPos)`。test M15 增加一行：招式 `sfxCues` 有 clip 时，播到该时刻出声。

把规格第 6 节 7 条验收拷进 `07-anim-events-test.md` 或新建小节「攻击时间轴」，不要另造验收标准。

- [ ] **步骤 5：验证**

文档与规格一致：不出现「请在 Clip 上加 EnableHitbox 来开刀」作为现行流程。

- [ ] **步骤 6：Commit（仅用户明确要求时）**

```bash
git add Docs/architecture/01-states.md Docs/architecture/03-hit-detection.md Docs/architecture/06-presentation.md Docs/architecture/06-presentation-test.md Docs/architecture/07-anim-events.md Docs/architecture/07-anim-events-test.md
git commit -m "docs: 攻击判定与出招音效改为动画时间，并增加时间轴验收"
```

---

## 自检

| 规格章节 | 任务 |
| --- | --- |
| 2.1–2.3 数据 / 烘焙 | 任务 1 |
| 2.4 时间含义 | 任务 2 + 4 |
| 3 运行时时钟、判定、取消、连招、结束、音效、旧 SO 回退、射箭不动 | 任务 2、3、4（射箭：不改相关脚本） |
| 4 编辑器窗口、Prefab、轨、Boss 选段、默认表警告 | 任务 5、6 |
| 5 错误表 | 任务 5 HelpBox、任务 1 Clamp、任务 4 空 pulse、任务 3 clip==null |
| 6 验收 | 任务 4/6 手测 + 任务 7 写入 test 文档 |
| 红线：不写 Anim Event / 不上 Timeline / 总线出声 | 全任务都不创建 Timeline 资源；音效仅 TriggerAttackSfx |

无 TODO/待定。类型名锁定：`AttackSfxCue`、`sfxCues`、`AttackWindowSync.ApplyPulses`、`AttackAnimClock.ReadSeconds`、`CombatEventBus.TriggerAttackSfx`。
