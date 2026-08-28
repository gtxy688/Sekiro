# 横扫踩头（Jump2）与空中三连实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 横扫改为 Jump2 踩头（关判定、招继续、不播受击）；空中随时可平 A 三连，伤害与地面轻砍相同。

**架构：** Jump2 与空中刀都是 `AirState` 叶子。踩头走 `CombatManager.ApplySweepStomp`（`TakeDamage`，不 `ReceiveHit`）并置 `SuppressAttackHitbox`。空中三连是独立 `AirAttackState`，落地立刻回 `GroundedState`。

**技术栈：** Unity 2022 LTS、现有 HFSM、`AttackConfig` SO。本项目无自动化测试；用 Unity 编译 0 error + 架构验收清单。不要新建 Test Runner。不要擅自 git commit。

**规格：** `Docs/superpowers/specs/2026-08-28-sweep-stomp-air-attack-design.md`

**本任务只改这些架构文档：** `01-states.md`、`01-states-test.md`、`03-hit-detection.md`、`03-hit-detection-test.md`

**不要做：** JumpThrust 镜头/迷雾/落地分叉；空中格挡/垫步/空中受击片；非 Sweep 踩 Boss 给上升；踩头进 `MikiriCounterState` / `ForceParryStun`。

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 修改 `Assets/Scripts/SO/CharacterConfig.cs` | `SweepStompRadius` / `SweepStompHeight` |
| 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | `AirAttack` 槽、`AirJump2Used`、`SuppressAttackHitbox`、`TryAirJump2`、`IsPerformingSweep` |
| 修改 `Assets/Scripts/Combat/CombatManager.cs` | `ApplySweepStomp` |
| 修改 `Assets/Scripts/FrameWork/States/Ground/AttackState.cs` | `ApplyHitbox` 尊重 `SuppressAttackHitbox`；`OnExit` 清旗 |
| 修改 `Assets/Scripts/FrameWork/States/Air/AirIdleState.cs` | 存 parent；Jump2；空中平 A；删自动踩头 |
| 创建 `Assets/Scripts/FrameWork/States/Air/AirAttackState.cs` | 空中三连叶子 |
| 修改 `Assets/Scripts/FrameWork/States/Air/AirState.cs` | 进空清 Jump2；AirAttack 落地立刻离空 |
| 创建 `Assets/SO/Player/AirAttack1.asset` 等三张 | 空中刀 SO，数值对齐地面轻砍 |
| 修改玩家场景组件 | `CharacterBody.AirAttack` 拖 AirAttack1 |
| 修改上述 4 份架构文档 | 与规格一致 |

Unity 多实例时先 `set_active_instance` → `ARPG@b4f5a8a84b4d31e9`。新 `.cs` 要 Import。Animator 短名必须已有：`Jump2`、`AirAttack1`、`AirAttack2`、`AirAttack3`。

---

### 任务 1：踩头数据与 CharacterBody API

**文件：**
- 修改：`Assets/Scripts/SO/CharacterConfig.cs`（跳跃 Header 后）
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`

- [ ] **步骤 1：CharacterConfig 增加检测范围**

在跳跃段 `JumpSpeed` 后面加：

```csharp
    [Header("横扫踩头")]
    [Tooltip("脚底水平检测半径。踩中还要求 Boss 正在放 Sweep")]
    public float SweepStompRadius = 0.6f;

    [Tooltip("脚底相对 Boss 根向上的最大高度差")]
    public float SweepStompHeight = 1.2f;
```

不要把踩头伤害写进 Config（伤害跟玩家 `LightAttack`）。

- [ ] **步骤 2：CharacterBody 增加槽位和旗**

在 `ThrustAttack` 旁：

```csharp
    public AttackConfig AirAttack;
```

在 `KengekiArmed` 旁：

```csharp
    public bool SuppressAttackHitbox { get; set; }
    public bool AirJump2Used { get; private set; }

    public void ResetAirJump2()
    {
        AirJump2Used = false;
    }
```

- [ ] **步骤 3：Sweep 检测 + TryAirJump2**

放在 `QueueJump` 附近。`IsPerformingSweep` 读运行时表行/窗口（AttackState 会清 `ActiveAttack`）：

```csharp
    public bool IsPerformingSweep()
    {
        if (!IsAttacking) return false;
        if (CurrentMoveWindow != null && CurrentMoveWindow.perilous == PerilousType.Sweep)
            return true;
        if (CurrentMoveEntry != null && CurrentMoveEntry.perilous == PerilousType.Sweep)
            return true;
        return false;
    }

    public bool IsWithinSweepStompRange(CharacterBody boss)
    {
        if (boss == null || Config == null) return false;
        Vector3 feet = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
        Vector3 bossPos = boss.transform.position;
        Vector3 delta = bossPos - feet;
        delta.y = 0f;
        if (delta.sqrMagnitude > Config.SweepStompRadius * Config.SweepStompRadius)
            return false;
        float dy = feet.y - bossPos.y;
        return dy >= 0f && dy <= Config.SweepStompHeight;
    }

    // 返回 true = 命令吃掉。playedNew = 本次新播了 Jump2（已用过则为 false）。
    public bool TryAirJump2(out bool playedNew)
    {
        playedNew = false;
        if (AirJump2Used) return true;
        AirJump2Used = true;

        if (!AnimUtil.HasState(Animator, "Jump2"))
        {
            Debug.LogError($"{name} 的 Animator 缺少状态：Jump2");
            return true;
        }

        AnimUtil.TryCrossFade(Animator, "Jump2", Config != null ? Config.JumpAnimBlend : 0.08f);
        playedNew = true;

        CharacterBody boss = CombatManager.Instance != null ? CombatManager.Instance.BossRef : null;
        if (boss != null && boss.IsPerformingSweep() && IsWithinSweepStompRange(boss))
        {
            QueueJump();
            CombatManager.Instance.ApplySweepStomp(this, boss);
        }

        return true;
    }
```

本步调用了尚未存在的 `ApplySweepStomp`，任务 2 立刻补上，不要只提交任务 1。

- [ ] **步骤 4：Unity 编译**

`refresh_unity` scope scripts + `read_console` types error。预期：若已写 `ApplySweepStomp` 则 0 error；若还没有则先做任务 2。

---

### 任务 2：ApplySweepStomp + 关判定不切受击

**文件：**
- 修改：`Assets/Scripts/Combat/CombatManager.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`

- [ ] **步骤 1：CombatManager.ApplySweepStomp**

放在 `ReportHit` 后面：

```csharp
    // 横扫踩头：扣血涨架势但不走 ReceiveHit（Boss 不播受击、不切状态）。
    public void ApplySweepStomp(CharacterBody player, CharacterBody boss)
    {
        if (player == null || boss == null || player == boss) return;

        int hp = 10;
        float posture = 15f;
        if (player.LightAttack != null)
        {
            hp = player.LightAttack.BaseDamage;
            posture = player.LightAttack.PostureDamage;
        }

        boss.SuppressAttackHitbox = true;
        boss.DisableWeaponHit();
        boss.TakeDamage(hp, posture);
        TriggerWeaponDeflected(
            CombatFxPoint.BetweenWeapons(player, boss, boss.transform.position + Vector3.up * 1.2f),
            DeflectType.Perfect);
        HitStop();
    }
```

确认 `HitStop` 已是现有 public 方法。`TakeDamage` 内 `AccumulatePosture` 默认可打崩；打崩走现有 `ForcePostureBroken`。

- [ ] **步骤 2：AttackState 尊重关刀旗**

`ApplyHitbox` 开头：

```csharp
        if (body.SuppressAttackHitbox)
        {
            if (weaponHitEnabled)
            {
                body.DisableWeaponHit();
                weaponHitEnabled = false;
                activePulseIndex = -1;
            }
            return;
        }
```

`OnExit` 在现有清理里加：

```csharp
        body.SuppressAttackHitbox = false;
```

这样横扫 `AttackState` 结束才重新允许下一招开刀，本招后半段不会自己把刀打开。

- [ ] **步骤 3：编译 0 error**

`read_console` types error，预期空。

---

### 任务 3：AirIdleState 接 Jump2 与空中平 A，去掉自动反制

**文件：**
- 修改：`Assets/Scripts/FrameWork/States/Air/AirIdleState.cs`

- [ ] **步骤 1：存 parent，扩展 Phase**

把构造和 Phase 改成：

```csharp
    private enum Phase
    {
        Takeoff,
        Airborne,
        Jump2,
        Landing
    }

    private readonly HierarchicalState parent;
    private readonly bool startInJump2;

    public AirIdleState(CharacterBody body, HierarchicalState parent, bool startInJump2 = false) : base(body)
    {
        this.parent = parent;
        this.startInJump2 = startInJump2;
    }
```

`OnEnter` 开头：若 `startInJump2`，只设 `phase = Jump2` 并 return（Jump2 已由 `TryAirJump2` CrossFade）。

`OnUpdate` 里 `phase != Landing` 的落地检测：`phase == Phase.Jump2` 时不要 `TryStartLanding`（避免刚播 Jump2 被切 Fall）。Jump2 播完或过顶点再进 Airborne：

```csharp
        if (phase == Phase.Jump2)
            TryFinishJump2();
```

```csharp
    private void TryFinishJump2()
    {
        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        bool jump2Done = AnimUtil.IsPlaying(info, "Jump2")
            && info.normalizedTime >= JumpToJumpingNorm
            && !body.Animator.IsInTransition(0);
        bool atApex = SwitchAtApex
            && body.Rb.velocity.y < 0f
            && Time.time - enterTime >= MinAirTime;
        bool gone = !AnimUtil.IsPlaying(info, "Jump2")
            && Time.time - enterTime >= TakeoffFailsafe;

        if (!jump2Done && !atApex && !gone) return;

        Play("Jumping", JumpBlend);
        phase = Phase.Airborne;
    }
```

- [ ] **步骤 2：HandleCommand**

替换整个 `HandleCommand`（落地 Fall 期间仍不接攻击/Jump2，留给缓冲）：

```csharp
    public override bool HandleCommand(ICommand cmd)
    {
        if (phase == Phase.Landing) return false;

        if (cmd is JumpCommand)
        {
            body.TryAirJump2(out bool played);
            if (played)
                phase = Phase.Jump2;
            return true;
        }

        if (cmd is AttackCommand)
        {
            AttackConfig air = body.AirAttack;
            if (air == null || string.IsNullOrEmpty(air.AnimName))
            {
                Debug.LogError($"{body.name} 未配置 AirAttack，空中平 A 无效。");
                return true;
            }
            if (!AnimUtil.HasState(body.Animator, air.AnimName))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少空中攻击状态：{air.AnimName}");
                return true;
            }
            parent.SubStateMachine.ChangeState(new AirAttackState(body, parent, air));
            return true;
        }

        if (cmd is MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            return true;
        }

        return false;
    }
```

Jump2 新播后把 `phase = Phase.Jump2`：

```csharp
        if (cmd is JumpCommand)
        {
            body.TryAirJump2(out bool played);
            if (played)
                phase = Phase.Jump2;
            return true;
        }
```

- [ ] **步骤 3：删除自动 Sweep 反制**

`OnHitReceived` 改成直接 `return false;`（空中挨扫走裸受击）。不要再 `ForceParryStun`。

- [ ] **步骤 4：编译**

此时 `AirAttackState` 还不存在会 CS0246。立刻做任务 4。

---

### 任务 4：AirAttackState + AirState 落地

**文件：**
- 创建：`Assets/Scripts/FrameWork/States/Air/AirAttackState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Air/AirState.cs`

- [ ] **步骤 1：新建 AirAttackState**

从 `AttackState.cs` 复制为 `AirAttackState`，然后只改这些（其余 ApplyHitbox / ValidateWindows / 转向 / 连招窗保持同一套逻辑）：

1. 类名 `AirAttackState`。
2. `OnEnter` / `ValidateWindows` 失败：切 `new AirIdleState(body, parent)`，不要 `IdleState`。
3. `OnUpdate` 动作结束：同样切 `AirIdleState`。
4. `OnUpdate` 开头：若 `WantsImmediateLand` 则 return（离空由 AirState 做）。
5. `HandleCommand`：**删掉** Deflect / Dodge / 切 MoveState。保留 AttackCommand 的 NextCombo（新状态仍是 `AirAttackState`）。JumpCommand：仅 `canCancel` 时切 `AirIdleState` 并 `TryAirJump2`：

```csharp
        if (cmd is JumpCommand)
        {
            if (canCancel)
            {
                parent.SubStateMachine.ChangeState(new AirIdleState(body, parent));
                body.TryAirJump2(out bool played);
                if (played && parent.SubStateMachine.CurrentState is AirIdleState idle)
                    idle.EnterJump2Phase();
                return true;
            }
            return false;
        }
```

给 `AirIdleState` 加：

```csharp
    public void EnterJump2Phase()
    {
        phase = Phase.Jump2;
    }
```

注意：先 `ChangeState(AirIdleState)` 会 `OnEnter` 播 `Jump`。应避免。正确顺序：

```csharp
            if (canCancel)
            {
                body.TryAirJump2(out bool played);
                var idle = new AirIdleState(body, parent, startInJump2: played);
                parent.SubStateMachine.ChangeState(idle);
                return true;
            }
```

给 `AirIdleState` 增加 `startInJump2` 构造参数。`OnEnter`：

```csharp
        if (startInJump2)
        {
            enterTime = Time.time;
            CanLeaveAir = false;
            phase = Phase.Jump2;
            return; // TryAirJump2 已经 CrossFade Jump2
        }
```

默认构造 `startInJump2: false`。

6. 落地旗：

```csharp
    private float enterTime;

    public bool WantsImmediateLand
    {
        get
        {
            if (Time.time - enterTime < 0.08f) return false;
            if (body.Rb != null && body.Rb.velocity.y > 0.1f) return false;
            return body.IsGrounded;
        }
    }
```

`OnEnter` 记 `enterTime = Time.time`。

7. `OnExit` 不要清 `SuppressAttackHitbox`（那是 Boss 招的旗）。玩家空中刀 OnExit 只关自己的刀，与 AttackState 相同，但 **不要** `body.SuppressAttackHitbox = false`（玩家不会置这旗；若复制时带了就删掉玩家侧清除，避免误清 Boss 的旗——此旗在 Boss 的 AttackState.OnExit 清）。

- [ ] **步骤 2：AirState 进空清 Jump2，处理空中刀落地**

```csharp
    public override void OnEnter()
    {
        body.IsAirborne = true;
        body.ResetAirJump2();
        base.OnEnter();
    }

    public override void OnUpdate()
    {
        base.OnUpdate();

        if (SubStateMachine.CurrentState is AirAttackState airAtk && airAtk.WantsImmediateLand)
        {
            LeaveAir();
            return;
        }

        AirIdleState air = SubStateMachine.CurrentState as AirIdleState;
        if (air == null || !air.CanLeaveAir) return;
        LeaveAir();
    }

    private void LeaveAir()
    {
        if (body.IsPostureBroken)
        {
            body.MainStateMachine.ChangeState(
                new GroundedState(body, new StaggerBrokenState(body)));
        }
        else
        {
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }
```

- [ ] **步骤 3：Import 新脚本 + 编译 0 error**

Unity MCP Import `AirAttackState.cs`，`read_console` error 为空。

---

### 任务 5：三张空中 AttackConfig + 场景绑定

**文件：**
- 创建：`Assets/SO/Player/AirAttack1.asset`、`AirAttack2.asset`、`AirAttack3.asset`
- 修改：玩家 `CharacterBody.AirAttack`

- [ ] **步骤 1：对齐地面轻砍数值**

打开玩家 `CharacterBody.LightAttack`。记下每段 `BaseDamage` / `PostureDamage` / `HitGrade`（以及 NextCombo 链上第 2、3 段）。没有第 2、3 段则 2、3 用第一段的三个数。

用 `CreateAssetMenu` → Combat/Attack Configuration，或复制 LightAttack 资源后改名。

三张必填：

| 资源 | AnimName | NextCombo | 伤害 |
|------|----------|-----------|------|
| AirAttack1 | `AirAttack1` | AirAttack2 | = 地面第 1 刀 |
| AirAttack2 | `AirAttack2` | AirAttack3 | = 地面第 2 刀或第 1 刀 |
| AirAttack3 | `AirAttack3` | 空 | = 地面第 3 刀或第 1 刀 |

`HitStartTime` / `RecoveryWindowStart` / `ComboWindowEnd` / `StateDuration` 先按各空中 Clip 长度填（打开 Animator 看 Clip Length）。`AllowRotation = true`。`Perilous = None`。`HitboxSlot = Weapon`。

不要用地面片的时长硬套空中片，否则会罚站或刀关太早。

- [ ] **步骤 2：拖到玩家**

玩家 `CharacterBody.AirAttack` = AirAttack1。缺槽时空中平 A 会 LogError，不会误播地面刀名。

- [ ] **步骤 3：确认 Animator**

玩家 controller 已有短名 `Jump2`、`AirAttack1/2/3`。没有就停下来问用户，不要改名去迁就错误短名。

---

### 任务 6：架构文档 + 验收清单

**文件：**
- `Docs/architecture/01-states.md`
- `Docs/architecture/01-states-test.md`
- `Docs/architecture/03-hit-detection.md`
- `Docs/architecture/03-hit-detection-test.md`

- [ ] **步骤 1：01-states.md**

HFSM 树 `AirState` 改为：

```
├─ AirState（父状态）
│  └─ SubStateMachine
│     ├─ AirIdleState     ← Jump / Jumping / Jump2 / Fall
│     └─ AirAttackState   ← AirAttack1→2→3
```

「已移除」一句改成：`AirDeflectState` / `AirStunnedState` / 独立 `JumpState`/`FallState` 仍不恢复。`AirAttackState` 已恢复。

空中命令：滞空可 `AttackCommand`（随时）与一次 `JumpCommand`（Jump2）。落地立刻离开 `AirAttackState`。Jump2 没踩中不给垂直速度。踩中见 03。

- [ ] **步骤 2：01-states-test.md**

Animator 表增加 `Jump2`、`AirAttack1`、`AirAttack2`、`AirAttack3`。验收表增加：

| # | 操作 | 预期 |
|---|------|------|
| A1 | 普通跳，空中点攻击 | 播 AirAttack1，可接 2、3；伤害与地面对应轻砍相同 |
| A2 | 空中三连未完落地 | 立刻回地面 Idle，不在地上把空中刀挥完 |
| A3 | 普通跳空中点跳 | 播 Jump2，人不再明显升高 |
| A4 | 同一跳第二次点跳 | 不再播 Jump2 |

- [ ] **步骤 3：03-hit-detection.md**

Sweep 那一行改成：

- `Sweep` 横扫 → 先跳，空中再 `Jump2` 踩在 Boss 身上（上升 + 轻砍第一刀伤害，Boss 不播受击，本招判定关闭但动画继续）或垫步无敌躲避；**不可防御、不可弹反、不可 Mikiri**；只跳一次空中挨扫 = 没防。

删「空中被 Sweep 命中自动反制」。

- [ ] **步骤 4：03-hit-detection-test.md**

M17 增加：

| # | 操作 | 预期 |
|---|------|------|
| 10b | Boss 放 Sweep，跳起再 Jump2 踩中 | 玩家上升；Boss 掉与轻砍第一刀相同的血/架势、不播 Hurt；横扫继续但后续扫不中 |
| 10c | Boss 放 Sweep，只跳一次空中挨扫 | 全额受伤，Boss 不被弹开 |
| 10d | Sweep 时无方向垫步 | 不识破（仍只有突刺 Mikiri） |

- [ ] **步骤 5：交付说明**

不要自称 Play 过关。请用户按 A1–A4 与 10b–10d 验收。

---

## 自检

| 规格条目 | 任务 |
| --- | --- |
| AirState 两叶子、落地掐连段 | 4、6 |
| Jump2 一次、无踩不上升、踩中上升+结算 | 1、2、3 |
| 关判定招继续、不 ReceiveHit | 2 |
| 删自动反制 | 3 |
| 空中随时平 A、伤害=地面轻砍 | 3、4、5 |
| 命中段不能 Jump2 | 4 |
| 文档 01/03 | 6 |
| 不做 JumpThrust 镜头 | 头部不要做 |
