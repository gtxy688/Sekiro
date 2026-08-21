# 防御反馈与三类忍杀实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 接入轻重防御动画、三种架势崩解来源、忍杀红点以及 Ground/Deflect/Mikiri 三组成对忍杀。

**架构：** 架势入口记录 `PostureBreakSource`，Boss 进入对应等待状态；玩家确认后由 `CombatManager` 对齐双方并启动成对动画。玩家动画事件是唯一正常清命入口，幂等保护与动画结束兜底防止重复结算或永久锁死。

**技术栈：** Unity 2022 LTS、C# HFSM、Animator Root Motion、ScriptableObject、CombatEventBus。

**提交约束：** 用户未要求 Git commit，执行期间不得创建 commit。

---

## 文件职责

- 修改 `Assets/Scripts/FrameWork/States/Command.cs`：定义崩解来源和忍杀类型。
- 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs`：架势来源、部分恢复、动画事件转发。
- 修改 `Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`：轻重弹反和弹反崩解分流。
- 修改 `Assets/Scripts/FrameWork/States/Ground/MikiriCounterState.cs`：识破崩解确认窗口。
- 修改 `Assets/Scripts/FrameWork/States/Ground/GroundedState.cs`：普通崩解忍杀入口守卫。
- 修改 `Assets/Scripts/FrameWork/States/Ground/FinisherState.cs`：玩家成对忍杀播放与结束。
- 新建 `Assets/Scripts/FrameWork/States/Ground/FinisherReadyState.cs`：弹反确认窗口。
- 新建 `Assets/Scripts/FrameWork/States/Ground/FinisherVictimState.cs`：Boss 等待/被处决锁定。
- 修改 `Assets/Scripts/Combat/CombatManager.cs`：三类忍杀校验、对齐、执行与幂等清理。
- 修改 `Assets/Scripts/Mgr/CombatEventBus.cs`：忍杀机会显隐事件。
- 修改 `Assets/Scripts/UI/CombatUIController.cs`：红点显隐。
- 修改 `Assets/Scripts/SO/CharacterConfig.cs`：动画默认名与忍杀偏移。
- 修改 `Docs/architecture/01-states.md`、`Docs/architecture/07-anim-events.md`：同步规则。

### 任务 1：建立崩解来源和红点事件

- [ ] **步骤 1：在 `Command.cs` 增加类型**

```csharp
public enum PostureBreakSource
{
    Attack,
    Deflect,
    Mikiri
}

public enum FinisherKind
{
    Ground,
    Deflect,
    Mikiri
}
```

- [ ] **步骤 2：在 `CombatEventBus.cs` 增加显式机会事件**

```csharp
public static event Action<CharacterBody, bool> OnFinisherOpportunityChanged;

public static void TriggerFinisherOpportunityChanged(CharacterBody target, bool available)
{
    OnFinisherOpportunityChanged?.Invoke(target, available);
}
```

- [ ] **步骤 3：在 `CombatUIController` 的启用/禁用处订阅和退订，并增加处理器**

```csharp
private void HandleFinisherOpportunityChanged(CharacterBody target, bool available)
{
    if (target == bossBody)
        lockOnIndicatorView?.SetFinisherReady(available);
}
```

保留 `OnPostureBroken` 对其他表现的通知，但红点最终状态以新事件为准。

- [ ] **步骤 4：刷新 Unity 并检查 Console**

预期：无 C# 编译错误；进入 Play Mode 前红点状态不改变。

### 任务 2：让架势崩解携带来源

- [ ] **步骤 1：修改 `CharacterBody` 运行时属性**

```csharp
public PostureBreakSource CurrentPostureBreakSource { get; private set; }
```

- [ ] **步骤 2：将 `AccumulatePosture` 改为返回本次是否首次崩解**

```csharp
public bool AccumulatePosture(
    float amount,
    bool allowBreak = true,
    PostureBreakSource source = PostureBreakSource.Attack)
{
    if (IsPostureBroken) return false;

    float max = Config != null ? Config.MaxPosture : 100f;
    CurrentPosture = Mathf.Min(CurrentPosture + amount, max);
    lastHitTime = Time.time;
    CombatEventBus.TriggerPostureChanged(this, CurrentPosture, max);

    if (!allowBreak || CurrentPosture < max) return false;

    IsPostureBroken = true;
    CurrentPostureBreakSource = source;
    CombatEventBus.TriggerPostureBroken(this);
    CombatEventBus.TriggerFinisherOpportunityChanged(this, true);
    ForcePostureBroken(source);
    return true;
}
```

- [ ] **步骤 3：按来源选择 Boss 等待状态**

`ForcePostureBroken(PostureBreakSource source)` 必须：

- 关闭攻击与 Hitbox；
- `Attack` → 原 `StaggerBrokenState`；
- `Deflect` → `FinisherVictimState(this, "Stagger_Broken_Deflect", waitingOnly: true)`；
- `Mikiri` → `FinisherVictimState(this, ResolveHurtAnim(HurtContext.Deflected), waitingOnly: true)`。

玩家自身崩解继续走原 `StaggerBrokenState`，不开放忍杀。

- [ ] **步骤 4：支持按比例解除崩解**

```csharp
public void RecoverFromBreak(float remainingRatio = 0f)
{
    IsPostureBroken = false;
    float max = Config != null ? Config.MaxPosture : 100f;
    CurrentPosture = Mathf.Clamp01(remainingRatio) * max;
    CombatEventBus.TriggerPostureChanged(this, CurrentPosture, max);
    CombatEventBus.TriggerFinisherOpportunityChanged(this, false);
    MainStateMachine.ChangeState(new GroundedState(this));
}
```

原 `StaggerBrokenState` 无参调用仍恢复到 0%；弹反/识破超时传 `0.8f`。

- [ ] **步骤 5：逐一更新调用来源**

- `TakeDamage` 和拼刀维持默认 `Attack`。
- `DeflectState` 调用 `AccumulatePosture(gain, true, PostureBreakSource.Deflect)`。
- `MikiriCounterState` 调用 `AccumulatePosture(gain, true, PostureBreakSource.Mikiri)`。
- 玩家完美弹反自身架势仍传 `allowBreak: false`。

- [ ] **步骤 6：刷新 Unity 并检查 Console**

预期：所有旧调用编译通过；普通攻击打满 Boss 架势仍显示红点。

### 任务 3：接入轻重防御动画和统一格挡退出

- [ ] **步骤 1：同步 `CharacterConfig` 默认名**

```csharp
public string HurtAnim_Heavy = "Hurt_Heavy";
public string HurtAnim_Guard = "Hurt_Guard";
public string HurtAnim_GuardHeavy = "Hurt_GuardHeavy";
```

- [ ] **步骤 2：修改 `DeflectState.OnHitReceived`**

完美弹反动画：

```csharp
string deflectAnim = hit.knockback > 0f
    ? "Deflect_HeavySlash"
    : "Deflect_Slash";
body.Animator.CrossFade(deflectAnim, 0.05f);
```

对攻击者累计架势后保存返回值：

```csharp
bool broke = hit.attacker != null &&
    hit.attacker.AccumulatePosture(
        gain, true, PostureBreakSource.Deflect);

if (broke)
{
    parent.SubStateMachine.ChangeState(
        new FinisherReadyState(body, parent, hit.attacker));
}
else
{
    hit.attacker?.ForceParryStun();
}
```

禁止在 `broke == true` 后继续 `ForceParryStun()`。

- [ ] **步骤 3：统一格挡松手**

删除 `< 0.15f` 的短按分支；所有 `IdleCommand` 调用 `StartCancel()`。`StartCancel()` 重入时直接 `return`，避免重复 CrossFade。

- [ ] **步骤 4：刷新 Unity 并检查 Console**

预期：六种受击/防御状态名均可通过 `AnimUtil.HasState` 检出；缺失时 Console 明确报角色名和状态名。

### 任务 4：实现弹反与识破确认窗口

- [ ] **步骤 1：创建 `FinisherReadyState.cs`**

职责：

- `OnEnter` 播 `DeflectToFinsher`；
- `HandleCommand(AttackCommand)` 调 `CombatManager.TryExecuteFinisher(body, FinisherKind.Deflect)`；
- 动画播放到 `normalizedTime >= 0.95f` 且未确认时，调用 `victim.RecoverFromBreak(0.8f)`，玩家回 `IdleState`；
- 其他命令全部吞掉；
- 受击全部拦截，保证确认姿态不被普通 Hit 覆盖。

- [ ] **步骤 2：修改 `MikiriCounterState`**

在 `OnEnter` 保存：

```csharp
isFinisherWindow = attacker != null &&
    attacker.AccumulatePosture(
        postureGain, true, PostureBreakSource.Mikiri);
```

`HandleCommand`：

```csharp
if (isFinisherWindow && cmd is AttackCommand)
    return CombatManager.Instance != null &&
           CombatManager.Instance.TryExecuteFinisher(body, FinisherKind.Mikiri);
return true;
```

动画结束时：

- `isFinisherWindow == true` → `attacker.RecoverFromBreak(0.8f)`；
- 否则维持原回待机。

- [ ] **步骤 3：限制普通处决入口**

`CombatManager.TryExecuteFinisher(player, FinisherKind.Ground)` 只接受：

```csharp
BossRef.IsPostureBroken &&
BossRef.CurrentPostureBreakSource == PostureBreakSource.Attack
```

`GroundedState` 的父层攻击拦截只调用 `FinisherKind.Ground`，让 Deflect/Mikiri 的攻击命令继续下钻到对应叶子状态。

- [ ] **步骤 4：刷新 Unity 并检查 Console**

预期：三类确认窗口均显示红点；弹反/识破超时后红点关闭且 Boss 架势显示为 80%。

### 任务 5：实现成对忍杀与单次结算

- [ ] **步骤 1：在 `CharacterConfig` 增加三类玩家本地偏移**

```csharp
public Vector3 FinisherGroundOffset = new Vector3(0f, 0f, -1f);
public Vector3 FinisherDeflectOffset = new Vector3(0f, 0f, -1f);
public Vector3 FinisherMikiriOffset = new Vector3(0f, 0f, -1f);
```

数值由 Unity 手动验收时按动画起始姿势调整，不在代码中分散硬编码。

- [ ] **步骤 2：创建 `FinisherVictimState.cs`**

构造参数包含 `CharacterBody body`、`string animName`、`bool waitingOnly`。`OnEnter` 播动画，`HandleCommand` 与 `OnHitReceived` 均返回 `true`。`waitingOnly` 状态不自行超时，由玩家确认窗口或 `CombatManager` 结束。

- [ ] **步骤 3：重写 `CombatManager.TryExecuteFinisher`**

方法签名：

```csharp
public bool TryExecuteFinisher(
    CharacterBody player,
    FinisherKind kind = FinisherKind.Ground)
```

按 `kind` 解析动画名：

- Ground → `Finsher_Ground`
- Deflect → `Finsher_Deflect`
- Mikiri → `Finsher_Mikiri`

校验来源、距离和当前无其他忍杀后：

1. 保存 `activeFinisherPlayer`、`activeFinisherVictim`、`activeFinisherKind`。
2. 设置 `finisherResolved = false`。
3. 按 Boss Transform 与对应 offset 对齐玩家，双方只修正水平朝向。
4. 关闭双方 Hitbox。
5. 隐藏红点。
6. Boss 切 `FinisherVictimState`，玩家切 `FinisherState`。
7. 触发处决表现事件和相机震动。

- [ ] **步骤 4：修改 `FinisherState`**

构造参数包含 victim、kind、双方共用 Animator 状态名。`OnEnter` 播玩家动画；`OnUpdate` 等待当前状态 `normalizedTime >= 0.98f`：

- 若动画事件未触发，记录 Error 并调用一次 `CombatManager.ExecuteFinisher(body)` 兜底；
- 调 `CombatManager.CompleteFinisherSequence()`；
- 不再直接 `ClearLife()`。

- [ ] **步骤 5：增加动画事件入口与幂等结算**

`CharacterBody`：

```csharp
public void ExecuteFinisher()
{
    CombatManager.Instance?.ExecuteFinisher(this);
}
```

`CombatManager.ExecuteFinisher(CharacterBody source)`：

- 仅接受当前 active player；
- `finisherResolved == true` 时立即返回；
- 置 true 后调用 victim `ClearLife()`；
- 隐藏红点。

`CompleteFinisherSequence()`：

- 清空 active 引用；
- Boss 尚有命时双方回 `GroundedState`；
- Boss 无命时玩家回待机，Boss 保持胜利结算状态；
- 禁止再次清命。

- [ ] **步骤 6：在三段玩家动画命中帧添加 `ExecuteFinisher` Event**

- 玩家 `Finsher_Ground`
- 玩家 `Finsher_Deflect`
- 玩家 `Finsher_Mikiri`

Boss 成对动画不添加清命事件。

- [ ] **步骤 7：刷新 Unity 并检查 Console**

预期：三组成对动画可播放；每次忍杀只减少一条命；重复事件不重复扣命。

### 任务 6：同步架构文档并手动验收

- [ ] **步骤 1：更新 `01-states.md`**

写明崩解来源、确认状态、成对忍杀状态和 80% 超时恢复。

- [ ] **步骤 2：更新 `07-anim-events.md`**

列出三个玩家忍杀 Clip 的 `ExecuteFinisher` 事件，并说明事件通过 `CharacterBody` 转发。

- [ ] **步骤 3：执行 Unity 手动验收**

1. 普通/重型裸受击、格挡、弹反分别播放正确动画。
2. 攻击前摇取消进格挡，松开后必播 `Deflect_Cancel`。
3. Ground/Deflect/Mikiri 三类崩解均显示红点。
4. 弹反/识破窗口超时，红点关闭且 Boss 架势为 80%。
5. 三类确认后红点立即关闭，双方位置与朝向正确。
6. 三个动画事件均只清一条命，动画结束后双方状态正确。

预期：Console 无 Error；允许存在用于调偏移的明确日志，不允许 Animator state missing。
