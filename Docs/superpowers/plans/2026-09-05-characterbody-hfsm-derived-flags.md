# CharacterBody HFSM 派生状态标志实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 让 `CharacterBody.IsAttacking` 只从当前 HFSM 攻击叶子派生，消除可写快照与状态机不同步的风险。

**架构：** `AttackStateBase` 实现语义标记 `IAttackingState`。CharacterBody 私有地解析任意嵌套层级的当前叶子，再通过标记接口暴露只读 `IsAttacking`；BT、CombatStats、表现层仍只读取该 Facade 属性。

**技术栈：** Unity 2022 LTS、现有 HFSM、Unity Test Framework EditMode 测试。

**规格：** `Docs/superpowers/specs/2026-09-05-characterbody-hfsm-derived-flags-design.md`

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Scripts/FrameWork/States/Base/IAttackingState.cs` | 攻击叶子的语义标记 |
| 修改 `Assets/Scripts/FrameWork/States/Base/AttackStateBase.cs` | 声明攻击语义，删除快照写入 |
| 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs` | 提供私有叶子查询和只读 `IsAttacking`，删除强制清理写入 |
| 修改 `Assets/Scripts/FrameWork/Body/CombatStats.cs` | 删除死亡、清命时的重复攻击标志清理 |
| 修改 `Assets/Scripts/Boss/BossReviveBackoffState.cs` | 删除进入非攻击叶子时的重复清理 |
| 修改 `Assets/Scripts/Combat/DuelDirector.cs` | 删除投技切换前的重复清理 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/FinisherState.cs` | 删除进入忍杀叶子时的重复清理 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/FinisherVictimState.cs` | 删除进入忍杀受害叶子时的重复清理 |
| 修改 `Assets/Scripts/FrameWork/States/Ground/GrabThrowState.cs` | 删除进入投技叶子时的重复清理 |
| 创建 `Assets/Tests/Editor/CharacterBodyHfsmFlagTests.cs` | 锁定派生查询和无写入 API 的 EditMode 回归测试 |

### 任务 1：先写失败的状态机语义测试

**文件：**
- 创建：`Assets/Tests/Editor/CharacterBodyHfsmFlagTests.cs`

- [ ] **步骤 1：编写失败的测试**

测试中用真实 `CharacterBody`、`StateMachine` 和最小有效 `AttackConfig`。定义仅测试用的 `ExitProbeAttackState : AttackStateBase`，在调用 `base.OnExit()` 后记录 `body.IsAttacking`；再切到普通 `BaseState`。

```csharp
[Test]
public void IsAttacking_remains_true_while_the_current_attack_leaf_exits()
{
    var attack = new ExitProbeAttackState(body, config);
    body.MainStateMachine.ChangeState(attack);
    body.MainStateMachine.ChangeState(new PassiveState(body));

    Assert.That(attack.IsAttackingObservedDuringExit, Is.True);
    Assert.That(body.IsAttacking, Is.False);
}

[Test]
public void IsAttacking_is_not_externally_writable()
{
    var property = typeof(CharacterBody).GetProperty(nameof(CharacterBody.IsAttacking));
    Assert.That(property.CanWrite, Is.False);
}
```

这两条测试分别应抓住“OnExit 手工清快照”和“Facade 仍向外暴露 setter”两种破坏。

- [ ] **步骤 2：运行测试验证失败**

运行 Unity EditMode 测试，仅选择 `CharacterBodyHfsmFlagTests`。预期：第一条断言在 `AttackStateBase.OnExit()` 后观察到 false；第二条因当前属性含 setter 而失败。

### 任务 2：以语义标记派生 IsAttacking

**文件：**
- 创建：`Assets/Scripts/FrameWork/States/Base/IAttackingState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Base/AttackStateBase.cs`
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`

- [ ] **步骤 1：定义攻击状态标记**

```csharp
namespace ARPG.FrameWork.States.Base
{
    public interface IAttackingState
    {
    }
}
```

- [ ] **步骤 2：让攻击基类表达该语义**

```csharp
public abstract class AttackStateBase : BaseState, IAttackingState
```

删除 `AttackStateBase.OnEnter()` 中的 `body.IsAttacking = true;`，以及 `OnExit()` 中的 `body.IsAttacking = false;`；保留攻击阶段和武器判定清理。

- [ ] **步骤 3：替换 CharacterBody 快照属性**

```csharp
public bool IsAttacking => GetCurrentLeafState() is IAttackingState;

private BaseState GetCurrentLeafState()
{
    BaseState state = MainStateMachine?.CurrentState;
    while (state is HierarchicalState hierarchical &&
           hierarchical.SubStateMachine?.CurrentState is BaseState child)
    {
        state = child;
    }
    return state;
}
```

删除 CharacterBody 复战、弹反、识破、崩解、取消攻击入口中对 `IsAttacking` 的赋值；保留状态切换和其余资源清理。

- [ ] **步骤 4：运行测试验证通过**

运行相同 EditMode 测试。预期：两条均通过，且攻击叶子离开后 `IsAttacking` 为 false。

### 任务 3：删除其余重复写入并验证调用方保持 Facade 边界

**文件：**
- 修改：`Assets/Scripts/FrameWork/Body/CombatStats.cs`
- 修改：`Assets/Scripts/Boss/BossReviveBackoffState.cs`
- 修改：`Assets/Scripts/Combat/DuelDirector.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/FinisherState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/FinisherVictimState.cs`
- 修改：`Assets/Scripts/FrameWork/States/Ground/GrabThrowState.cs`

- [ ] **步骤 1：删除每个非攻击状态路径的写入**

仅移除 `IsAttacking = false;`。不要删除同一块中的 `DisableWeaponHit()`、`AttackUninterruptible = false`、`IsAttackRecoveryOpen = false`、攻击配置清理、`IsFinisherLocked` 或状态切换。

- [ ] **步骤 2：确认无写入残留**

运行：`rg -n --glob '*.cs' 'IsAttacking\\s*=' Assets`

预期：无输出。读取方（例如 BTBrain、GroundedState、DeflectMemory）继续使用 `body.IsAttacking`，不改为具体 State 类型判断。

- [ ] **步骤 3：运行完整 EditMode 测试与编译检查**

运行 Unity EditMode 测试并检查 Console。预期：测试通过，且没有本次变更引入的编译错误或未处理异常。

## 自检

| 规格条目 | 任务 |
| --- | --- |
| 攻击状态的唯一真相来源 | 2 |
| 对外维持 CharacterBody Facade | 2、3 |
| 删除所有 IsAttacking 写入 | 2、3 |
| 保留阶段与玩法标志 | 2、3 |
| 生命周期期间与切换后语义 | 1、2 |
| 不改动其他 flag 的所有权 | 2、3 |
