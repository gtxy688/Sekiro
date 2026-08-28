# 玩家受击 Light/Mid/Heavy 实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** Boss 打玩家的招式带 Light/Mid/Heavy 等级；玩家按等级播受击/格挡/弹反；连续受击按等级决定是否刷新动画；等级在现有 `ARPG/招式伤害` 窗口编辑。

**架构：** 等级是招式数据（`HitGrade`），随命中进 `HitData`。玩家受击走新规则；Boss 被打仍用 `Knockback > 0` → `Hurt_Ground` / `Hurt_Heavy`。箭走 `ReportProjectileHit` 的 `isProjectile`，用来区分箭 Heavy 的特殊格挡/弹反。

**技术栈：** Unity 2022 LTS、现有 HFSM、`BossMoveDamageWindow`。本项目无自动化测试；用编译 0 error + 验收清单。不要新建 Test Runner。不要擅自 git commit。

**本任务只改这些架构文档：** `01-states.md`、`01-states-test.md`、`03-hit-detection.md`、`03-hit-detection-test.md`

**不要做：** 改 Boss 被打的受击套；把玩家招式改成表；拆 `Hurt_Mid` 为两个 Animator 状态；给 Heavy 标倒地结束点。

---

## 已锁定规则

### 等级只作用于「玩家挨 Boss」

Boss 被玩家打：继续 `knockback > 0` → `Hurt_Heavy`，否则 `Hurt_Ground`。

### 无防御受击

| 等级 | 第一次 | 连续刷新时 |
|------|--------|------------|
| Light | `Hurt_Light` | `Hurt_Light2`（含第三次起，每次重播） |
| Mid | `Hurt_Mid` | 同级/更低不刷新；升 Heavy → `Hurt_HeavyRepeat` |
| Heavy | `Hurt_Heavy` | 更低不刷新；再吃 Heavy → `Hurt_HeavyRepeat`（每次重播） |

连续受击**一定扣血涨架势**。只决定动不动画。

`Hurt_Ground` 在玩家 Animator 里改名为 `Hurt_Light`。代码查找优先 `Hurt_Light`，没有则回退 `Hurt_Ground`。

### 倒地起身

- `Hurt_Mid` / `Hurt_Heavy` / `Hurt_HeavyRepeat` 整段（含躺地）播完 → `Standing` → Idle。
- `Standing` 期间挨刀 = **新的一次受击**（从该等级完整动画起）。
- Heavy **不**标倒地结束点。

### MidToGuard

- 仅 `Hurt_Mid`。
- 必须在 CharacterConfig 的 **倒地结束秒数**（相对 `Hurt_Mid` 动画 0 点）之前按下防御。过了进入躺地，只能播完再 `Standing`。
- `MidToGuard` 播完：按住防御 → 举刀循环；没按住 → Idle。

### 格挡 / 弹反

| 类型 | 等级 | 普通格挡 | 完美弹反 |
|------|------|----------|----------|
| 刀 / 箭 | Light | `Hurt_Guard` | `Deflect_Slash` |
| 刀 / 箭 | Mid | `Hurt_Guard` | `Deflect_HeavySlash` |
| 刀 | Heavy | **穿透**（当没防：扣血 + `Hurt_Heavy`） | `Deflect_HeavySlash` |
| 箭 | Heavy | `Stagger_Broken`（只播动画，不崩架势） | `Deflect_HeavyArrow` |

箭 Heavy 的 `Stagger_Broken` **没有**倒地/躺地。必须播完；播完后按住 → 举刀循环，松开 → Idle。

危字规则不变（横扫连弹反窗口也不吃；其余危字只有窗口内能弹）。

### 招式伤害窗口

`ARPG/招式伤害` 的「重击」勾选改成 **等级枚举** Light / Mid / Heavy。招默认、段覆盖、刀覆盖与血量/架势同一套继承。弓段（·箭）也能改等级。垫步/位移段仍无伤害也无等级。

`knockback` 字段保留（Boss 被打仍用），窗口不再暴露「重击」勾选。

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Scripts/Configs/HitGrade.cs` | `Light / Mid / Heavy` |
| 创建 `Assets/Scripts/Combat/HitReactionUtil.cs` | 玩家格挡/弹反/连续受击动画判定 |
| 创建 `Assets/Scripts/FrameWork/States/Ground/StandingState.cs` | 倒地后起身 |
| 创建 `Assets/Scripts/FrameWork/States/Ground/MidToGuardState.cs` | Mid 倒地起身进防御 |
| 修改 `Assets/Scripts/SO/AttackConfig.cs` | `HitGrade`；`HitPulse.hitGrade` |
| 修改 `Assets/Scripts/Boss/BossMoveEntry.cs` | `hitGrade` 招默认 |
| 修改 `Assets/Scripts/Boss/BossMoveWindow.cs` | `hitGrade` 段覆盖 |
| 修改 `Assets/Scripts/SO/AttackCombatResolve.cs` | 解析顺序含等级 |
| 修改 `Assets/Scripts/Boss/BossAttackBaker.cs` | 烘焙拷贝等级 |
| 修改 `Assets/Scripts/FrameWork/States/Command.cs` | `HitData.hitGrade` / `isProjectile` |
| 修改 `Assets/Scripts/Combat/CombatManager.cs` | 转发等级；箭标 `isProjectile` |
| 修改 `Assets/Scripts/Combat/ArrowProjectile.cs` | Fire/Report 带等级 |
| 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | `ReceiveHit`、玩家用等级、`SpawnArrow` |
| 修改 `Assets/Scripts/FrameWork/States/StunnedState.cs` | 玩家二次受击结算 + MidToGuard 放行 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/GroundStunnedState.cs` | 玩家三级动画、倒地结束点、起身 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/DeflectState.cs` | 新格挡/弹反表 |
| 修改 `Assets/Scripts/SO/CharacterConfig.cs` | 新动画名 + `HurtMidFallEndTime` |
| 修改 `Assets/Editor/BossMoveDamageWindow.cs` | 「重击」→「等级」 |
| 修改 `Assets/Scripts/Boss/GenichiroMoveCatalog.cs` | 默认表带等级 |
| 修改 `Assets/SO/Boss/GenichiroMoveTable.asset` | 按分类表写入各段等级（只改 grade，不改时间） |
| 修改 `Docs/architecture/01-states.md` | 受击/格挡规则 |
| 修改 `Docs/architecture/01-states-test.md` | 验收 |
| 修改 `Docs/architecture/03-hit-detection.md` | 等级随命中传递 |
| 修改 `Docs/architecture/03-hit-detection-test.md` | 招式伤害窗口 + 几条代表招 |

字段名锁定（后续任务禁止改名）：

| 用途 | 名字 |
| --- | --- |
| 枚举 | `HitGrade.Light` / `.Mid` / `.Heavy`（0/1/2） |
| 招默认 | `BossMoveEntry.hitGrade` |
| 段 | `BossMoveWindow.hitGrade`（`overrideCombat` 为 true 才用，否则继承招） |
| 刀 | `HitPulse.hitGrade`（`overrideCombat` 为 true 才用） |
| 烘焙 | `AttackConfig.HitGrade` |
| 命中包 | `HitData.hitGrade`、`HitData.isProjectile` |
| Mid 倒地结束 | `CharacterConfig.HurtMidFallEndTime`（秒，相对 Hurt_Mid 0 点） |

解析顺序与伤害相同：**刀覆盖 → 段覆盖 → 招默认**。

---

### 任务 1：HitGrade 数据 + 招式伤害窗口 + 命中转发

**文件：**
- 创建：`Assets/Scripts/Configs/HitGrade.cs`
- 修改：`Assets/Scripts/SO/AttackConfig.cs`
- 修改：`Assets/Scripts/Boss/BossMoveEntry.cs`
- 修改：`Assets/Scripts/Boss/BossMoveWindow.cs`
- 修改：`Assets/Scripts/SO/AttackCombatResolve.cs`
- 修改：`Assets/Scripts/Boss/BossAttackBaker.cs`
- 修改：`Assets/Scripts/FrameWork/States/Command.cs`
- 修改：`Assets/Scripts/Combat/CombatManager.cs`
- 修改：`Assets/Scripts/Combat/ArrowProjectile.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（`SpawnArrow` / `ReceiveHit` 签名先接字段，动画逻辑任务 2）
- 修改：`Assets/Editor/BossMoveDamageWindow.cs`
- 修改：`Assets/Scripts/Boss/GenichiroMoveCatalog.cs`

- [ ] **步骤 1：新建枚举**

```csharp
public enum HitGrade
{
    Light = 0,
    Mid = 1,
    Heavy = 2
}
```

- [ ] **步骤 2：`HitPulse` 增加字段并写入 `Clone()`**

```csharp
    public HitGrade hitGrade;

    public HitPulse Clone()
    {
        return new HitPulse
        {
            start = start,
            end = end,
            overrideCombat = overrideCombat,
            baseDamage = baseDamage,
            postureDamage = postureDamage,
            knockback = knockback,
            hitGrade = hitGrade
        };
    }
```

- [ ] **步骤 3：`AttackConfig` 在 `Knockback` 后加**

```csharp
    [Tooltip("打到玩家时的受击等级。Boss 被打仍看 Knockback。")]
    public HitGrade HitGrade = HitGrade.Light;
```

- [ ] **步骤 4：`BossMoveEntry` 在 `knockback` 后加 `public HitGrade hitGrade = HitGrade.Light;`**

- [ ] **步骤 5：`BossMoveWindow` 在 `knockback` 后加 `public HitGrade hitGrade;`**

- [ ] **步骤 6：改 `AttackCombatResolve`**

`ApplyWindow` / `Resolve` / `ApplyPulse` 都带上 `HitGrade`。`Resolve` 增加 `out HitGrade grade`。

```csharp
    public static void ApplyWindow(AttackConfig cfg, BossMoveEntry entry, BossMoveWindow w)
    {
        if (cfg == null || entry == null) return;
        if (w != null && w.overrideCombat)
        {
            cfg.BaseDamage = w.baseDamage;
            cfg.PostureDamage = w.postureDamage;
            cfg.Knockback = w.knockback;
            cfg.HitGrade = w.hitGrade;
            return;
        }

        cfg.BaseDamage = entry.baseDamage;
        cfg.PostureDamage = entry.postureDamage;
        cfg.Knockback = entry.knockback;
        cfg.HitGrade = entry.hitGrade;
    }

    public static void Resolve(
        BossMoveEntry entry,
        BossMoveWindow w,
        out int damage,
        out float posture,
        out float knockback,
        out HitGrade grade)
    {
        if (entry == null)
        {
            damage = 0;
            posture = 0f;
            knockback = 0f;
            grade = HitGrade.Light;
            return;
        }

        if (w != null && w.overrideCombat)
        {
            damage = w.baseDamage;
            posture = w.postureDamage;
            knockback = w.knockback;
            grade = w.hitGrade;
            return;
        }

        damage = entry.baseDamage;
        posture = entry.postureDamage;
        knockback = entry.knockback;
        grade = entry.hitGrade;
    }

    public static void ApplyPulse(
        AttackConfig cfg,
        HitPulse pulse,
        int fallbackDamage,
        float fallbackPosture,
        float fallbackKnockback,
        HitGrade fallbackGrade)
    {
        if (cfg == null) return;
        if (pulse != null && pulse.overrideCombat)
        {
            cfg.BaseDamage = pulse.baseDamage;
            cfg.PostureDamage = pulse.postureDamage;
            cfg.Knockback = pulse.knockback;
            cfg.HitGrade = pulse.hitGrade;
            return;
        }

        cfg.BaseDamage = fallbackDamage;
        cfg.PostureDamage = fallbackPosture;
        cfg.Knockback = fallbackKnockback;
        cfg.HitGrade = fallbackGrade;
    }
```

所有旧 `Resolve(...)` 调用点补 `out HitGrade grade`。`AttackState.ApplyPulseCombat` 把当前 `config.HitGrade` 当 fallback 传入（烘焙后已是段等级）。

- [ ] **步骤 7：`HitData` 增加**

```csharp
    public HitGrade hitGrade;
    public bool isProjectile;
```

- [ ] **步骤 8：`ReceiveHit` 签名增加默认参数，写入 HitData。本任务先只填字段，动画仍按 knockback（任务 2 改玩家分支）。**

```csharp
    public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint,
                           bool isPerilous = false, PerilousType perilousType = PerilousType.None,
                           float knockback = 0f,
                           HitGrade hitGrade = HitGrade.Light,
                           bool isProjectile = false)
```

HitData 赋值增加 `hitGrade = hitGrade, isProjectile = isProjectile`。

- [ ] **步骤 9：`CombatManager.ReportHit` 追加 `hitbox.Config.HitGrade, false`。`ReportProjectileHit` 增加 `HitGrade hitGrade` 参数，追加 `hitGrade, true`。**

- [ ] **步骤 10：`ArrowProjectile` 存 `HitGrade grade`，`Fire` 增加参数，`ReportProjectileHit` 传出去。`SpawnArrow` 从 `Resolve` 取出 grade 传入 `Fire`。**

- [ ] **步骤 11：改 `BossMoveDamageWindow`——「重击」列换成「等级」**

常量：删 `HeavyWidth` / `HeavyKnockback`（或留着不用）。新常量 `const float GradeWidth = 72f;`。

表头最后一列：`GUILayout.Label("等级", ..., GUILayout.Width(GradeWidth));`

HelpBox 改成：

```
招那一行是默认伤害和受击等级。勾选段/刀/箭的「覆盖」后才能改那一行；不勾则跟招走。弓段不开刀，但可以单独改箭伤和等级。垫步仍无伤害。等级 Light/Mid/Heavy 决定打到玩家时的受击/格挡/弹反。时间轴只管出伤帧。
```

`DrawEntryCombat`：血量、架势后面改为

```csharp
        HitGrade g = (HitGrade)EditorGUILayout.EnumPopup(entry.hitGrade, GUILayout.Width(GradeWidth));
```

写入 `entry.hitGrade = g`。

`DrawOverrideCombat` 增加 `ref HitGrade grade` 和 `HitGrade inheritGrade`：

- 勾选覆盖时把 `grade = inheritGrade`（与伤害拷贝一起）。
- 数字区后面：`HitGrade showG = ov ? grade : inheritGrade;` 弹出 `EnumPopup`。
- **改等级时若尚未覆盖，自动勾覆盖并拷贝血量/架势/等级**（和旧「重击」勾选行为一样）。

垫步行：禁用的 Toggle 改成禁用的 `EnumPopup(HitGrade.Light)`。

「用段」的只读刀行：只读 `EnumPopup(inheritGrade)`。

所有 `DrawOverrideCombat(...)` 调用补上对应的 `hitGrade` 参数。

- [ ] **步骤 12：`GenichiroMoveCatalog.Move(...)` 增加 `HitGrade grade = HitGrade.Light`，写入 `hitGrade`。`Hit` / `Hits` / `NoHit` 增加可选 `HitGrade grade`，写入 window，并 `overrideCombat = grade != HitGrade.Light` 时不要自动改伤害——等级与伤害覆盖独立。**

段等级若与招默认不同：必须 `w.overrideCombat = true` 并拷贝招的伤害数字，再设 `w.hitGrade`。实现用辅助：

```csharp
    static void SetWindowGrade(BossMoveWindow w, BossMoveEntry entry, HitGrade grade)
    {
        if (w == null || entry == null) return;
        if (grade == entry.hitGrade) return;
        if (!w.overrideCombat)
        {
            w.overrideCombat = true;
            w.baseDamage = entry.baseDamage;
            w.postureDamage = entry.postureDamage;
            w.knockback = entry.knockback;
        }
        w.hitGrade = grade;
    }
```

不要在 `Apply()` 里改判定时间。本任务 Catalog 只加字段默认 Light；具体招的等级在任务 5 写入 asset。

- [ ] **步骤 13：编译。Console 0 error。打开 `ARPG/招式伤害`，招行能改等级，勾覆盖后段/刀/箭能改，不勾显示继承值。**

---

### 任务 2：玩家受击动画 + 连续受击 + Standing

**文件：**
- 创建：`Assets/Scripts/Combat/HitReactionUtil.cs`
- 创建：`Assets/Scripts/FrameWork/States/Ground/StandingState.cs`
- 修改：`Assets/Scripts/SO/CharacterConfig.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/GroundStunnedState.cs`

- [ ] **步骤 1：CharacterConfig 受击动画字段**

`HurtAnim_Normal` 默认值改为 `"Hurt_Light"`。新增：

```csharp
    public string HurtAnim_Light2 = "Hurt_Light2";
    public string HurtAnim_Mid = "Hurt_Mid";
    public string HurtAnim_HeavyRepeat = "Hurt_HeavyRepeat";
    public string HurtAnim_Standing = "Standing";
    public string HurtAnim_MidToGuard = "MidToGuard";

    [Tooltip("相对 Hurt_Mid 动画 0 点，秒。超过则不能 MidToGuard，进入躺地。")]
    public float HurtMidFallEndTime = 0.4f;
```

`HurtAnim_Heavy` 保持 `"Hurt_Heavy"`。`HurtAnim_Broken` 保持 `"Stagger_Broken"`（真崩解和箭 Heavy 格挡共用这段动画）。

- [ ] **步骤 2：`HitReactionUtil`**

```csharp
public static class HitReactionUtil
{
    public static bool IsPlayer(CharacterBody body)
    {
        return CombatManager.Instance != null && body != null && body == CombatManager.Instance.PlayerRef;
    }

    public static string PerfectParryAnim(HitData hit)
    {
        if (hit.isProjectile && hit.hitGrade == HitGrade.Heavy)
            return "Deflect_HeavyArrow";
        if (hit.hitGrade == HitGrade.Light)
            return "Deflect_Slash";
        return "Deflect_HeavySlash";
    }

    // 普通格挡。pierce=true 表示刀 Heavy，调用方应 return false 当没防。
    public static bool IsMeleeHeavyPierce(HitData hit)
    {
        return !hit.isProjectile && hit.hitGrade == HitGrade.Heavy;
    }

    public static bool IsArrowHeavyGuard(HitData hit)
    {
        return hit.isProjectile && hit.hitGrade == HitGrade.Heavy;
    }

    public static string GuardHurtAnim(CharacterBody body)
    {
        return body.ResolveHurtAnim(HurtContext.Guard);
    }

    public static string UnguardedAnim(CharacterBody body, HitGrade grade, bool lightRepeat, bool heavyRepeat)
    {
        if (heavyRepeat)
            return FirstExisting(body, "Hurt_HeavyRepeat", "Hurt_Heavy");
        switch (grade)
        {
            case HitGrade.Mid:
                return FirstExisting(body, "Hurt_Mid", "Hurt_Heavy");
            case HitGrade.Heavy:
                return FirstExisting(body, "Hurt_Heavy", "Hurt_Ground");
            default:
                if (lightRepeat)
                    return FirstExisting(body, "Hurt_Light2", "Hurt_Light", "Hurt_Ground");
                return FirstExisting(body, "Hurt_Light", "Hurt_Ground");
        }
    }

    static string FirstExisting(CharacterBody body, params string[] names)
    {
        if (body != null && body.Config != null)
        {
            if (names[0] == "Hurt_Light2" && !string.IsNullOrEmpty(body.Config.HurtAnim_Light2))
                names[0] = body.Config.HurtAnim_Light2;
            if (names[0] == "Hurt_Mid" && !string.IsNullOrEmpty(body.Config.HurtAnim_Mid))
                names[0] = body.Config.HurtAnim_Mid;
            if (names[0] == "Hurt_HeavyRepeat" && !string.IsNullOrEmpty(body.Config.HurtAnim_HeavyRepeat))
                names[0] = body.Config.HurtAnim_HeavyRepeat;
            if (names[0] == "Hurt_Light" && !string.IsNullOrEmpty(body.Config.HurtAnim_Normal))
                names[0] = body.Config.HurtAnim_Normal;
            if (names[0] == "Hurt_Heavy" && !string.IsNullOrEmpty(body.Config.HurtAnim_Heavy))
                names[0] = body.Config.HurtAnim_Heavy;
        }
        for (int i = 0; i < names.Length; i++)
        {
            if (AnimUtil.HasState(body.Animator, names[i]))
                return names[i];
        }
        return names[names.Length - 1];
    }
}
```

`FirstExisting` 实现时用 Config 字段映射，不要写死两套。原则：Config 非空用 Config 名，再用 `AnimUtil.HasState` 回退。

连续受击是否刷新：

```csharp
    public static bool ShouldRefreshHurt(HitGrade current, HitGrade incoming, out bool heavyRepeat)
    {
        heavyRepeat = false;
        if (current == HitGrade.Light)
            return true;
        if (current == HitGrade.Mid)
        {
            if (incoming == HitGrade.Heavy)
            {
                heavyRepeat = true;
                return true;
            }
            return false;
        }
        // Heavy
        if (incoming == HitGrade.Heavy)
        {
            heavyRepeat = true;
            return true;
        }
        return false;
    }
```

- [ ] **步骤 3：`ReceiveHit` 玩家分支用等级进 `StunnedState`**

在 `TakeDamage` 之后、`alreadyBroken` 之前：

```csharp
        bool player = HitReactionUtil.IsPlayer(this);
        HurtContext ctx = knockback > 0f ? HurtContext.Heavy : HurtContext.Normal;
```

`alreadyBroken` 玩家仍切 `StunnedState(this, HurtContext.Heavy)`（崩解再挨刀保持倒地，现有 14b）。

最后 `ChangeState`：

```csharp
        if (player)
            MainStateMachine.ChangeState(new StunnedState(this, hit.hitGrade));
        else
            MainStateMachine.ChangeState(new StunnedState(this, ctx));
```

Boss 被动防御 `TryPassiveDeflect` 不改（Boss 仍走旧 knockback 格挡）。

- [ ] **步骤 4：`StunnedState` 记住玩家等级**

两个构造：保留 `StunnedState(body, HurtContext)` 给 Boss；新增 `StunnedState(body, HitGrade grade)` 设 `useHitGrade = true`。

```csharp
    private readonly HurtContext context;
    private readonly HitGrade grade;
    private readonly bool useHitGrade;

    public StunnedState(CharacterBody body, HurtContext context = HurtContext.Normal) : base(body)
    {
        this.context = context;
        useHitGrade = false;
    }

    public StunnedState(CharacterBody body, HitGrade grade) : base(body)
    {
        this.grade = grade;
        useHitGrade = true;
        context = grade == HitGrade.Light ? HurtContext.Normal : HurtContext.Heavy;
    }
```

`GetInitialSubState`：`return new GroundStunnedState(body, this, context, useHitGrade ? grade : (HitGrade?)null);`

`OnParentHandleHit`：

```csharp
    protected override bool OnParentHandleHit(HitData hit)
    {
        if (!useHitGrade)
            return true; // Boss 旧行为：拦截且不掉血

        body.TakeDamage(hit.healthDmg, hit.postureDmg);
        if (body.CurrentHP <= 0 || body.IsPostureBroken)
            return true;

        var ground = SubStateMachine.CurrentState as GroundStunnedState;
        if (ground != null)
            ground.ReceiveFollowUpHit(hit);
        return true;
    }
```

`OnParentHandleCommand` 任务 3 再放行 `DeflectCommand`。本任务仍 `return true`。

- [ ] **步骤 5：`GroundStunnedState` 玩家模式**

增加 `HitGrade grade`、`bool lightRepeat`、`bool heavyRepeat`。`OnEnter`：玩家用 `HitReactionUtil.UnguardedAnim`；Mid/Heavy/HeavyRepeat `waitForAnim = true`；Light 也等动画播完（不要 0.5s 掐 Light clip）。

播完后：

- Light → `new GroundedState(body)`（Idle）
- Mid / Heavy / HeavyRepeat → `new GroundedState(body, new StandingState(body, parent))`

`ReceiveFollowUpHit`：

```csharp
    public void ReceiveFollowUpHit(HitData hit)
    {
        bool refresh = HitReactionUtil.ShouldRefreshHurt(grade, hit.hitGrade, out bool toHeavyRepeat);
        if (!refresh) return;

        if (grade == HitGrade.Light)
            lightRepeat = true;
        grade = toHeavyRepeat ? HitGrade.Heavy : hit.hitGrade;
        heavyRepeat = toHeavyRepeat;
        OnEnter(); // 重播。不要 new 整个 StunnedState，避免顶层闪一下。
    }
```

`OnEnter` 会重置 timer，符合「刷新动画」。

- [ ] **步骤 6：`StandingState`**

Grounded 叶子。`OnEnter` 播 `Config.HurtAnim_Standing`（空则 `"Standing"`）。等 `normalizedTime >= 0.99` 或兜底 2s → `IdleState`。`OnHitReceived` **return false**（新的一次受击）。命令全吞（起身不能跑/砍）。

- [ ] **步骤 7：编译。玩家 Animator 若还没有新状态，HasState 回退旧名，不能报红让 Play 崩。**

---

### 任务 3：MidToGuard + 倒地结束时间点

**文件：**
- 创建：`Assets/Scripts/FrameWork/States/Ground/MidToGuardState.cs`
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/GroundStunnedState.cs`

- [ ] **步骤 1：`GroundStunnedState` 暴露窗口**

```csharp
    public bool CanMidToGuard
    {
        get
        {
            if (grade != HitGrade.Mid || heavyRepeat) return false;
            float end = body.Config != null ? body.Config.HurtMidFallEndTime : 0.4f;
            return stunTimer < end;
        }
    }
```

用 `stunTimer`（进状态后的秒），与「相对 Hurt_Mid 动画 0 点」在 CrossFade 很短时等价。不要另读 Animator 时间，避免融合期读到上一clip。

- [ ] **步骤 2：`StunnedState.OnParentHandleCommand`**

```csharp
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        if (useHitGrade && cmd is DeflectCommand)
        {
            var ground = SubStateMachine.CurrentState as GroundStunnedState;
            if (ground != null && ground.CanMidToGuard)
            {
                body.MainStateMachine.ChangeState(
                    new GroundedState(body, new MidToGuardState(body, null)));
                return true;
            }
        }
        return true;
    }
```

`MidToGuardState` 构造里自己拿 Grounded 父引用：按现有 `IdleState` 那样，`GroundedState` 创建子状态时把 `this` 传入。改成：

```csharp
    body.MainStateMachine.ChangeState(new GroundedState(body));
```

不够——会进 Idle。必须 `new GroundedState(body, new MidToGuardState(body, ...))`。`MidToGuardState` 的 parent 在 `GroundedState.OnEnter` 之后才存在。做法与 `FinisherState` 相同：构造只收 `body`，切举刀时 `body.MainStateMachine.CurrentState as GroundedState` 取父。

```csharp
public class MidToGuardState : BaseState
{
    private float timer;
    private bool released;
    private string animName;
    private bool seen;

    public MidToGuardState(CharacterBody body) : base(body) { }

    public override void OnEnter()
    {
        body.IsGuarding = true;
        released = false;
        timer = 0f;
        seen = false;
        animName = body.Config != null && !string.IsNullOrEmpty(body.Config.HurtAnim_MidToGuard)
            ? body.Config.HurtAnim_MidToGuard : "MidToGuard";
        AnimUtil.TryCrossFade(body.Animator, animName, 0.05f);
    }

    public override void OnExit()
    {
        // 进入 DeflectState 会再设 true；进 Idle 必须清掉
        if (!(body.MainStateMachine.CurrentState is GroundedState g
            && g.SubStateMachine.CurrentState is DeflectState))
            body.IsGuarding = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is DeflectCommand) { released = false; return true; }
        if (cmd is IdleCommand) { released = true; return true; }
        return true;
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        var info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, animName))
        {
            seen = true;
            if (info.normalizedTime >= 0.95f)
                Finish();
            return;
        }
        if (seen || timer >= 1.2f)
            Finish();
    }

    void Finish()
    {
        var ground = body.MainStateMachine.CurrentState as GroundedState;
        if (ground == null) return;
        if (released)
            ground.SubStateMachine.ChangeState(new IdleState(body, ground));
        else
            ground.SubStateMachine.ChangeState(new DeflectState(body, ground));
    }

    public override bool OnHitReceived(HitData hit)
    {
        // 起身进防过程已举刀：刀 Heavy 穿透；其余走 DeflectState 同一套（任务 4 抽到 HitReactionUtil 后直接调）
        return false; // 任务 4 改成真正格挡处理。本任务先 false：被打按新受击打断起身。
    }
}
```

任务 4 把 `OnHitReceived` 改成与防御一致。本任务先能切状态即可。

- [ ] **步骤 3：编译。没动画时 `TryCrossFade` 失败则 1.2s 兜底进 Idle/举刀，不要卡住。**

---

### 任务 4：DeflectState 格挡/弹反表 + 箭 Heavy Stagger_Broken

**文件：**
- 修改：`Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/MidToGuardState.cs`

- [ ] **步骤 1：`HandlePerfectParry` 里选动画改为 `HitReactionUtil.PerfectParryAnim(hit)`。缺状态 `LogError`，不要静默 Deflect_Slash。**

玩家才走新表。Boss 被动完美弹反仍可走同一张表（Boss 没有 `Deflect_HeavyArrow` 则 `HasState` 失败会 LogError）。Boss 被弹动画是攻击者侧，不受影响。

若 `!HitReactionUtil.IsPlayer(body)`：完美弹反继续 `hit.knockback > 0f ? Deflect_HeavySlash : Deflect_Slash`（Boss 旧逻辑）。

- [ ] **步骤 2：`HandleGuardHit` 开头，仅玩家：**

```csharp
        if (HitReactionUtil.IsPlayer(body))
        {
            if (HitReactionUtil.IsMeleeHeavyPierce(hit))
                return false;
            if (HitReactionUtil.IsArrowHeavyGuard(hit))
                return PlayArrowHeavyGuard(hit);
            // Light/Mid 刀和箭：Hurt_Guard
            ...现有架势结算...
            guardHurtAnim = HitReactionUtil.GuardHurtAnim(body);
            ...
            return true;
        }
```

Boss 格挡保持 `knockback > 0 ? GuardHeavy : Guard`。

- [ ] **步骤 3：`PlayArrowHeavyGuard`**

架势按普通格挡系数涨；打满崩解则 return true（真崩解优先）。否则：

- `guardHurtAnim = body.Config.HurtAnim_Broken`（空则 `Stagger_Broken`）
- `waitingGuardHurt = true`（复用 UpdateGuardHurt）
- **不要**在这段动画中 `StartCancel`：`HandleCommand` 的 `IdleCommand` 只记 `hasReleased = true`，等动画结束再走。
- `UpdateGuardHurt` 结束时：`hasReleased` → `IdleState`；否则 `PlayGuardLoop(force: true)`。

`HandleCommand` 里若 `waitingGuardHurt && guardHurtAnim == Stagger_Broken`：`IdleCommand` 不要 `StartCancel`。

- [ ] **步骤 4：`MidToGuardState.OnHitReceived` 改为创建临时逻辑太重。改为切 `DeflectState(..., pendingHit: hit, mode: Normal)` 让 Deflect 处理这一击。窗口从进 Deflect 起算。可接受：起身防的弹反窗口在 MidToGuard 结束才开始。被打发生在 MidToGuard 期间：直接 `return false` 当没防打断（起身动作还没防好）。保持任务 3 的 `return false`。**

不要在 MidToGuard 里复制一整份 Deflect。被打打断起身是可验收行为。

- [ ] **步骤 5：编译。**

---

### 任务 5：按分类表写入招式等级

**文件：**
- 修改：`Assets/SO/Boss/GenichiroMoveTable.asset`（只改 `hitGrade` / 为改等级而开的 `overrideCombat` + 拷贝的伤害，**禁止改** hitStart/recover/arrowCues）
- 修改：`Assets/Scripts/Boss/GenichiroMoveCatalog.cs`（`Apply` 里同步默认值，避免以后点「填入默认表」丢等级）

写入规则：段等级 ≠ 招默认 → 该段 `overrideCombat = true`，伤害数字拷贝招默认（若该段已经 override 伤害则不动数字），只改 `hitGrade`。

招默认尽量设成该招最常见的等级，减少 override 数量。

| 招 id | 招默认 | 段（按 windows 下标） |
|-------|--------|------------------------|
| Bow_ThenSlash | Light | [0] Bow 箭 Mid；[1] Slash Light |
| Bow_Shot | Mid | [0] Mid |
| Bow_AirHeavy | Heavy | [0] Heavy |
| Bow_Heavy | Heavy | [0] Heavy |
| Slash_Rush2 | Light | 全 Light |
| Slash_RushThenBow | Light | [0] Slash Light；[1] Bow 箭 Mid |
| Slash_Double | Light | 全 Light |
| Slash_Heavy | Mid | 全 Mid |
| Slash_SpinElbow | Mid | [0] Slash Mid；[1] Elbow Light（特殊动画未提供，按 Light） |
| Slash_StepTurn | Light | Slash Light |
| Kick | Light | [0] Attack_Slash Light；[1] Kick Heavy |
| JumpThrust | Heavy | Heavy |
| Kengeki_Bow | Light | 表写 Mid,Light,Light → windows[0] Mid，其余 Light。若段是 NoHit 垫步则不改 |
| Kengeki_Double | Light | 两刀都 Light |
| Kengeki_Slash | Light | Light |
| Kengeki_Thrust | Heavy | Heavy |
| Perilous_Sweep | Light | Light |
| Kengeki_Heavy | Mid | Step_* 垫步不改；Kengeki_Heavy 段 Mid |
| Boat / Boat_Full | Light | 有判定的段：前面 Light，**最后一段 Mid**。Boat_Full 单段多刀：前刀 Light，最后一刀 Mid（pulse override） |
| Bow_Air5 / Kengeki_Air5 | Light | Dodge 不改；Bow_Air5 箭：表写前面 Light 最后 Mid。五连射同一段则 **pulse/出箭无法逐发标等级时：整段 Mid**（与「最后一招 Mid」折中：五支出同一 Clip，段级标 Mid；若只有一段 NoHit 箭，hitGrade=Mid） |
| Kengeki_JumpBow | Light | Mid,Light,Light 按 window 下标 |
| Kengeki_Bow2Slash | Light | 全 Light |

弓段是 `NoHit` + `arrowCues`：等级写在 **该 window.hitGrade**，Resolve 箭时能读到。必须 `overrideCombat`（若与招默认不同）否则继承招默认。因此 `Bow_ThenSlash` 招默认 Light 时，Bow 段必须 override 为 Mid。

- [ ] **步骤 1：用 Unity 序列化或 `manage_scriptable_object` / 直接改 asset YAML 的 `hitGrade:` 字段。禁止整表替换。**

YAML 枚举：`hitGrade: 0` Light，`1` Mid，`2` Heavy。

- [ ] **步骤 2：Catalog `Apply()` 里同样 SetWindowGrade，避免以后重建默认表丢数据。仍不要改时间数字。**

- [ ] **步骤 3：打开 `ARPG/招式伤害` 人工核对：Bow_Shot=Mid，Bow_Heavy=Heavy，Slash_Heavy=Mid，Kick 第二段=Heavy，JumpThrust=Heavy。**

---

### 任务 6：架构文档 + 验收清单

**文件：**
- 修改：`Docs/architecture/01-states.md`
- 修改：`Docs/architecture/01-states-test.md`
- 修改：`Docs/architecture/03-hit-detection.md`
- 修改：`Docs/architecture/03-hit-detection-test.md`

- [ ] **步骤 1：`01-states.md`**

替换「未格挡受击：按 knockback 选择 Hurt_Ground / Hurt_Heavy」为玩家三级表 + 连续受击规则 + Standing + MidToGuard + 格挡/弹反表。写明 Boss 被打仍 knockback。写明二次受击**扣血**（废止旧「拦截不掉血」仅对 Boss 有效）。

`ReceiveHit` 伪代码补 `hitGrade` / `isProjectile`。

涉及文件列表加上本计划新建/修改的脚本。

- [ ] **步骤 2：`01-states-test.md`**

Animator 表：`Hurt_Ground` → `Hurt_Light`；增加 `Hurt_Light2`、`Hurt_Mid`、`Hurt_HeavyRepeat`、`Standing`、`MidToGuard`、`Deflect_HeavyArrow`。

改第 2 条：未防御 Light 播 `Hurt_Light`。

改第 3 条：**二次受击掉血涨架势**；动画是否刷新看等级（Light 重播 Light2；Mid 同级不刷新；Heavy 同级播 Repeat）。

新增验收：

| # | 操作 | 预期 |
|---|------|------|
| 3a | Hurt_Light 中再吃 Light | 掉血，播 `Hurt_Light2`；再吃仍重播 `Hurt_Light2` |
| 3b | Hurt_Mid 中再吃 Light/Mid | 掉血，动画不刷新；吃 Heavy 播 `Hurt_HeavyRepeat` |
| 3c | Hurt_Heavy 中再吃 Light/Mid | 掉血，不刷新；再吃 Heavy 播 `Hurt_HeavyRepeat` |
| 3d | Hurt_Mid / Hurt_Heavy 播完 | 播 `Standing` 再回 Idle；Standing 中挨刀按新一击完整播 |
| 3e | Hurt_Mid 倒地结束前按防御 | `MidToGuard`；按住进举刀，松开回 Idle |
| 3f | Hurt_Mid 躺地后再按防御 | 不进 MidToGuard |
| 6a | 格挡 Light/Mid（刀或箭） | `Hurt_Guard` |
| 6b | 格挡刀 Heavy | 防不住，等同没防，`Hurt_Heavy` |
| 6c | 格挡箭 Heavy（Bow_Heavy） | `Stagger_Broken`，架势条不因这段动画而崩（伤害仍按格挡涨架势）；播完按住继续举刀 |
| 7a | 弹反 Light | `Deflect_Slash` |
| 7b | 弹反 Mid 或刀 Heavy | `Deflect_HeavySlash` |
| 7c | 弹反箭 Heavy | `Deflect_HeavyArrow` |

旧第 6/7 条（Knockback 分轻重视）改为指向 6a–7c。

- [ ] **步骤 3：`03-hit-detection.md`**

「段/刀伤害」段：`hitGrade` 与伤害同一套覆盖。`ARPG/招式伤害` 用等级枚举，不再勾重击。`ReportHit` / `ReportProjectileHit` 把 `HitGrade` 和 `isProjectile` 写入 `HitData`。玩家受击读等级；Boss 受击仍读 knockback。

- [ ] **步骤 4：`03-hit-detection-test.md` 增加**

| # | 操作 | 预期 |
|---|------|------|
| G1 | 打开 ARPG/招式伤害 | 每招有等级下拉；段勾覆盖后能改；不勾显示招默认 |
| G2 | Bow_Shot 无覆盖应为 Mid | 玩家裸吃 `Hurt_Mid` |
| G3 | 改 Bow_Shot 为 Light 存盘再打 | 裸吃变 `Hurt_Light`（改完测完改回 Mid） |

- [ ] **步骤 5：对照本计划「已锁定规则」每一条，文档里都能找到对应句。没有占位「待定」。**

---

## 用户需在 Unity 里准备的动画状态（代码按短名 CrossFade）

玩家 Animator（Boss 不必加）：

`Hurt_Light`（由 Hurt_Ground 改名）、`Hurt_Light2`、`Hurt_Mid`、`Hurt_Heavy`、`Hurt_HeavyRepeat`、`Standing`、`MidToGuard`、`Deflect_HeavyArrow`

真崩解仍用 `Stagger_Broken`。箭 Heavy 普通格挡也播这个状态名。

`HurtMidFallEndTime` 在玩家 CharacterConfig Inspector 里调，对着 `Hurt_Mid` clip 的倒地结束帧换算成秒。

---

## 自检

| 规格 | 任务 |
|------|------|
| Light/Mid/Heavy 无防御动画 | 2 |
| 连续受击刷新规则 | 2 |
| Standing | 2 |
| MidToGuard + 可编辑倒地结束点 | 3 + CharacterConfig |
| 格挡/弹反表 + 刀 Heavy 穿透 + 箭 Heavy Stagger | 4 |
| 等级可在招式伤害编辑 | 1 |
| 分类表写入各招 | 5 |
| 只玩家、Boss 旧套 | 2 ReceiveHit 分支 |
| 文档/验收 | 6 |
| Elbow 特殊动画 | 5 按 Light，无新 Clip 名 |

无「待定」、无「类似任务 N」。`HitGrade` / `overrideCombat` 继承规则任务 1 与任务 5 一致。
