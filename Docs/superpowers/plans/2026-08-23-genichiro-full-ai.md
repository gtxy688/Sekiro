# 弦一郎完整 AI（M7）实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法跟踪进度。

**目标：** 用一份 `BossMoveTable` 加权抽招，替换简单树里「冷却好了就 `BT_HitOnce`」，接上主动四档、交锋还击、招架、喝药重箭、弓/五连/飞舟；不改受击、崩解、忍杀结算。

**架构：** 行为树保持薄 Selector。选招是纯函数 `BossMovePicker`。出招节点把表行烤成运行时 `AttackConfig`，写入 `CharacterBody.ActiveAttack` 再 `TryExecuteCommand(new AttackCommand())`。多段招在同一节点内顺序进 `AttackState`。交锋：`ForceParryStun` 置 `KengekiArmed`，等 `ParriedState` 结束（`IsParried == false`）再抽招。正在播的招由叶子自己记住。Selector 只对实现 `ISelectorLock` 的 Running 子节点续跑；**禁止**记住 `BT_MoveToTarget` 的 Running，否则追击时招架永远抢不到。

**技术栈：** Unity 2022.3、C#、HFSM、行为树、ScriptableObject。本项目无自动化测试；每个任务用 Unity 编译 0 error + 手测验证。不要新建 Test Runner 程序集。不要擅自 git commit（仅当用户本会话明确要求时才提交）。

**规格：** `Docs/superpowers/specs/2026-08-23-genichiro-full-ai-design.md`  
**资源表：** `Docs/architecture/04-boss-ai-move-assets.md`  
**本任务只改这两份架构文档：** `Docs/architecture/04-behavior-tree-ai.md`、`Docs/architecture/04-behavior-tree-ai-test.md`

**现有 API（必须按此写，不要发明别名）：**

| 用途 | 名字 |
| --- | --- |
| 行为树 | `Evaluate()` → `NodeState` |
| 黑板冷却 | `IsOnCooldown(key, duration)` / `SetCooldown(key)` |
| 出招 | `body.ActiveAttack = cfg;` `body.TryExecuteCommand(new AttackCommand())` |
| 招架命令 | `new DeflectCommand()` / 松手 `new IdleCommand()` |
| 被弹硬直 | `ForceParryStun()`；崩解 `ForcePostureBroken()` |
| 关刀 | `DisableWeaponHit()` |
| 动画短名 | `AnimUtil.HasState(animator, shortName)` |
| 危字枚举 | `PerilousType`（`None` / `Thrust` / `Sweep` / `Grab`） |
| 攻击配置字段 | `AnimName`, `TransitionDuration`, `BaseDamage`, `PostureDamage`, `Knockback`, `Perilous`, `HitStartTime`, `RecoveryWindowStart`, `ComboWindowEnd`, `StateDuration`, `AllowRotation`, `RotationSpeed`, `RotationWindowEnd`, `NextCombo` |
| 角色 | `LightAttack`, `IsAttacking`, `IsHealing`, `IsPostureBroken`, `CurrentHP`, `CurrentPosture`, `Config.MaxHP`, `Config.MaxPosture`, `Config.ParriedDuration`, `CombatTarget`, `Animator` |
| 现有叶子 | `BT_Deflect`, `BT_HitOnce`（树里不再挂）, `BT_MoveToTarget` |
| 条件节点 | `ConditionNode` |
| Boss 脑 | `BTBrain`：`PlayerTarget`, `PlayerBody`, `deflectRange`, `deflectCooldown`, `attackRange` |

`AttackState.ValidateWindows` 要求：`0 <= HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration`，且 `RotationWindowEnd` 在 `StateDuration` 内。无判定段把这四者都设成 `stateDuration`（先走 recover 分支，不会开刀）。

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Scripts/Boss/BossMoveLayer.cs` | `Active` / `Kengeki` / `Interrupt` |
| 创建 `Assets/Scripts/Boss/BossMoveExtra.cs` | `None` / `HpBelow75` / `PostureLow` |
| 创建 `Assets/Scripts/Boss/BossMoveWindow.cs` | 单段窗口，烤进 `AttackConfig` |
| 创建 `Assets/Scripts/Boss/BossAnimSequence.cs` | 一套顺序 Animator 短名 |
| 创建 `Assets/Scripts/Boss/BossMoveEntry.cs` | 表的一行 |
| 创建 `Assets/Scripts/Boss/BossMoveTable.cs` | SO + `FindById` |
| 创建 `Assets/Scripts/Boss/BossMovePicker.cs` | 加权抽取 |
| 创建 `Assets/Scripts/Boss/BossAttackBaker.cs` | 行+段 → 运行时 `AttackConfig` |
| 创建 `Assets/Scripts/Boss/GenichiroMoveCatalog.cs` | 弦一郎默认行（Editor 按钮调用） |
| 创建 `Assets/Scripts/Boss/BehaviourTree/ISelectorLock.cs` | 标记「招/招架/交锋，Selector 可续跑」 |
| 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_ExecuteMove.cs` | 播一招（多段、五连→重箭） |
| 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_Kengeki.cs` | 等被弹硬直结束 → 抽交锋招 |
| 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_DeflectIf.cs` | 条件招架，自身是 Selector 直接子节点 |
| 创建 `Assets/Editor/BossMoveTableEditor.cs` | 填默认表 / 建 asset |
| 修改 `Assets/Scripts/Boss/BTBrain.cs` | 换树；引用 `moveTable` |
| 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | `IsParried`, `KengekiArmed`；`ForceParryStun` 置位 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/ParriedState.cs` | 进出维护 `IsParried` |
| 修改 `Assets/Scripts/Boss/BehaviourTree/Selector.cs` | Running 时写入索引，且仅当子节点是 `ISelectorLock` |
| 修改 `Assets/Scripts/Boss/BehaviourTree/BT/BT_Deflect.cs` | 公开 `IsActive`；冷却调用 `SetCooldown` |
| 修改两份 `04-behavior-tree-ai*.md` | 完整薄树 + 手测清单 |
| 创建 `Assets/SO/Boss/GenichiroMoveTable.asset` | Editor 菜单生成，不要手写 YAML |

不改：`AttackState`、`CombatManager`、`Hitbox`、玩家 `AttackConfig` 链。`BT_Combo` / `BT_HitOnce` 文件可留，树里不挂。

不要做：二阶段雷、左右弹分表、普攻 1-2-3-4、投射物箭。

---

### 任务 1：招式表数据类型

**文件：** 创建 Layer / Extra / Window / Sequence / Entry / Table（可合并进 `BossMoveTable.cs`，但 Entry 必须 `[Serializable]`）

- [ ] **步骤 1：按下列名字实现（后续任务禁止改名）**

```csharp
using System;
using UnityEngine;

public enum BossMoveLayer
{
    Active = 0,
    Kengeki = 1,
    Interrupt = 2
}

public enum BossMoveExtra
{
    None = 0,
    HpBelow75 = 1,
    PostureLow = 2
}

[Serializable]
public class BossMoveWindow
{
    public float hitStartTime = 0.2f;
    public float recoverStart = 0.8f;
    public float comboWindowEnd = 0.9f;
    public float stateDuration = 1.8f;
    public float rotateEnd = 0.3f;
    public float transitionDuration = 0.1f;
}

[Serializable]
public class BossAnimSequence
{
    public string[] states;
}

[Serializable]
public class BossMoveEntry
{
    public string id;
    public BossMoveLayer layer;
    public BossAnimSequence[] sequences;
    public BossMoveWindow[] windows;
    public int baseDamage = 10;
    public float postureDamage = 15f;
    public float knockback = 0f;
    public PerilousType perilous = PerilousType.None;
    public float minRange = 0f;
    public float maxRange = 99f;
    public float weight = 10f;
    public float cooldown = 4f;
    public BossMoveExtra extra = BossMoveExtra.None;
}

[CreateAssetMenu(fileName = "BossMoveTable", menuName = "Combat/Boss Move Table")]
public class BossMoveTable : ScriptableObject
{
    public float kengekiMaxRange = 2.5f;

    [Tooltip("<=0 则用 Config.MaxPosture * 0.5（本项目 MaxPosture 默认 100，不能照抄原作 360）")]
    public float postureLowThreshold = 0f;

    [Range(0f, 1f)]
    public float air5HeavyInterruptChance = 0.5f;

    public BossMoveEntry[] moves;

    public BossMoveEntry FindById(string id)
    {
        if (moves == null || string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < moves.Length; i++)
        {
            if (moves[i] != null && moves[i].id == id)
                return moves[i];
        }
        return null;
    }
}
```

无判定段：`hitStartTime == recoverStart == comboWindowEnd == stateDuration`。

- [ ] **步骤 2：Unity 编译，Console 0 error。CreateAssetMenu 出现 Combat/Boss Move Table。**

- [ ] **步骤 3：不要 commit。**

---

### 任务 2：抽招器 + AttackConfig 烘焙

**文件：**
- 创建 `Assets/Scripts/Boss/BossMovePicker.cs`
- 创建 `Assets/Scripts/Boss/BossAttackBaker.cs`

- [ ] **步骤 1：实现 `BossMovePicker`**

```csharp
using UnityEngine;

public static class BossMovePicker
{
    public static BossMoveEntry Pick(
        BossMoveTable table,
        BossMoveLayer layer,
        CharacterBody self,
        Animator animator,
        Blackboard blackboard,
        float distance)
    {
        if (table == null || table.moves == null || self == null) return null;

        float postureLow = table.postureLowThreshold > 0f
            ? table.postureLowThreshold
            : (self.Config != null ? self.Config.MaxPosture * 0.5f : 50f);

        float total = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            float cursor = 0f;
            float roll = pass == 1 ? Random.Range(0f, total) : 0f;
            for (int i = 0; i < table.moves.Length; i++)
            {
                float w = Weight(table.moves[i], table, layer, self, animator, blackboard, distance, postureLow);
                if (w <= 0f) continue;
                if (pass == 0) total += w;
                else
                {
                    cursor += w;
                    if (roll <= cursor) return table.moves[i];
                }
            }
            if (pass == 0 && total <= 0f) return null;
        }
        return null;
    }

    public static float Weight(
        BossMoveEntry e,
        BossMoveTable table,
        BossMoveLayer layer,
        CharacterBody self,
        Animator animator,
        Blackboard blackboard,
        float distance,
        float postureLow)
    {
        if (e == null || e.layer != layer || e.weight <= 0f) return 0f;
        if (layer == BossMoveLayer.Active)
        {
            if (distance < e.minRange || distance > e.maxRange) return 0f;
        }
        if (blackboard != null && blackboard.IsOnCooldown(e.id, e.cooldown)) return 0f;
        if (!AnySequencePlayable(e, animator)) return 0f;

        switch (e.extra)
        {
            case BossMoveExtra.HpBelow75:
                if (self.Config == null || self.CurrentHP >= self.Config.MaxHP * 0.75f)
                    return 0f;
                break;
            case BossMoveExtra.PostureLow:
                if (self.CurrentPosture > postureLow) return 0f;
                break;
        }
        return e.weight;
    }

    public static bool AnySequencePlayable(BossMoveEntry e, Animator animator)
    {
        if (e == null || e.sequences == null) return false;
        for (int i = 0; i < e.sequences.Length; i++)
        {
            if (SequencePlayable(e.sequences[i], animator)) return true;
        }
        return false;
    }

    public static bool SequencePlayable(BossAnimSequence seq, Animator animator)
    {
        if (seq == null || seq.states == null || seq.states.Length == 0) return false;
        for (int i = 0; i < seq.states.Length; i++)
        {
            if (string.IsNullOrEmpty(seq.states[i])) return false;
            if (animator != null && !AnimUtil.HasState(animator, seq.states[i])) return false;
        }
        return true;
    }

    public static BossAnimSequence ChooseSequence(BossMoveEntry e, Animator animator)
    {
        if (e == null || e.sequences == null) return null;
        int playable = 0;
        for (int i = 0; i < e.sequences.Length; i++)
        {
            if (SequencePlayable(e.sequences[i], animator)) playable++;
        }
        if (playable <= 0) return null;
        int pick = Random.Range(0, playable);
        for (int i = 0; i < e.sequences.Length; i++)
        {
            if (!SequencePlayable(e.sequences[i], animator)) continue;
            if (pick == 0) return e.sequences[i];
            pick--;
        }
        return null;
    }

    public static BossMoveWindow WindowFor(BossMoveEntry e, int segmentIndex)
    {
        if (e == null || e.windows == null || e.windows.Length == 0)
            return new BossMoveWindow();
        int i = Mathf.Clamp(segmentIndex, 0, e.windows.Length - 1);
        return e.windows[i];
    }
}
```

交锋层 `Pick(..., BossMoveLayer.Kengeki, ...)` **不要**用 `minRange`/`maxRange` 过滤（距离由 `BT_Kengeki` 用 `kengekiMaxRange` 判断）。

- [ ] **步骤 2：实现 `BossAttackBaker.Bake`**

```csharp
using UnityEngine;

public static class BossAttackBaker
{
    public static AttackConfig Bake(BossMoveEntry entry, string animName, BossMoveWindow w)
    {
        AttackConfig cfg = ScriptableObject.CreateInstance<AttackConfig>();
        cfg.hideFlags = HideFlags.HideAndDontSave;
        cfg.AnimName = animName;
        cfg.TransitionDuration = w.transitionDuration;
        cfg.BaseDamage = entry.baseDamage;
        cfg.PostureDamage = entry.postureDamage;
        cfg.Knockback = entry.knockback;
        bool canHit = w.hitStartTime < w.stateDuration;
        cfg.Perilous = canHit ? entry.perilous : PerilousType.None;
        cfg.HitStartTime = w.hitStartTime;
        cfg.RecoveryWindowStart = w.recoverStart;
        cfg.ComboWindowEnd = w.comboWindowEnd;
        cfg.StateDuration = w.stateDuration;
        cfg.AllowRotation = true;
        cfg.RotationSpeed = 720f;
        cfg.RotationWindowEnd = w.rotateEnd;
        cfg.NextCombo = null;
        return cfg;
    }
}
```

- [ ] **步骤 3：编译 0 error。** 用假距离 `8` 时，只有 `maxRange >= 8` 的 Active 行权重大于 0。缺 Animator 状态的行权重必须为 0。测完删临时 Debug。

- [ ] **步骤 4：不要 commit。**

---

### 任务 3：被弹硬直 → 交锋武装

**文件：**
- 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs`
- 修改 `Assets/Scripts/FrameWork/States/Ground/ParriedState.cs`

- [ ] **步骤 1：在 `IsHealing` 旁加标志**

```csharp
public bool IsParried { get; set; }
public bool KengekiArmed { get; set; }
```

- [ ] **步骤 2：改 `ForceParryStun`**

```csharp
public void ForceParryStun()
{
    EnsureRuntimeReady();
    IsAttacking = false;
    DisableWeaponHit();
    KengekiArmed = true;
    MainStateMachine.ChangeState(new GroundedState(this, new ParriedState(this)));
}
```

`ForcePostureBroken` 开头加 `KengekiArmed = false;`（崩解没有交锋还击）。

- [ ] **步骤 3：`ParriedState`**

`OnEnter` 里：`body.IsParried = true;`

新增：

```csharp
public override void OnExit()
{
    body.IsParried = false;
}
```

现有回 `GroundedState` 的 `ChangeState` 会调 `OnExit`。硬直期间 `HandleCommand` 仍吞掉所有命令，所以交锋出招必须等 `IsParried == false`。

- [ ] **步骤 4：编译。Play：完美弹刀 Boss，硬直期间 `IsParried` 为真、结束后为假；`KengekiArmed` 在硬直开始即为真。不要 commit。**

---

### 任务 4：`BT_ExecuteMove`

**文件：**
- 创建 `Assets/Scripts/Boss/BehaviourTree/ISelectorLock.cs`
- 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_ExecuteMove.cs`

- [ ] **步骤 1：锁接口**

```csharp
public interface ISelectorLock { }
```

- [ ] **步骤 2：出招节点**

```csharp
using UnityEngine;

public class BT_ExecuteMove : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;

    private BossMoveEntry entry;
    private BossAnimSequence sequence;
    private int segment;
    private bool started;
    private bool waitingAttack;

    public bool IsBusy => started;

    public BT_ExecuteMove(CharacterBody body, BossMoveTable table)
    {
        this.body = body;
        this.table = table;
    }

    public void ResetMove()
    {
        started = false;
        waitingAttack = false;
        entry = null;
        sequence = null;
        segment = 0;
    }

    public NodeState Begin(BossMoveEntry move)
    {
        ResetMove();
        if (move == null) return NodeState.Failure;
        sequence = BossMovePicker.ChooseSequence(move, body.Animator);
        if (sequence == null) return NodeState.Failure;
        entry = move;
        started = true;
        segment = 0;
        return FireCurrentSegment();
    }

    public override NodeState Evaluate()
    {
        if (!started) return NodeState.Failure;

        if (body.IsParried || body.IsPostureBroken)
        {
            ResetMove();
            return NodeState.Failure;
        }

        if (waitingAttack)
        {
            if (body.IsAttacking) return NodeState.Running;
            waitingAttack = false;
            segment++;
            TryInterruptAir5();
            if (sequence == null || segment >= sequence.states.Length)
            {
                blackboard?.SetCooldown(entry.id);
                ResetMove();
                return NodeState.Success;
            }
            return FireCurrentSegment();
        }

        return FireCurrentSegment();
    }

    private void TryInterruptAir5()
    {
        if (entry == null) return;
        if (entry.id != "Bow_Air5" && entry.id != "Kengeki_Air5") return;
        if (segment <= 0) return;
        float chance = table != null ? table.air5HeavyInterruptChance : 0f;
        if (Random.value > chance) return;
        BossMoveEntry heavy = table != null ? table.FindById("Bow_Heavy") : null;
        if (heavy == null) return;
        if (blackboard != null && blackboard.IsOnCooldown(heavy.id, heavy.cooldown)) return;
        if (!BossMovePicker.AnySequencePlayable(heavy, body.Animator)) return;

        blackboard?.SetCooldown(entry.id);
        entry = heavy;
        sequence = BossMovePicker.ChooseSequence(heavy, body.Animator);
        segment = 0;
    }

    private NodeState FireCurrentSegment()
    {
        string anim = sequence.states[segment];
        BossMoveWindow w = BossMovePicker.WindowFor(entry, segment);
        body.ActiveAttack = BossAttackBaker.Bake(entry, anim, w);
        if (!body.TryExecuteCommand(new AttackCommand()))
        {
            ResetMove();
            return NodeState.Failure;
        }
        waitingAttack = true;
        return NodeState.Running;
    }
}
```

不要改 `AttackState`：它 `OnEnter` 仍会清掉 `ActiveAttack`。

- [ ] **步骤 3：编译 0 error。不要 commit。**

---

### 任务 5：交锋节点 + Selector + 换树

**文件：**
- 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_Kengeki.cs`
- 创建 `Assets/Scripts/Boss/BehaviourTree/BT/BT_DeflectIf.cs`
- 修改 `Selector.cs`
- 修改 `BT_Deflect.cs`
- 修改 `BTBrain.cs`

- [ ] **步骤 1：`BT_Kengeki`**

```csharp
using UnityEngine;

public class BT_Kengeki : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;

    public BT_Kengeki(CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor)
    {
        this.body = body;
        this.table = table;
        this.target = target;
        this.executor = executor;
    }

    public override NodeState Evaluate()
    {
        if (executor != null && executor.IsBusy)
            return executor.Evaluate();

        if (body.IsPostureBroken) return NodeState.Failure;
        if (!body.KengekiArmed && !body.IsParried) return NodeState.Failure;
        if (body.IsParried) return NodeState.Running;

        float dist = Vector3.Distance(body.transform.position, target.position);
        if (dist > table.kengekiMaxRange)
        {
            body.KengekiArmed = false;
            return NodeState.Failure;
        }

        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Kengeki, body, body.Animator, blackboard, dist);
        body.KengekiArmed = false;
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }
}
```

- [ ] **步骤 2：Selector 只锁 `ISelectorLock`**

把 `case NodeState.Running` 改成写入 `runningChildIndex`，且仅当 `children[i] is ISelectorLock`。`BT_MoveToTarget` 不要实现该接口。

```csharp
public override NodeState Evaluate()
{
    if (runningChildIndex >= 0)
    {
        NodeState resume = children[runningChildIndex].Evaluate();
        if (resume == NodeState.Running)
        {
            state = NodeState.Running;
            return state;
        }
        runningChildIndex = -1;
    }

    for (int i = 0; i < children.Count; i++)
    {
        switch (children[i].Evaluate())
        {
            case NodeState.Failure:
                continue;
            case NodeState.Success:
                runningChildIndex = -1;
                state = NodeState.Success;
                return state;
            case NodeState.Running:
                runningChildIndex = children[i] is ISelectorLock ? i : -1;
                state = NodeState.Running;
                return state;
        }
    }

    runningChildIndex = -1;
    state = NodeState.Failure;
    return state;
}
```

禁止改成「任意 Running 都记住」。

- [ ] **步骤 3：`BT_Deflect` 加 `public bool IsActive => active;`。** 冷却必须走 `blackboard.SetCooldown("deflect")`（与 `Blackboard.cs` 一致；若文件里写成了不存在的方法名，一并改掉）。

- [ ] **步骤 4：`BT_DeflectIf`（Selector 直接子节点，避免 Sequence 丢掉招架 Running）**

```csharp
public class BT_DeflectIf : Node, ISelectorLock
{
    private readonly BT_Deflect inner;
    private readonly System.Func<bool> canStart;

    public BT_DeflectIf(CharacterBody body, System.Func<bool> canStart)
    {
        inner = new BT_Deflect(body);
        children.Add(inner);
        this.canStart = canStart;
    }

    public override NodeState Evaluate()
    {
        if (inner.IsActive) return inner.Evaluate();
        if (!canStart()) return NodeState.Failure;
        return inner.Evaluate();
    }
}
```

把 `inner` 放进 `children`，这样 `SetBlackboard` 会传到 `BT_Deflect`。

- [ ] **步骤 5：重写 `BTBrain.ConstructBehaviorTree`**

新增：

```csharp
[Header("完整 AI")]
public BossMoveTable moveTable;
```

`Start`：`moveTable == null` 则 `Debug.LogError` 并 `enabled = false`。保留 `LightAttack == null` 检查。

```csharp
private BT_ExecuteMove activeExecutor;
private BT_ExecuteMove kengekiExecutor;
private BT_ExecuteMove interruptExecutor;

private Node ConstructBehaviorTree()
{
    activeExecutor = new BT_ExecuteMove(body, moveTable);
    kengekiExecutor = new BT_ExecuteMove(body, moveTable);
    interruptExecutor = new BT_ExecuteMove(body, moveTable);

    return new Selector(new System.Collections.Generic.List<Node>
    {
        new ConditionNode(() => body.IsPostureBroken),

        new BT_Kengeki(body, moveTable, PlayerTarget, kengekiExecutor),

        new BT_DeflectIf(body, () =>
            !body.IsAttacking
            && !body.IsParried
            && PlayerBody != null && PlayerBody.IsAttacking
            && !blackboard.IsOnCooldown("deflect", deflectCooldown)
            && Distance() <= deflectRange),

        new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor),

        new BT_PickActive(body, moveTable, PlayerTarget, activeExecutor),

        new BT_MoveToTarget(body, PlayerTarget, attackRange)
    });
}
```

`BT_HealPunish` 与 `BT_PickActive` 放独立文件（或 `BTBrain.cs` 底部），都必须实现 `ISelectorLock`：

```csharp
public class BT_HealPunish : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly CharacterBody player;
    private readonly BossMoveTable table;
    private readonly BT_ExecuteMove executor;

    public BT_HealPunish(CharacterBody body, BossMoveTable table, CharacterBody player, BT_ExecuteMove executor)
    {
        this.body = body;
        this.table = table;
        this.player = player;
        this.executor = executor;
    }

    public override NodeState Evaluate()
    {
        if (executor.IsBusy) return executor.Evaluate();
        if (body.IsAttacking || body.IsParried) return NodeState.Failure;
        if (player == null || !player.IsHealing) return NodeState.Failure;
        BossMoveEntry heavy = table.FindById("Bow_Heavy");
        if (heavy == null) return NodeState.Failure;
        if (blackboard != null && blackboard.IsOnCooldown(heavy.id, heavy.cooldown))
            return NodeState.Failure;
        return executor.Begin(heavy);
    }
}

public class BT_PickActive : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;

    public BT_PickActive(CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor)
    {
        this.body = body;
        this.table = table;
        this.target = target;
        this.executor = executor;
    }

    public override NodeState Evaluate()
    {
        if (executor.IsBusy) return executor.Evaluate();
        if (body.IsPostureBroken || body.IsParried) return NodeState.Failure;
        float dist = Vector3.Distance(body.transform.position, target.position);
        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Active, body, body.Animator, blackboard, dist);
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }
}
```

三个 `BT_ExecuteMove` 实例不要共用（交锋/重箭/主动状态机互相踩）。

- [ ] **步骤 6：编译。Play：表还是空数组时只会走、不会砍。不要 commit。**

---

### 任务 6：默认表 + Editor + 挂到 Boss

**文件：**
- 创建 `Assets/Scripts/Boss/GenichiroMoveCatalog.cs`
- 创建 `Assets/Editor/BossMoveTableEditor.cs`
- 创建 `Assets/SO/Boss/GenichiroMoveTable.asset`
- Boss Prefab / 场景 `BTBrain.moveTable` 赋值

- [ ] **步骤 1：窗口工具写在 Catalog 里**

```csharp
static BossMoveWindow Hit(float duration, float hitAt = 0.2f, float recoverAt = -1f)
{
    float rec = recoverAt > 0f ? recoverAt : duration * 0.55f;
    return new BossMoveWindow
    {
        hitStartTime = hitAt,
        recoverStart = rec,
        comboWindowEnd = Mathf.Min(duration, rec + 0.15f),
        stateDuration = duration,
        rotateEnd = Mathf.Min(0.35f, duration),
        transitionDuration = 0.1f
    };
}

static BossMoveWindow NoHit(float duration)
{
    return new BossMoveWindow
    {
        hitStartTime = duration,
        recoverStart = duration,
        comboWindowEnd = duration,
        stateDuration = duration,
        rotateEnd = Mathf.Min(0.15f, duration),
        transitionDuration = 0.08f
    };
}

static BossAnimSequence Seq(params string[] states)
{
    return new BossAnimSequence { states = states };
}

static BossMoveEntry Move(
    string id, BossMoveLayer layer,
    float min, float max, float weight, float cooldown,
    BossAnimSequence[] sequences, BossMoveWindow[] windows,
    PerilousType perilous = PerilousType.None,
    BossMoveExtra extra = BossMoveExtra.None,
    int dmg = 10, float posture = 15f)
{
    return new BossMoveEntry
    {
        id = id,
        layer = layer,
        minRange = min,
        maxRange = max,
        weight = weight,
        cooldown = cooldown,
        sequences = sequences,
        windows = windows,
        perilous = perilous,
        extra = extra,
        baseDamage = dmg,
        postureDamage = posture
    };
}

public static void Apply(BossMoveTable t)
{
    t.kengekiMaxRange = 2.5f;
    t.postureLowThreshold = 0f;
    t.air5HeavyInterruptChance = 0.5f;
    t.moves = BuildMoves();
}

static BossMoveEntry[] BuildMoves()
{
    return new[]
    {
        Move("Bow_ThenSlash", BossMoveLayer.Active, 7f, 99f, 600f, 6f,
            new[] { Seq("Bow_Shot", "3015") }, new[] { Hit(1.6f), Hit(1.8f) }),
        Move("Bow_Shot", BossMoveLayer.Active, 7f, 99f, 200f, 5f,
            new[] { Seq("Bow_Shot") }, new[] { Hit(1.6f) }),
        Move("Slash_Rush2", BossMoveLayer.Active, 5f, 99f, 300f, 6f,
            new[] { Seq("Slash_Rush2") }, new[] { Hit(2.2f) }),
        Move("Slash_RushThenBow", BossMoveLayer.Active, 5f, 7f, 100f, 8f,
            new[] { Seq("Kengeki_Heavy", "3011") }, new[] { Hit(1.8f), Hit(1.6f) }),
        Move("Boat", BossMoveLayer.Active, 3f, 7f, 300f, 10f,
            new[] { Seq("Boat1", "Boat2") }, new[] { Hit(2.4f), Hit(2.4f) },
            extra: BossMoveExtra.PostureLow),
        Move("Slash_Double", BossMoveLayer.Active, 3f, 5f, 10f, 4f,
            new[] { Seq("Slash_Double") }, new[] { Hit(1.8f) }),
        Move("Slash_Heavy", BossMoveLayer.Active, 3f, 5f, 30f, 5f,
            new[] { Seq("Slash_Heavy") }, new[] { Hit(2.0f) }),
        Move("Slash_SpinElbow", BossMoveLayer.Active, 0f, 5f, 15f, 6f,
            new[] { Seq("Slash_Spin", "Elbow") }, new[] { Hit(1.6f), Hit(1.4f) }),
        Move("Slash_StepTurn", BossMoveLayer.Active, 0f, 3f, 15f, 4f,
            new[] { Seq("Slash_StepTurn") }, new[] { Hit(1.8f) }),
        Move("Kick", BossMoveLayer.Active, 0f, 3f, 30f, 5f,
            new[] { Seq("Attack_Slash", "Kick") }, new[] { Hit(1.2f), Hit(1.4f) }),
        Move("JumpThrust", BossMoveLayer.Active, 0f, 5f, 20f, 8f,
            new[] { Seq("JumpThrust") }, new[] { Hit(2.2f, 0.35f, 1.4f) },
            PerilousType.Thrust),
        Move("Perilous_Sweep", BossMoveLayer.Active, 0f, 5f, 10f, 8f,
            new[] { Seq("Sweep") }, new[] { Hit(2.4f, 0.4f, 1.6f) },
            PerilousType.Sweep),
        Move("Bow_Air5", BossMoveLayer.Active, 0f, 3f, 30f, 10f,
            new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), Hit(4.5f, 0.3f, 4.0f) }),

        Move("Bow_Heavy", BossMoveLayer.Interrupt, 0f, 99f, 1f, 8f,
            new[] { Seq("Bow_Heavy") }, new[] { Hit(3.0f, 0.4f, 2.2f) }, dmg: 25, posture: 30f),

        Move("Kengeki_Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 40f, 0.5f,
            new[] { Seq("3050"), Seq("3055"), Seq("3065"), Seq("3071"), Seq("3076") },
            new[] { Hit(1.5f) }),
        Move("Kengeki_Double", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 3f,
            new[] { Seq("Kengeki_Double") }, new[] { Hit(1.8f) }),
        Move("Kengeki_Thrust", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
            new[] { Seq("Kengeki_Thrust") }, new[] { Hit(2.0f, 0.35f, 1.3f) },
            PerilousType.Thrust),
        Move("Kengeki_Heavy", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 4f,
            new[] { Seq("Step_L", "Kengeki_Heavy"), Seq("Step_R", "Kengeki_Heavy") },
            new[] { NoHit(0.45f), Hit(1.8f) }),
        Move("Kengeki_Bow", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 5f,
            new[] { Seq("3031", "3019", "3029"), Seq("3031", "3036") },
            new[] { NoHit(0.8f), Hit(1.8f), Hit(1.8f) }),
        Move("Kengeki_Air5", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 8f,
            new[] { Seq("Dodge_Back", "Bow_Air5") }, new[] { NoHit(0.55f), Hit(4.5f, 0.3f, 4.0f) }),
        Move("Boat_Full", BossMoveLayer.Kengeki, 0f, 2.5f, 20f, 12f,
            new[] { Seq("Boat_Full") }, new[] { Hit(3.2f) },
            extra: BossMoveExtra.HpBelow75),
        Move("Kengeki_Bow2Slash", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
            new[] { Seq("3018", "3015") }, new[] { Hit(1.6f), Hit(1.8f) }),
        Move("Kengeki_JumpBow", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
            new[] { Seq("3034", "3036", "3015") }, new[] { Hit(1.6f), Hit(1.6f), Hit(1.8f) }),
        Move("Bow_AirHeavy", BossMoveLayer.Kengeki, 0f, 2.5f, 10f, 6f,
            new[] { Seq("Bow_AirHeavy") }, new[] { Hit(2.2f) })
    };
}
```

`Bow_ThenSlash` 无 3014：用 `Bow_Shot` + `3015`。缺状态的行由 Picker 权重变 0，不要在 Catalog 里删行。

`Kengeki_Bow` 两套：`3031→3019→3029` 或 `3031→3036`。windows 三格：`NoHit(0.8), Hit(1.8), Hit(1.8)`，短套用前两格。

- [ ] **步骤 2：Editor**

```csharp
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BossMoveTable))]
public class BossMoveTableEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("填入弦一郎默认招式表"))
        {
            BossMoveTable t = (BossMoveTable)target;
            Undo.RecordObject(t, "Fill Genichiro moves");
            GenichiroMoveCatalog.Apply(t);
            EditorUtility.SetDirty(t);
        }
    }

    [MenuItem("ARPG/Create Genichiro Move Table")]
    static void CreateAsset()
    {
        const string path = "Assets/SO/Boss/GenichiroMoveTable.asset";
        BossMoveTable existing = AssetDatabase.LoadAssetAtPath<BossMoveTable>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            return;
        }
        if (!AssetDatabase.IsValidFolder("Assets/SO"))
            AssetDatabase.CreateFolder("Assets", "SO");
        if (!AssetDatabase.IsValidFolder("Assets/SO/Boss"))
            AssetDatabase.CreateFolder("Assets/SO", "Boss");
        BossMoveTable t = ScriptableObject.CreateInstance<BossMoveTable>();
        GenichiroMoveCatalog.Apply(t);
        AssetDatabase.CreateAsset(t, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = t;
    }
}
#endif
```

asset 已存在则菜单只选中，不覆盖。更新内容用 Inspector 按钮。

- [ ] **步骤 3：把 `GenichiroMoveTable` 拖到 Boss 的 `BTBrain.moveTable`。场景里若有 BTBrain override 也要拖。**

- [ ] **步骤 4：编译 0 error。不要 commit。**

---

### 任务 7：架构文档与验收清单

**文件：** `Docs/architecture/04-behavior-tree-ai.md`、`Docs/architecture/04-behavior-tree-ai-test.md`

- [ ] **步骤 1：把「简单树」改成完整薄树。不要写普攻 1-2-3-4，不要再把 `BT_Combo` + `lastComboIndex` 当正路。**

```
Selector:
├─ 崩解中 → Success（不选招）
├─ BT_Kengeki（武装且硬直结束、距离≤2.5 → 交锋表）
├─ BT_DeflectIf（玩家攻击中、距离、冷却）
├─ 玩家 IsHealing → Bow_Heavy
├─ BT_PickActive（距离档加权）
└─ BT_MoveToTarget
```

写明：`Slash_Spin`+`Elbow` 一行 `Slash_SpinElbow`；`Boat` ≠ `Boat_Full`；`Kengeki_Slash` 五片随机；`Kengeki_Bow` 先 3031 再二选一。

- [ ] **步骤 2：验收清单增加「M7 完整版」，简单版标明已被替换。必须含：**

1. 远（>7m）：会 `Bow_Shot` / `Bow_ThenSlash` 或 `Slash_Rush2`，不站桩空挥。  
2. 5–7m：能见 `Slash_Rush2` / 飞舟（架势偏低时）。  
3. 3–5m：能见二连 / 重砍 / 旋转接肘。  
4. ≤3m：能见转身砍、踢、后跳五连。  
5. 完美弹刀且未崩解、贴身：还击来自交锋表；多次 `Kengeki_Slash` 会换片。  
6. 弹刀后拉开 >2.5m：不交锋，回主动。  
7. Boss HP <75% 时弹刀：有机会 `Boat_Full`。  
8. `JumpThrust` / `Kengeki_Thrust`：危字 + 识破仍崩解。  
9. `Sweep`：危字 + 跳踩仍成立。  
10. 玩家喝药：Boss 出 `Bow_Heavy`（Boss 自己不在近战动画中时）。  
11. 空中五连：能完整播完；有时会被重箭打断。  
12. 回归：追击朝向、招架弹开、崩解忍杀与简单树已验收行为一致。  
13. Console：缺 Animator 状态的招不出（权重 0），无新的每帧 error。

- [ ] **步骤 3：不要 commit。**

---

### 任务 8：实现者编译与冒烟

- [ ] 编译 0 error  
- [ ] Boss 上 `moveTable` 已赋值，`PlayerTarget` / `PlayerBody` 仍在  
- [ ] Play 约 30 秒：Boss 会接近并发招，无 NRE  
- [ ] 对照任务 7 列出「实现者已看过 / 需用户验」  

不要把用户才能确认的手感写成已完成。

---

## 规格覆盖自检

| 规格 | 任务 |
| --- | --- |
| 薄树六层、Running 不因距离取消、交锋 >2.5 NoAction | 5 |
| BossMoveTable、烤 AttackConfig、多段、弓走路过 Hitbox | 1, 2, 4 |
| 主动距离档、Spin+Elbow 一条、Boat 两段 | 6 |
| 交锋表、Slash 五随机、Bow 二选一、Boat_Full HP&lt;75% | 3, 5, 6 |
| 招架、喝药重箭、五连打断 | 4, 5 |
| 替换 HitOnce、不改崩解忍杀 | 5 |
| 手测 | 7, 8 |

无 TODO 挡实现。权重写在表里，手感调 SO。

**全局类型名（禁止混用）：** `BossMoveTable`、`BossMoveEntry`、`BossMovePicker.Pick`、`BossAttackBaker.Bake`、`BT_ExecuteMove.Begin`、`KengekiArmed`、`IsParried`、`ISelectorLock`、`FindById`、`ChooseSequence`、`WindowFor`、`air5HeavyInterruptChance`、`kengekiMaxRange`。

## 实现时禁止

- 用 `MainStateMachine.CurrentState is XxxState` 查交锋/攻击  
- 给 Boss 建一堆 AttackConfig SO 当招谱  
- 把 `BT_Combo` 的 AttackSet 下标当弦一郎连招  
- 修 Selector 时记住 `BT_MoveToTarget`  
- 自动 git commit  
