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
│     ├─ DeflectState       ← 弹反
│     ├─ DodgeState         ← 闪避
│     └─ MikiriCounterState ← 识破（M17）
├─ AirState（父状态）
│  └─ SubStateMachine
│     ├─ JumpState / FallState
│     ├─ AirAttackState
│     └─ AirDeflectState
└─ StunnedState（父状态）
   └─ SubStateMachine
      ├─ GroundStunnedState（地面受击）
      └─ AirStunnedState（空中受击）
```

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

## 三、Hit 路由（M1，待实现）

镜像 Command 路由，让受击结算能查到"当前在弹反吗/闪避吗"。

### HitData 定义

```csharp
public struct HitData
{
    public CharacterBody attacker;   // 攻击者
    public int healthDmg;            // 血量伤害
    public float postureDmg;         // 架势伤害
    public Vector3 hitPoint;         // 命中点
    public bool isPerilous;          // 是否危字攻击（M17）
    public PerilousType perilousType; // 危字类型（M17）
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

### DeflectState（地面弹反）
```csharp
public override bool OnHitReceived(HitData hit)
{
    if (IsInDeflectWindow)  // 弹反窗口内
    {
        HandlePerfectParry(hit); // 触发弹反：涨对方架势 + 事件总线发"叮"声
        return true;
    }
    return false; // 窗口过了 → 硬吃
}
```

### DodgeState（闪避）
```csharp
public override bool OnHitReceived(HitData hit)
{
    if (IsInvincibleFrames) return true;  // 无敌帧吞掉
    return false;
}
```

### StunnedState（受击期间）
```csharp
protected override bool OnParentHandleHit(HitData hit) { return true; } // 二次受击拦截
```

### AttackState（攻击中被打）
默认 false → 会被打断进 StunnedState（只狼里被打就是打断）。

## 五、StunnedState 设计（已有，M4 收尾）

- 顶层 HierarchicalState，`GetInitialSubState()` 按 `body.IsGrounded` 分派到 Ground/Air 受击。
- 空中受击结束：仍按 IsGrounded 决定去向。
- 受击期间 `OnParentHandleCommand` 返回 true 吞掉所有命令。

## 六、处决/忍杀（M10）

- 架势崩解 → 对方进 EndureState（崩解硬直）。
- 玩家进入攻击范围 → 交互键触发忍杀。
- 忍杀动画 → 动画事件调用 `CombatManager.ExecuteFinisher()` 清空一条命。
- 不新增独立状态机，靠 EndureState + 动画事件。

## 涉及文件

- 修改：`Assets/Scripts/FrameWork/States/Base/BaseState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Base/HierarchicalState.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（ReceiveHit 实现）
- 修改：`Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/DodgeState.cs`
- 修改：`Assets/Scripts/FrameWork/States/StunnedState.cs`
