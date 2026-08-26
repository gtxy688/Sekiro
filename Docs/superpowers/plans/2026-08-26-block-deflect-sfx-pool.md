# 格挡 / 弹反多音效随机池 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法跟踪进度。

**目标：** 格挡和弹反各从 Resources 文件夹随机播一条 wav，连打不连抽同一条。

**架构：** 仍订阅 `CombatEventBus.OnWeaponDeflected`。`AudioManager.Awake` 用 `Resources.LoadAll` 装两池；`HandleWeaponDeflected` 按 `DeflectType` 选池，共用 `Pick` 避开上次下标，再 `PlayOneShot(clip)`（不传 volume）。

**技术栈：** Unity 2022 LTS、`Resources.LoadAll<AudioClip>`、现有 `AudioSource`。本项目无自动化测试；每个任务用 Unity 编译 0 error + 手测验证。不要新建 Test Runner 程序集。不要擅自 git commit（仅当用户本会话明确要求时才提交）。

**规格：** `Docs/superpowers/specs/2026-08-26-block-deflect-sfx-pool-design.md`  
**本任务只改这两份架构文档：** `Docs/architecture/06-presentation.md`、`Docs/architecture/06-presentation-test.md`  
**不要改：** `CombatEventBus`、`DeflectState`、`CombatManager`、`GameScene.unity` YAML、其它音效字段、音高、Mixer。

**现有 API（必须按此写，不要发明别名）：**

| 用途 | 名字 |
| --- | --- |
| 打铁事件 | `CombatEventBus.OnWeaponDeflected` / `TriggerWeaponDeflected(Vector3, DeflectType)` |
| 类型 | `DeflectType.Normal`（格挡）、`DeflectType.Perfect`（弹反） |
| 加载 | `Resources.LoadAll<AudioClip>("Sounds/Block")`、`Resources.LoadAll<AudioClip>("Sounds/Deflect")` |
| 播放 | `audioSource.PlayOneShot(clip)` — 禁止第二参数 |

磁盘（gitignored，不要搬文件）：

- `Assets/Resources/Sounds/Block/Block1.wav` … `Block14.wav`
- `Assets/Resources/Sounds/Deflect/Deflect1.wav` … `Deflect4.wav`

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 修改 `Assets/Scripts/Audio/AudioManager.cs` | 删单 clip 字段；Awake 装池；Pick + 事件播放 |
| 修改 `Docs/architecture/06-presentation.md` | 补 M15：Resources 池 + 不连抽 |
| 修改 `Docs/architecture/06-presentation-test.md` | #17/#18 多 clip；FAQ 文件夹空 |

不要做：Inspector 数组、SO、改事件签名、改 `GameScene.unity`、音高随机。

---

### 任务 1：AudioManager 装池并随机播放

**文件：**
- 修改：`Assets/Scripts/Audio/AudioManager.cs`

- [ ] **步骤 1：把 `AudioManager.cs` 整文件换成下面这份（其它事件处理保持原样）**

类注释改成从 Resources 加载格挡/弹反池。删除 `deflectSfx` / `blockSfx` 两个 public 字段。新增两池、两个 lastIndex（初值 `-1`）、路径常量。`Awake` 在拿到 `AudioSource` 之后立刻 `LoadPool`。`HandleWeaponDeflected` 只走 `Pick` + `PlayOneShot`。

```csharp
using UnityEngine;

// 音效管理器：订阅 CombatEventBus，播放对应 AudioClip
// 与 FXManager 同模式（事件驱动，不做每帧轮询）
// 格挡/弹反从 Resources 文件夹装池，每次随机且不连抽同一条
public class AudioManager : MonoBehaviour
{
    private const string BlockFolder = "Sounds/Block";
    private const string DeflectFolder = "Sounds/Deflect";

    [Header("音效资源")]
    public AudioClip hitSfx;          // 受击
    public AudioClip perilousSfx;     // 危字警示
    public AudioClip finisherSfx;     // 忍杀处决
    public AudioClip deathSfx;        // 玩家死亡
    public AudioClip gourdSfx;        // 喝葫芦
    public AudioClip reviveSfx;       // 回生（M14）
    public AudioClip victorySfx;      // 胜利（M10）

    private AudioSource audioSource;
    private AudioClip[] blockPool = System.Array.Empty<AudioClip>();
    private AudioClip[] deflectPool = System.Array.Empty<AudioClip>();
    private int lastBlockIndex = -1;
    private int lastDeflectIndex = -1;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        // 若没有 AudioSource，自动补一个（方便场景搭建）
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;

        blockPool = LoadPool(BlockFolder);
        deflectPool = LoadPool(DeflectFolder);
    }

    // 订阅事件
    private void OnEnable()
    {
        CombatEventBus.OnWeaponDeflected += HandleWeaponDeflected;
        CombatEventBus.OnTakeDamage += HandleTakeDamage;
        CombatEventBus.OnPerilousAttack += HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered += HandleFinisherTriggered;
        CombatEventBus.OnDeath += HandleDeath;
        CombatEventBus.OnGourdUsed += HandleGourdUsed;
        CombatEventBus.OnReviveAvailable += HandleReviveAvailable;
        CombatEventBus.OnVictory += HandleVictory;
        CombatEventBus.OnAttackSfx += HandleAttackSfx;
    }

    // 取消订阅
    private void OnDisable()
    {
        CombatEventBus.OnWeaponDeflected -= HandleWeaponDeflected;
        CombatEventBus.OnTakeDamage -= HandleTakeDamage;
        CombatEventBus.OnPerilousAttack -= HandlePerilousAttack;
        CombatEventBus.OnFinisherTriggered -= HandleFinisherTriggered;
        CombatEventBus.OnDeath -= HandleDeath;
        CombatEventBus.OnGourdUsed -= HandleGourdUsed;
        CombatEventBus.OnReviveAvailable -= HandleReviveAvailable;
        CombatEventBus.OnVictory -= HandleVictory;
        CombatEventBus.OnAttackSfx -= HandleAttackSfx;
    }

    // ===== 资源池 =====

    private static AudioClip[] LoadPool(string folder)
    {
        AudioClip[] loaded = Resources.LoadAll<AudioClip>(folder);
        if (loaded == null || loaded.Length == 0)
            return System.Array.Empty<AudioClip>();

        int n = 0;
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null) n++;
        }
        if (n == loaded.Length) return loaded;

        AudioClip[] filtered = new AudioClip[n];
        int w = 0;
        for (int i = 0; i < loaded.Length; i++)
        {
            if (loaded[i] != null) filtered[w++] = loaded[i];
        }
        return filtered;
    }

    // 池空返回 null 并 Warning；长 1 播那条；长 ≥ 2 均匀随机且 ≠ lastIndex。
    // 第一次 lastIndex < 0，在全池抽。
    private static AudioClip Pick(AudioClip[] pool, ref int lastIndex, string folder)
    {
        if (pool == null || pool.Length == 0)
        {
            Debug.LogWarning($"AudioManager: 音效池为空（Resources/{folder}）");
            return null;
        }

        int i;
        if (pool.Length == 1 || lastIndex < 0)
        {
            i = Random.Range(0, pool.Length);
        }
        else
        {
            i = Random.Range(0, pool.Length - 1);
            if (i >= lastIndex) i++;
        }

        lastIndex = i;
        return pool[i];
    }

    // ===== 事件处理 =====

    private void HandleWeaponDeflected(Vector3 hitPoint, DeflectType type)
    {
        AudioClip clip = null;
        if (type == DeflectType.Perfect)
            clip = Pick(deflectPool, ref lastDeflectIndex, DeflectFolder);
        else if (type == DeflectType.Normal)
            clip = Pick(blockPool, ref lastBlockIndex, BlockFolder);

        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }

    // 受击（这里只播 hitSfx；区分玩家/Boss 的音效可后续加字段）
    private void HandleTakeDamage(CharacterBody victim, int dmg, int currentHp)
    {
        if (hitSfx != null)
        {
            audioSource.PlayOneShot(hitSfx);
        }
    }

    private void HandlePerilousAttack(PerilousType type)
    {
        if (perilousSfx != null)
        {
            audioSource.PlayOneShot(perilousSfx);
        }
    }

    private void HandleFinisherTriggered(Vector3 pos)
    {
        if (finisherSfx != null)
        {
            audioSource.PlayOneShot(finisherSfx);
        }
    }

    private void HandleDeath(CharacterBody c)
    {
        if (deathSfx != null)
        {
            audioSource.PlayOneShot(deathSfx);
        }
    }

    private void HandleGourdUsed(CharacterBody c, int remaining)
    {
        if (gourdSfx != null)
        {
            audioSource.PlayOneShot(gourdSfx);
        }
    }

    private void HandleReviveAvailable(CharacterBody c)
    {
        if (reviveSfx != null)
        {
            audioSource.PlayOneShot(reviveSfx);
        }
    }

    private void HandleVictory(CharacterBody c)
    {
        if (victorySfx != null)
        {
            audioSource.PlayOneShot(victorySfx);
        }
    }

    // 出招音：clip 由事件携带；第一版忽略 worldPos
    private void HandleAttackSfx(AudioClip clip, Vector3 worldPos)
    {
        if (clip == null || audioSource == null) return;
        audioSource.PlayOneShot(clip);
    }
}
```

硬性约束：

- 路径字符串只能是 `Sounds/Block` / `Sounds/Deflect`，不要写成 `Block` 或 `Assets/Resources/...`
- `PlayOneShot` 只传 clip
- 不要把 `Pick` 复制成格挡/弹反两份

- [ ] **步骤 2：确认 Unity 编译无 error**

切回 Unity，等脚本编译完。Console 不得有 `AudioManager` 相关 CS 错误。`GameScene` 里 `AudioManager` 的 `blockSfx` / `deflectSfx` 槽位消失是正常的（字段已删，不必手改 YAML）。

若报 `blockSfx` / `deflectSfx` 找不到：说明旧文件没整份替换干净，对照步骤 1 再写一遍。

---

### 任务 2：架构文档与验收清单

**文件：**
- 修改：`Docs/architecture/06-presentation.md`
- 修改：`Docs/architecture/06-presentation-test.md`

- [ ] **步骤 1：在 `06-presentation.md` 震屏小节之后追加 M15 音效（不要改相机章节）**

文件目前在「震屏」处结束。在最后追加：

```markdown

## 三、音效（M15）

- 订阅 `OnWeaponDeflected`：`DeflectType.Normal` 从 `Resources/Sounds/Block` 随机一条；`Perfect` 从 `Resources/Sounds/Deflect` 随机一条。
- `AudioManager.Awake` 用 `Resources.LoadAll<AudioClip>` 各装一次；事件里不重载。
- 同一池连打不连抽同一条（池长 ≥ 2）。第一次全池均匀随机。池空则本发不播并 `LogWarning`。
- `PlayOneShot(clip)` 不传 volume，音量走 `AudioSource.volume`。
- 受击 / 处决 / 出招等其它 clip 仍 Inspector 拖，本规则只管格挡和弹反。
```

- [ ] **步骤 2：改 `06-presentation-test.md` 的 M15 表和 FAQ**

把 M15 表里 #17、#18 换成下面四行（保留 #19/#21/#22/#23 不动）：

```markdown
| 17 | 弹反成功 | 从 Deflect 池出声（清脆打铁），不是格挡那组 |
| 17b | 连续弹反 ≥ 3 次 | 相邻两次不是同一条（池 ≥ 2） |
| 18 | 普通防御 | 从 Block 池出声（沉闷格挡），不是弹反那组 |
| 18b | 连续格挡 ≥ 3 次 | 相邻两次不是同一条（池 ≥ 2） |
```

把 FAQ「没声音」那行换成：

```markdown
- **没声音**：AudioManager 没订阅；或 `Resources/Sounds/Block`、`Resources/Sounds/Deflect` 为空（应有 Warning）；或路径写成了 `Block` 而不是 `Sounds/Block`。其它音效仍看 Inspector 有没有拖 clip。
```

不要在这份文档里加音量滑条验收（那是另一份规格）。手测时若暂停菜单已有音效滑条，拉到 0 后格挡应无声——那是滑条方案的回归，不是本任务要写进 06 测试表的条目。

---

## 验收（交付给用户）

| 操作 | 预期 |
|------|------|
| 普通格挡 | Block 池出声 |
| 完美弹反 | Deflect 池出声 |
| 连续格挡 ≥ 3 次 | 相邻两次听得出不是同一条 |
| 连续弹反 ≥ 3 次 | 同上 |
| Play 时两文件夹都在 | Console 无「音效池为空」Warning |

实现者不要宣称完成；把上表交给用户在 Unity 里勾。

---

## 自检

| 规格条目 | 任务 |
| --- | --- |
| LoadAll `Sounds/Block` / `Sounds/Deflect`，去 null，只装一次 | 任务 1 `LoadPool` |
| Normal → 格挡池，Perfect → 弹反池，分记 lastIndex | 任务 1 `HandleWeaponDeflected` |
| 空池 Warning；长 1 播那条；≥ 2 不连抽；首次全池随机 | 任务 1 `Pick` |
| 删除 `blockSfx` / `deflectSfx` | 任务 1 字段 |
| `PlayOneShot` 不传 volume | 任务 1 |
| 共用 Pick，不复制两套 | 任务 1 |
| `06-presentation.md` / `-test.md` | 任务 2 |
| 不改事件签名、不改判定、不改其它 clip | 文件结构「不要做」 |
