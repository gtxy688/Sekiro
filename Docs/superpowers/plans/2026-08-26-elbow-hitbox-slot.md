# 肘击 Hitbox Slot 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:executing-plans 或 superpowers:subagent-driven-development 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** `Slash_SpinElbow` 的 `Elbow` 段打开肘上的 Hitbox，旋转横砍段仍用刀。

**架构：** 招式数据写枚举 `AttackHitboxSlot`；`CharacterBody` 拖各槽引用；`EnableWeaponHit` 按配置亮一把、同时只亮一把；缺肘引用回退刀。结算仍走现有 `Hitbox` → `CombatManager`。

**技术栈：** Unity 2022.3、C#、现有 HFSM / AttackState。本项目无自动化测试；用编译 0 error + 验收清单。不要新建 Test Runner。不要擅自 git commit。

**规格：** `Docs/superpowers/specs/2026-08-26-elbow-hitbox-slot-design.md`

**本任务只改这些架构文档：** `03-hit-detection.md`、`03-hit-detection-test.md`

**不要做：** 射箭投射物、Grab 新状态、Kick 接线、改 CombatManager 结算、OnTrigger、把 `Weapon` 改成肘。

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Scripts/Configs/AttackHitboxSlot.cs` | 槽位枚举 Weapon / Elbow / Kick |
| 修改 `Assets/Scripts/SO/AttackConfig.cs` | `HitboxSlot` |
| 修改 `Assets/Scripts/Boss/BossMoveWindow.cs` | `hitboxSlot` |
| 修改 `Assets/Scripts/Boss/BossAttackBaker.cs` | 烘焙拷贝 slot |
| 修改 `Assets/Scripts/Boss/GenichiroMoveCatalog.cs` | Elbow 段 `slot: Elbow` |
| 修改 `Assets/SO/Boss/GenichiroMoveTable.asset` | 只给 Elbow 窗口写 `hitboxSlot: 1`，不整表覆盖时间 |
| 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | 引用、自动找刀、Enable/Disable 切槽 |
| 修改 `Assets/Scripts/Combat/CombatFxPoint.cs` | 攻击者优先 `ActiveHitbox` |
| 修改 `Assets/Editor/AttackTimelineWindow.cs` | Boss 段枚举下拉 |
| 修改 `Docs/architecture/03-hit-detection.md` | 肢体 Hitbox 接线 |
| 修改 `Docs/architecture/03-hit-detection-test.md` | Elbow 验收 |

字段名锁定（后续任务禁止改名）：

| 用途 | 名字 |
| --- | --- |
| 枚举 | `AttackHitboxSlot.Weapon` / `.Elbow` / `.Kick` |
| AttackConfig | `HitboxSlot` |
| BossMoveWindow | `hitboxSlot` |
| CharacterBody 引用 | `weaponHitbox` / `elbowHitbox` / `kickHitbox` |
| 刀身份 | `Weapon`（只读属性，永远是刀） |
| 当前亮着 | `ActiveHitbox` |
| 解析 | `ResolveHitbox(AttackHitboxSlot slot)` |

---

### 任务 1：数据字段 + 烘焙 + 招式表

**文件：**
- 创建：`Assets/Scripts/Configs/AttackHitboxSlot.cs`
- 修改：`Assets/Scripts/SO/AttackConfig.cs`
- 修改：`Assets/Scripts/Boss/BossMoveWindow.cs`
- 修改：`Assets/Scripts/Boss/BossAttackBaker.cs`
- 修改：`Assets/Scripts/Boss/GenichiroMoveCatalog.cs`
- 修改：`Assets/SO/Boss/GenichiroMoveTable.asset`（Elbow 窗口）

- [ ] **步骤 1：新建枚举**

```csharp
public enum AttackHitboxSlot
{
    Weapon = 0,
    Elbow = 1,
    Kick = 2
}
```

- [ ] **步骤 2：`AttackConfig` 在 `Perilous` 后加**

```csharp
    [Header("判定 Hitbox")]
    [Tooltip("本招用哪把采样点。玩家保持 Weapon。Boss 肘击选 Elbow。")]
    public AttackHitboxSlot HitboxSlot = AttackHitboxSlot.Weapon;
```

- [ ] **步骤 3：`BossMoveWindow` 在 `perilous` 后加**

```csharp
    [Tooltip("本段用哪把 Hitbox。默认刀；Elbow 段选 Elbow。缺引用时运行时回退刀。")]
    public AttackHitboxSlot hitboxSlot = AttackHitboxSlot.Weapon;
```

- [ ] **步骤 4：`BossAttackBaker.Bake` 在 `cfg.Perilous = ...` 之后加**

```csharp
        cfg.HitboxSlot = w.hitboxSlot;
```

- [ ] **步骤 5：Catalog 的 `Hit` 增加可选 `slot`，写入 window；仅 Elbow 调用改 slot**

```csharp
    static BossMoveWindow Hit(float duration, float hitAt = 0.2f, float recoverAt = -1f,
        PerilousType perilous = PerilousType.None,
        AttackHitboxSlot slot = AttackHitboxSlot.Weapon)
    {
        float rec = recoverAt > 0f ? recoverAt : duration * 0.55f;
        return new BossMoveWindow
        {
            hitStartTime = hitAt,
            recoverStart = rec,
            comboWindowEnd = Mathf.Min(duration, rec + 0.15f),
            stateDuration = duration,
            rotateEnd = Mathf.Min(0.35f, duration),
            transitionDuration = 0.1f,
            perilous = perilous,
            hitboxSlot = slot
        };
    }
```

`Slash_SpinElbow` 第二段改为：

```csharp
                new[] { Hit(1.6f), Hit(1.4f, perilous: PerilousType.Grab, slot: AttackHitboxSlot.Elbow) }),
```

- [ ] **步骤 6：asset 里 `Slash_SpinElbow` 的第二段 window（`perilous: 3` 那一段）加 `hitboxSlot: 1`。不要跑 Catalog.Apply，以免覆盖已调时间。**

```yaml
      perilous: 3
      hitboxSlot: 1
```

---

### 任务 2：CharacterBody 切槽

**文件：**
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`

- [ ] **步骤 1：在 `Weapon` 属性附近加引用和 `ActiveHitbox`**

```csharp
    public Hitbox Weapon { get; private set; }
    public Hitbox ActiveHitbox { get; private set; }

    [Header("Hitbox 槽位")]
    [Tooltip("刀。空则 Awake 自动找（会跳过肘/脚引用）")]
    public Hitbox weaponHitbox;
    [Tooltip("肘击采样点。玩家不拖")]
    public Hitbox elbowHitbox;
    [Tooltip("预留踢击。本需求不拖")]
    public Hitbox kickHitbox;
```

删掉原来单独的 `public Hitbox Weapon { get; private set; }` 以免重复。

- [ ] **步骤 2：替换 Awake 里 `GetComponentInChildren<Hitbox>()` 整段为**

```csharp
        InitHitboxes();
```

新增方法（放在 `EnableWeaponHit` 附近）：

```csharp
    void InitHitboxes()
    {
        Weapon = weaponHitbox != null ? weaponHitbox : FindDefaultWeaponHitbox();
        InitHitbox(Weapon);
        InitHitbox(elbowHitbox);
        InitHitbox(kickHitbox);
    }

    void InitHitbox(Hitbox hitbox)
    {
        if (hitbox != null)
            hitbox.Initialize(this);
    }

    Hitbox FindDefaultWeaponHitbox()
    {
        Hitbox[] all = GetComponentsInChildren<Hitbox>(true);
        for (int i = 0; i < all.Length; i++)
        {
            Hitbox h = all[i];
            if (h == null || h == elbowHitbox || h == kickHitbox)
                continue;
            return h;
        }
        return null;
    }

    Hitbox ResolveHitbox(AttackHitboxSlot slot)
    {
        if (slot == AttackHitboxSlot.Elbow)
        {
            if (elbowHitbox != null)
                return elbowHitbox;
            Debug.LogWarning($"{name} 未指定 Elbow Hitbox，回退到刀");
            return Weapon;
        }

        if (slot == AttackHitboxSlot.Kick)
        {
            if (kickHitbox != null)
                return kickHitbox;
            Debug.LogWarning($"{name} 未指定 Kick Hitbox，回退到刀");
            return Weapon;
        }

        return Weapon;
    }
```

- [ ] **步骤 3：替换 Enable / Disable**

```csharp
    public void EnableWeaponHit(AttackConfig config)
    {
        if (config == null) return;
        Hitbox target = ResolveHitbox(config.HitboxSlot);
        if (target == null) return;

        if (ActiveHitbox != null && ActiveHitbox != target)
            ActiveHitbox.Disable();

        target.SetConfig(config);
        target.Enable();
        ActiveHitbox = target;
        CombatEventBus.TriggerAttackSwingStart(this);
    }

    public void DisableWeaponHit()
    {
        Hitbox target = ActiveHitbox != null ? ActiveHitbox : Weapon;
        if (target == null) return;
        target.Disable();
        ActiveHitbox = null;
        CombatEventBus.TriggerAttackSwingEnd(this);
    }
```

`AttackState.ApplyHitbox` 不改。

---

### 任务 3：打铁点 + 时间轴

**文件：**
- 修改：`Assets/Scripts/Combat/CombatFxPoint.cs`
- 修改：`Assets/Editor/AttackTimelineWindow.cs`

- [ ] **步骤 1：`BetweenWeapons` 用角色当前亮着的 Hitbox**

```csharp
    static Hitbox FxHitbox(CharacterBody body)
    {
        if (body == null) return null;
        return body.ActiveHitbox != null ? body.ActiveHitbox : body.Weapon;
    }

    public static Vector3 BetweenWeapons(CharacterBody a, CharacterBody b, Vector3 fallback)
    {
        Hitbox ha = FxHitbox(a);
        Hitbox hb = FxHitbox(b);
        // ...其余保持原样，只是 ha/hb 来源变了
```

- [ ] **步骤 2：`DrawTargetPicker` 在 Boss 段选择之后、`sequences.Length > 1` 提示之前，加槽位下拉。`ApplyPulses` 不要动 `hitboxSlot`。**

```csharp
            BossMoveWindow window = CurrentWindow();
            if (window != null)
            {
                EditorGUI.BeginChangeCheck();
                AttackHitboxSlot slot = (AttackHitboxSlot)EditorGUILayout.EnumPopup(
                    "判定 Hitbox", window.hitboxSlot);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(bossTable, "Attack Timeline Hitbox");
                    window.hitboxSlot = slot;
                    EditorUtility.SetDirty(bossTable);
                }
            }
```

玩家招式不显示此项。

---

### 任务 4：架构文档

**文件：**
- 修改：`Docs/architecture/03-hit-detection.md`
- 修改：`Docs/architecture/03-hit-detection-test.md`

- [ ] **步骤 1：场景接线表增加 Boss 肢体行，并写明 slot + 引用、同时只亮一把、缺槽回退刀。**

- [ ] **步骤 2：验收清单增加 Elbow 条目（规格「验收」表原样搬过去）。**

---

### 任务 5：交接（用户 Unity）

代码完成后列出验收清单。AI 不代挂场景 Hitbox。用户按规格「场景接线」在肘骨下建空物体、挂 `Hitbox`、拖到 `elbowHitbox`，再用时间轴对 Elbow 接触帧。
