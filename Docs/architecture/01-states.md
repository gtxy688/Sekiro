# 01 状态机核心（M1 HFSM 基架 + M4 受击/弹反/闪避 + M10 处决）

> 模块：M1, M4, M10
> 前置：无
> 验收：`01-states-test.md`

## 一、HFSM 结构

```
MainStateMachine（顶层，只装 HierarchicalState）
├─ GroundedState（父状态）
│  └─ SubStateMachine
│     ├─ IdleState
│     ├─ MoveState
│     ├─ AttackState
│     ├─ DeflectState       ← 防御/盾反
│     ├─ DodgeState         ← 垫步（无方向垫步踩中突刺才识破）
│     └─ MikiriCounterState ← 识破（M17：突刺 + 垫步 → 踩刀）
├─ AirState（父状态）
│  └─ SubStateMachine
│     ├─ AirIdleState     ← Jump / Jumping / Jump2 / Fall
│     └─ AirAttackState   ← AirAttack1→2→3
└─ StunnedState（父状态）
   └─ SubStateMachine
      └─ GroundStunnedState ← 受击统一播地面受击（无空中受击，空中被打也用它）
```

> **移动方式：全权根运动**。位移完全由动画 Root 曲线驱动（Animator.applyRootMotion = true），
> `CharacterBody.OnAnimatorMove` 把动画位移转成 Rigidbody 水平速度（Y 保留重力），
> 代码只负责朝向（RotateTowards）与状态切换，不再直接设置速度。
> 切动画一律走 `AnimUtil.TryPlay` / `TryCrossFade`：用短名哈希在整层查找（状态名不重复），不要拼 `_Hurt.xxx`。两参数 `CrossFade` 会把 layer 当成 -1，必须走带 layer 的哈希重载。
> **已移除的状态**：AirDeflectState / AirStunnedState / 独立 JumpState/FallState 仍不恢复。AirAttackState 已恢复。
> **空中命令**：滞空可 `AttackCommand`（随时）与一次 `JumpCommand`（Jump2）。落地立刻离开 `AirAttackState`，不在地上把空中刀挥完。Jump2 没踩中不给垂直速度。踩中见 `03-hit-detection.md`。

**红线**：顶层只装 HierarchicalState。叶子状态永远在父状态 SubStateMachine 内。
**红线**：`MainStateMachine.CurrentState is DodgeState` 永远为 false，禁止这样查。

## 二、命令路由（已有）

```
Brain (输入/AI) → body.TryExecuteCommand(cmd) → MainStateMachine.HandleCommand
  → 父状态 OnParentHandleCommand 拦截? → 是: 返回 true
  → 否: → 子状态 HandleCommand
```

- 返回 true = 消耗，清空缓冲池
- 返回 false = 拒收，留在缓冲池等 0.2s 超时

## 三、Hit 路由（M1，已实现）

镜像 Command 路由，让受击结算能查到"当前在防御吗/受击中吗"。

### HitData 定义

```csharp
public struct HitData
{
    public CharacterBody attacker;   // 攻击者
    public int healthDmg;            // 血量伤害
    public float postureDmg;         // 架势伤害
    public Vector3 hitPoint;         // 命中点
}
```

### BaseState 新增

```csharp
// 默认：不处理 Hit，返回 false = 交给上层扣血
public virtual bool OnHitReceived(HitData hit) { return false; }
```

### HierarchicalState 转发

```csharp
public override bool OnHitReceived(HitData hit)
{
    if (OnParentHandleHit(hit)) return true;              // 父拦截
    return SubStateMachine.CurrentState?.OnHitReceived(hit) ?? false; // 子处理
}

protected virtual bool OnParentHandleHit(HitData hit) { return false; }
```

### CharacterBody.ReceiveHit（入口）

```csharp
public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint)
{
    HitData hit = new HitData { attacker, healthDmg, postureDmg, hitPoint, hitGrade, isProjectile, ... };

    // 1. 先问当前状态：能拦截吗？（弹反/闪避/受击期间）
    if (MainStateMachine.CurrentState.OnHitReceived(hit))
    {
        return; // 被状态拦截了（弹反成功 / 无敌帧 / 玩家二次受击已在内结算）
    }

    // 2. 没拦住 → 扣血
    TakeDamage(healthDmg, postureDmg);

    // 3. 强切受击父状态。玩家 new StunnedState(this, hit.hitGrade)；Boss 仍按 knockback。
    MainStateMachine.ChangeState(...);
}
```

## 四、各状态 OnHitReceived 实现（M4）

### DeflectState（防御/盾反）

- 弹反窗口内（玩家）：Light → `Deflect_Slash`；箭 Heavy → `Deflect_HeavyArrow`；其余 → `Deflect_HeavySlash`。Boss 被动弹反仍按 `knockback` 选 `Deflect_Slash` / `Deflect_HeavySlash`。近战完美弹反增加攻击者架势；防守者自己不涨架势。**弹反箭不涨 Boss 架势、不把 Boss 弹进 `Deflected`。**
- **玩家弹反 Boss 连段不打断招**：Boss 不进 `ParriedState`，飞舟/二连等继续出完，玩家才能连续弹反。架势仍涨；打崩才进 `Stagger_Broken_Deflect`。**Boss 弹反玩家**时玩家仍进 `ParriedState` 播 `Deflected`。
- 玩家被弹开时硬直 = **max(被弹动画, `Config.ParriedDuration` 下限)**——配置是下限不是兜底，保证被弹方稳定被压出一段反击窗口（回合制）。
- 完美弹反成功后弹反方 `KengekiArmed = true`：Boss 弹反玩家 → BT_Kengeki 层（树序优先、短前摇）抽交锋还击招；命令先存下，反击发起时刻按"目标命中 - 该招 HitStartTime"倒推：`发起 = 弹反开始 + 被弹方硬直 + ParryCounterHitDelay(0~0.15) − HitStartTime`，命中固定落在被弹方硬直结束 + 补偿处——玩家恢复瞬间的刀（前摇 ~0.15s）永远晚于反击命中，贪刀必被罚；弹反动画完整播放作为表现，到点切入反击攻击。
- 弹反收刀：`Deflect_Slash`/`Deflect_HeavySlash` 播到 0.9（兜底 1.1s）归档；有反击命令时保持姿态等发起时刻。
- 窗口外仍在防御（玩家）：刀/箭 Light、Mid → `Hurt_Guard`；刀 Heavy **穿透**（当没防）；箭 Heavy → `Stagger_Broken`（只播动画，不是真崩架势，必须播完；播完按住继续举刀，松开回 Idle）。Boss 格挡仍按 `knockback` 选 `Hurt_Guard` / `Hurt_GuardHeavy`。只增加防守者架势。
- 未格挡受击（玩家）：按招式 `HitGrade` 选 `Hurt_Light` / `Hurt_Mid` / `Hurt_Heavy`。Boss 被打仍按 `knockback` 选 `Hurt_Ground` / `Hurt_Heavy`。
- 普通格挡不会增加攻击者架势；只有近战完美弹反会。弹反箭不加攻击者架势。
- 完美弹反造成攻击者架势崩解时，不再进入普通 `ParriedState`，改走弹反忍杀确认窗口。

### StunnedState（受击期间）

玩家二次受击：**扣血涨架势**，动画是否刷新看当前等级 vs 新一击等级（见下方连续受击）。Boss 二次受击仍拦截且不掉血。

受击动画默认播完：Light 播完回 Idle；Mid/Heavy 播完 → `Standing` → Idle。空中被打也走 `GroundStunnedState`。

玩家受击后摇（移动/攻击/跳跃仍锁到动画结束）：Light 从 `StunDuration` 起可垫步，**防御随时可取消**（含 `Hurt_Light2`，直接进 `DeflectState`）；Mid 从 `KnockdownStunDuration` 起可垫步；Heavy 从 `HeavyStunDuration` 起可垫步。不垫/不防则动画仍播完（Light → Idle，Mid/Heavy → Standing）。对应垫步字段填 `0` = 该等级期间不能垫步。二次受击刷新动画时后摇计时重算。

玩家 `Hurt_Mid` 且倒地结束时间（`CharacterConfig.HurtMidFallEndTime`，相对动画 0 点）之前按下防御 → `MidToGuard`；过了只能躺完再 `Standing`。`MidToGuard` 播完：按住 → 举刀循环，松开 → Idle。

### 玩家受击等级（只作用于玩家挨 Boss）

| 等级 | 第一次 | 连续刷新 |
|------|--------|----------|
| Light | `Hurt_Light` | 任意等级都刷新；同级从第二次起每次重播 `Hurt_Light2` |
| Mid | `Hurt_Mid` | Light/Mid 不刷新；Heavy → `Hurt_HeavyRepeat` |
| Heavy | `Hurt_Heavy` | Light/Mid 不刷新；再 Heavy → `Hurt_HeavyRepeat`（每次重播） |

Light 受击动画播完回 Idle。`Hurt_Mid` / `Hurt_Heavy` / `Hurt_HeavyRepeat` 播完 → `Standing` → Idle。`Standing` 期间挨刀 = 新的一次受击。Heavy 不另标倒地结束点。垫步取消见上方后摇窗口（`StunDuration` / `KnockdownStunDuration` / `HeavyStunDuration`）。

旧名 `Hurt_Ground` 在玩家侧等同 `Hurt_Light`（代码按短名回退）。

### AttackState（攻击中被打）
默认 false → 会被打断进 StunnedState（只狼里被打就是打断）。
**例外（霸体）**：当前招是危字（`AttackConfig.Perilous != None`）、飞舟（`Boat` / `Boat_Full`，含 `Boat1`/`Boat2` 段）、或 `JumpThrust` 全段（含无危字起跳）时，仍扣血涨架势，**不切受击、招不中断**。架势被打崩或 HP 归零仍走崩解/死亡。玩家普通挥砍出手仍可抓前摇。

## 五、取消规则（攻击前摇 / 格挡连按）

命令仍走叶子 `HandleCommand`，不在 `GroundedState` 父层做取消表。

### 攻击前摇

- Hitbox：**动画时间 `t`（`AttackAnimClock.ReadSeconds`）到 `HitStartTime` 开判定，到 `RecoveryWindowStart` 关判定**（退出再兜底关）。有 `hitPulses` 时按各段 `[start,end)` 开关刀，并回填这两个字段给取消/连招。可取消 = 这一刀已经打完，收刀动画不再扫人。
- `t < HitStartTime`：允许格挡/垫步取消。
- `HitStartTime <= t < RecoveryWindowStart`：动作锁定，离散命令进入 0.2s 输入缓冲。
- `RecoveryWindowStart <= t <= ComboWindowEnd`：判定已关；攻击接 `NextCombo`；移动、格挡、垫步、跳跃、喝药可立即取消。动画仍播到 `StateDuration`（`t >= StateDuration` 回 Idle）。
- 受击后摇：玩家 `StunnedState` 不放开移动/攻击/跳跃。垫步：Light=`StunDuration`，Mid=`KnockdownStunDuration`，Heavy=`HeavyStunDuration`。**Light 受击全程可按防御取消进 `DeflectState`**（含 `Hurt_Light2`）。垫步/格挡取消都会打断剩余受击动画。
- `ComboWindowEnd` 后不再接本段 `NextCombo`，尚未过期的命令由动作结束后的状态处理。
- `ComboWindowStart` 已更名为 `RecoveryWindowStart`，使用序列化迁移保留旧 SO 数值。
- `HitStartTime = 0`：进招不可取消（判定仍然一进攻击就开）。
- 攻击转向由 `AttackConfig` 的 `AllowRotation` / `RotationSpeed` / `RotationWindowEnd` 控制；锁定时追踪 Boss，未锁定时按移动输入转向。允许转向的招在 `OnAnimatorMove` 里丢掉 Clip 根旋转，只吃位移，避免挥砍 Root yaw 把朝向拧走。

### 格挡取消

- `DeflectState` 全程（抬刀 / 举刀 / 抖刀 / `Deflect_Slash` / `Deflect_Cancel`）：
  - `DeflectCommand` → 新的 `DeflectState`（重开弹反窗口；再按播 `Deflect_Repeat`，没有该状态则回退抬刀）
  - `DodgeCommand` → `DodgeState`
- 无论短按还是长按，松开格挡键都播放 `Deflect_Cancel`，播放结束后回待机。
- 连按格挡会走 `RegisterDeflectPress` 抖刀惩罚（0.5s 内 ≥3 次，窗口 ×0.75，下限 0.1s）。
- 垫步本身仍不可被打断。
- **锁定垫步**：`DodgeState` 按相对 Boss 的输入取最近四向，播一次性状态 `Dodge_Forward` / `Dodge_Back` / `Dodge_Left` / `Dodge_Right`。无输入默认**前垫**（识破踩刀方向）。斜向取绝对值更大的轴。不要用融合树（一次性 Root 混在一起会斜着滑）。未锁定仍播 `Dodge`。
- **识破**：仅「无方向键垫步」踩中突刺才进 `MikiriCounterState`。带方向的垫步（含后垫）只走无敌帧，不识破。
- **起步**：`IdleToWalk` / `IdleToStrafe` / `DodgeToWalk` 若 Animator 里没有对应状态，直接播 `Walk` / `Walk_Strafe`（Boss 没有 `IdleToWalk` 即可）。
- **走着进格挡**：不播原地 `Deflect_Begin`（会掐步伐），约 0.22s 融合到 `Deflect_Walk` / `Deflect_Strafe` 并对齐步伐，抬刀靠这段融合。待机进格挡仍播抬刀，播完再进举刀循环。

## 六、StunnedState 设计（已有，M4 收尾）

- 顶层 HierarchicalState，`GetInitialSubState()` 统一进入 GroundStunnedState（空中受击已移除，空中被打也播地面受击）。
- 受击期间 `OnParentHandleCommand` 默认吞掉命令。例外：垫步窗口内的 `DodgeCommand`；Light 全程 `DeflectCommand` → `DeflectState`；Mid 倒地结束前 `DeflectCommand` → `MidToGuard`。

## 七、处决/忍杀（M10）

- 架势崩解记录来源：`Attack` / `Deflect` / `Mikiri`，三类均通过事件总线显示忍杀红点。
- 攻击崩解：Boss 播 `Stagger_Broken`，范围内玩家再按攻击后双方播放 `Finsher_Ground`。未处决则动画播完立刻 `RecoverFromBreak`（清架势条），不再套 `PostureBrokenDuration`。
- 处决身份：`CombatManager.PlayerRef` 是唯一发起者，受害者固定 `BossRef`。`TryExecuteFinisher` 正向断言 `initiator == PlayerRef && initiator != BossRef && !initiator.IsPostureBroken`。Boss 的 `AttackCommand` 不能把自己当处决发起者。
- 弹反崩解：Boss 播 `Stagger_Broken_Deflect`，玩家播 `DeflectToFinsher`；窗口内按攻击后双方播放 `Finsher_Deflect`。反向（Boss 弹反打崩玩家）玩家走 `StaggerBrokenState` 击飞倒地（动画播完即恢复，不加额外硬直），Boss 不进确认窗口，继续弹反挥刀。
- 玩家被攻击打崩：同样播 `Stagger_Broken`，动画结束立刻恢复，不套 `PostureBrokenDuration`。崩解期间父层不响应跳跃/喝药；若仍被带入空中，落地回到倒地直到动画结束。倒地期间再挨刀：扣血、解除崩解，并切 `StunnedState` 播 `Hurt_Heavy` 倒地（打崩那一刀仍只播倒地）。Boss 崩解窗口保持倒地，不被普通命中抬起。
- 识破未崩解：Boss 立刻停招，播 `Mikiri_Deflect`（Animator 里若仍叫 `Miriki_Deflect` 也能解析），硬直结束回待机。不抢交锋反击（`KengekiArmed` 不置位）。打断当下钉住水平朝向，**硬直结束后仍保持**，直到下一招 `AttackState` 才允许再转向玩家。
- 识破崩解：Boss 立即播放专用 `Stagger_Broken_Mikiri`（现资源名 `Stagger_Broken_Miriki` 仍兼容）。玩家现有 `Mikiri` 剩余动画作为确认窗口（跟 Clip 走）。确认窗口不对玩家对齐朝向；窗口内按攻击后双方播放成对忍杀：`Finsher_Mikiri`（Boss 侧现资源名 `Finsher_Miriki` 仍兼容），开演才水平对视。弹反/识破确认窗口不走 Ground 处决距离门。
- 弹反/识破窗口超时：Boss 架势从 100% 降到 80%，解除崩解并隐藏红点。
- 忍杀开始前双方只转水平朝向彼此，不瞬移对齐站位。开始后隐藏红点；`IsFinisherLocked` 期间双方锁定命令、受击与强切攻击，直到动画播完。
- Boss 崩解窗口内玩家再按攻击：优先处决（含连招后摇里的 AttackCommand），不进 `NextCombo`。崩解那一刀本身不会再发攻击指令，不会被同一刀直接处决。
- 玩家忍杀动画播完后由 `FinisherState` 调用 `CombatManager.ExecuteFinisher()` 清命，再 `CompleteFinisherSequence` 解锁双方。不依赖命中帧动画事件。

## 七b、Elbow 投技（Grab）

- `Slash_SpinElbow` 第二段 `Elbow` 打中玩家后，`CombatManager.TryStartGrabThrow` 把双方切进 `GrabThrowState`，都播 `Elbow_Danger`。不清命、无红点，不是忍杀。
- 开始前双方只转水平朝向彼此，不瞬移。复用 `IsFinisherLocked`：锁命令、受击、强切，BT 跳过，直到两边动画都播完再一起回 Idle。
- 垫步无敌、弹反窗口仍可解投技；普通格挡等于没防；不可识破。受击中再吃 Grab 也会进投技。本次命中打死不播投技。

## 涉及文件

- 修改：`Assets/Scripts/FrameWork/States/Base/BaseState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Base/HierarchicalState.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（ReceiveHit 实现）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`（锁定四向垫步）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`（进攻击开判定 + 前摇取消）
- 修改：`Assets/Scripts/SO/AttackConfig.cs`（`HitStartTime`、`HitGrade`）
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/GroundStunnedState.cs`
- 创建：`Assets/Scripts/Combat/HitReactionUtil.cs`
- 创建：`Assets/Scripts/FrameWork/States/Ground/StandingState.cs`
- 创建：`Assets/Scripts/FrameWork/States/Ground/MidToGuardState.cs`
- 创建：`Assets/Scripts/Configs/HitGrade.cs`
- 创建：`Assets/Scripts/FrameWork/States/Ground/GrabThrowState.cs`
