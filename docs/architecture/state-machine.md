# HFSM 框架设计

## 设计意图

分层状态机（HFSM）是本项目的核心架构模式，用于管理玩家和 Boss 的复杂行为。避免在 MonoBehaviour.Update 中使用 switch-case。

## 核心抽象

### StateMachine 基类

```csharp
/// <summary>
/// 状态机容器，管理当前状态和状态转换
/// </summary>
public class StateMachine
{
    protected State _currentState;
    private Dictionary<Type, State> _states = new Dictionary<Type, State>();
    
    public T AddState<T>() where T : State, new()
    {
        var state = new T();
        state.Initialize(this);
        _states.Add(typeof(T), state);
        return state;
    }
    
    public void TransitionTo<T>() where T : State
    {
        _currentState?.Exit();
        _currentState = _states[typeof(T)];
        _currentState.Enter();
    }
    
    public void Update()
    {
        _currentState?.Execute();
    }
}
```

### State 基类

```csharp
/// <summary>
/// 状态基类，定义生命周期方法
/// </summary>
public abstract class State
{
    protected StateMachine _stateMachine;
    
    public void Initialize(StateMachine stateMachine)
    {
        _stateMachine = stateMachine;
    }
    
    public virtual void Enter() { }
    public virtual void Execute() { }
    public virtual void Exit() { }
}
```

## 玩家状态机层级

```
PlayerStateMachine
│
├── GroundedState（地面状态）
│   ├── IdleSubState（待机）
│   ├── MoveSubState（移动）
│   └── LockOnMoveSubState（锁定移动）
│
├── AirborneState（空中状态）
│   ├── JumpSubState（跳跃）
│   └── FallSubState（下落）
│
└── CombatLayer（战斗层，高优先级，可打断基础层）
    ├── AttackState（攻击状态）
    │   ├── Startup（前摇）
    │   ├── Active（判定生效）
    │   └── Recovery（后摇）
    ├── DeflectState（弹刀状态）
    ├── DodgeState（闪避状态）
    ├── MikiriState（识破状态）
    ├── HitState（受击状态）
    ├── StunState（架势崩溃状态）
    ├── DeathblowState（忍杀状态）
    ├── HealingState（喝药状态）
    └── LightningChargeState（接雷状态）
```

## Boss 状态机层级

```
BossStateMachine
│
├── IdleState（待机）
├── MoveState（移动/追击）
├── AttackState（攻击）
│   ├── Startup
│   ├── Active
│   └── Recovery
├── RangedState（远程射箭，仅一阶段）
├── LightningState（雷电攻击，仅二阶段）
├── StaggerState（被弹刀硬直）
├── CollapseState（架势崩溃）
├── ExecutedState（被忍杀）
└── PhaseTransitionState（阶段转换）
```

## 状态转换规则

### 玩家状态转换

| 当前状态 | 触发条件 | 目标状态 |
|---------|---------|---------|
| 任意战斗状态 | 按下右键 | DeflectState |
| 任意战斗状态 | 按下 Shift（非突刺危字） | DodgeState |
| 任意战斗状态 | 按下 Shift（突刺危字） | MikiriState |
| AttackState.Recovery | 连按左键 | AttackState.Startup |
| AttackState.* | 按右键 | DeflectState |
| 任意状态 | 受到攻击且未弹刀 | HitState |
| StunState | 硬直结束 | GroundedState.Idle |
| 任意状态 | 崩溃敌人贴近 + 左键 | DeathblowState |

### 状态优先级

```
DeathblowState > StunState > HitState > LightningChargeState > 
HealingState > DeflectState > DodgeState > MikiriState > 
AttackState > GroundedState/AirborneState
```

高优先级状态可以打断低优先级状态。

## 实现指南

1. 每个状态一个文件，放在对应模块的 `States/` 目录下
2. 状态类继承 `State` 基类，重写 `Enter`、`Execute`、`Exit`
3. 状态转换通过 `_stateMachine.TransitionTo<T>()` 调用
4. 状态内部逻辑不要超过 100 行，复杂逻辑拆分到独立的系统类

## 与 Unity Animator 的关系

HFSM 管理游戏逻辑状态，Animator 管理动画播放。两者通过参数同步：

```csharp
// 在状态中设置 Animator 参数
public override void Enter()
{
    _animator.SetBool("isAttacking", true);
}

public override void Exit()
{
    _animator.SetBool("isAttacking", false);
}
```
