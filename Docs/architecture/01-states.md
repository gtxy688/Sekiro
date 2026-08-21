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
│     ├─ DodgeState         ← 垫步（突刺危字时触发识破）
│     └─ MikiriCounterState ← 识破（M17：突刺 + 垫步 → 踩刀）
├─ AirState（父状态）
│  └─ SubStateMachine
│     └─ AirIdleState       ← 跳跃/下落共用一个状态（Jump/Fall 两个动画切换）
└─ StunnedState（父状态）
   └─ SubStateMachine
      └─ GroundStunnedState ← 受击统一播地面受击（无空中受击，空中被打也用它）
```

> **移动方式：全权根运动**。位移完全由动画 Root 曲线驱动（Animator.applyRootMotion = true），
> `CharacterBody.OnAnimatorMove` 把动画位移转成 Rigidbody 水平速度（Y 保留重力），
> 代码只负责朝向（RotateTowards）与状态切换，不再直接设置速度。
> **已移除的状态**（无对应动画资源）：AirAttackState、AirDeflectState、AirStunnedState、JumpState、FallState（跳跃/下落共用 AirIdleState）。

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
    HitData hit = new HitData { attacker, healthDmg, postureDmg, hitPoint, ... };

    // 1. 先问当前状态：能拦截吗？（弹反/闪避/受击期间）
    if (MainStateMachine.CurrentState.OnHitReceived(hit))
    {
        return; // 被状态拦截了（弹反成功 / 无敌帧 / 二次受击）
    }

    // 2. 没拦住 → 扣血
    TakeDamage(healthDmg, postureDmg);

    // 3. 强切受击父状态（不走 Command，物理强制覆写）
    MainStateMachine.ChangeState(new StunnedState(this));
}
```

## 四、各状态 OnHitReceived 实现（M4）

### DeflectState（防御/盾反）

- 弹反窗口内：按 `knockback` 选择 `Deflect_Slash` / `Deflect_HeavySlash`，增加攻击者架势。
- 窗口外仍在防御：按 `knockback` 选择 `Hurt_Guard` / `Hurt_GuardHeavy`，只增加防守者架势。
- 未格挡受击：按 `knockback` 选择 `Hurt_Ground` / `Hurt_Heavy`。
- 普通格挡不会增加攻击者架势；只有完美弹反会。
- 完美弹反造成攻击者架势崩解时，不再进入普通 `ParriedState`，改走弹反忍杀确认窗口。

### StunnedState（受击期间）
```csharp
protected override bool OnParentHandleHit(HitData hit) { return true; } // 二次受击拦截
```

### AttackState（攻击中被打）
默认 false → 会被打断进 StunnedState（只狼里被打就是打断）。

## 五、取消规则（攻击前摇 / 格挡连按）

命令仍走叶子 `HandleCommand`，不在 `GroundedState` 父层做取消表。

### 攻击前摇

- Hitbox：**进入 `AttackState` 即 `EnableWeaponHit`，退出即关**。刀碰到就算，不再等 `HitStartTime`。
- `stateTimer < HitStartTime`：允许格挡/垫步取消。
- `HitStartTime <= stateTimer < RecoveryWindowStart`：动作锁定，离散命令进入 0.2s 输入缓冲。
- `RecoveryWindowStart <= stateTimer <= ComboWindowEnd`：攻击接 `NextCombo`；移动、格挡、垫步、跳跃、喝药可立即取消。
- `ComboWindowEnd` 后不再接本段 `NextCombo`，尚未过期的命令由动作结束后的状态处理。
- `ComboWindowStart` 已更名为 `RecoveryWindowStart`，使用序列化迁移保留旧 SO 数值。
- `HitStartTime = 0`：进招不可取消（判定仍然一进攻击就开）。
- 攻击转向由 `AttackConfig` 的 `AllowRotation` / `RotationSpeed` / `RotationWindowEnd` 控制；锁定时追踪 Boss，未锁定时按移动输入转向。

### 格挡取消

- `DeflectState` 全程（抬刀 / 举刀 / 抖刀 / `Deflect_Slash` / `Deflect_Cancel`）：
  - `DeflectCommand` → 新的 `DeflectState`（重开弹反窗口；再按播 `Deflect_Repeat`，没有该状态则回退抬刀）
  - `DodgeCommand` → `DodgeState`
- 无论短按还是长按，松开格挡键都播放 `Deflect_Cancel`，播放结束后回待机。
- 连按格挡会走 `RegisterDeflectPress` 抖刀惩罚（0.5s 内 ≥3 次，窗口 ×0.75，下限 0.1s）。
- 垫步本身仍不可被打断。
- **锁定垫步**：`DodgeState` 按相对 Boss 的输入取最近四向，播一次性状态 `Dodge_Forward` / `Dodge_Back` / `Dodge_Left` / `Dodge_Right`。无输入默认后垫。斜向取绝对值更大的轴。不要用融合树（一次性 Root 混在一起会斜着滑）。未锁定仍播 `Dodge`。
- **起步**：`IdleToWalk` / `IdleToStrafe` / `DodgeToWalk` 若 Animator 里没有对应状态，直接播 `Walk` / `Walk_Strafe`（Boss 没有 `IdleToWalk` 即可）。
- **走着进格挡**：不播原地 `Deflect_Begin`（会掐步伐），约 0.22s 融合到 `Deflect_Walk` / `Deflect_Strafe` 并对齐步伐，抬刀靠这段融合。待机进格挡仍播抬刀，播完再进举刀循环。

## 六、StunnedState 设计（已有，M4 收尾）

- 顶层 HierarchicalState，`GetInitialSubState()` 统一进入 GroundStunnedState（空中受击已移除，空中被打也播地面受击）。
- 受击期间 `OnParentHandleCommand` 返回 true 吞掉所有命令。

## 七、处决/忍杀（M10）

- 架势崩解记录来源：`Attack` / `Deflect` / `Mikiri`，三类均通过事件总线显示忍杀红点。
- 攻击崩解：Boss 播 `Stagger_Broken`，范围内按攻击后双方播放 `Finsher_Ground`。
- 弹反崩解：Boss 播 `Stagger_Broken_Deflect`，玩家播 `DeflectToFinsher`；窗口内按攻击后双方播放 `Finsher_Deflect`。
- 识破崩解：Boss 立即播放专用 `Stagger_Broken_Miriki`，玩家现有 `Mikiri` 剩余动画作为确认窗口；按攻击后双方播放 `Finsher_Mikiri`。
- 弹反/识破窗口超时：Boss 架势从 100% 降到 80%，解除崩解并隐藏红点。
- 忍杀开始时按配置偏移对齐双方并隐藏红点，期间双方锁定命令与受击。
- 玩家忍杀动画命中帧调用 `CharacterBody.ExecuteFinisher()`，转发到 `CombatManager` 幂等清命；动画结束只做兜底与状态释放。

## 涉及文件

- 修改：`Assets/Scripts/FrameWork/States/Base/BaseState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Base/HierarchicalState.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（ReceiveHit 实现）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`（锁定四向垫步）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/AttackState.cs`（进攻击开判定 + 前摇取消）
- 修改：`Assets/Scripts/SO/AttackConfig.cs`（`HitStartTime`）
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`
