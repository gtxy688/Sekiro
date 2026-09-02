# Unity 自动化测试做法（EditMode / PlayMode）

> 基于动作类项目实践提炼。项目未启用自动化测试时，本文件内容不适用，走纯手动验收清单。

## 测试分层

| 层 | 目录 | 测什么 | 特点 |
|----|------|--------|------|
| EditMode 单元测试 | `Assets/Tests/EditMode/` | 数值逻辑、纯 C# 逻辑（SO 配置、伤害计算、架势系统、状态机切换） | 不进 Play，秒级运行 |
| PlayMode 集成测试 | `Assets/Tests/PlayMode/` | 状态转换、场景装配、核心闭环链路 | 进 Play 模式，可 `yield return` |

**覆盖边界约定（写进项目测试策略）：**
- 新增数值逻辑 → 必须配套 EditMode 单测
- 新增状态转换 → 必须配套 PlayMode 集成测试
- 修改已有逻辑前 → 先运行现有测试确保不回归
- 深度场景测试用**简化策略**：PlayMode 只测「场景装配无异常 + 关键事件触发」，完整手感由手动验收清单覆盖

## 运行方式

Unity Editor → Window → General → Test Runner → EditMode / PlayMode 选项卡运行。

## 测试命名

- 测试类：`<被测类>Test`（`CombatConfigValidationTest`、`PostureSystemTest`、`StateMachineTest`）
- 测试方法：`<行为>_<预期>`（`TransitionTo_SwitchesState`、`BossDefenseProbabilities_SumNotExceedOne`）
- 集成测试类：`<系统>IntegrationTest`（`CombatIntegrationTest`）

## EditMode 单测写法

### 1. SO 数据校验（用 `CreateInstance`，无需创建资产文件）

```csharp
using NUnit.Framework;
using UnityEngine;

/// <summary>数据层合法性测试：概率和必须 ≤1，伤害非负。</summary>
public class CombatConfigValidationTest
{
    [Test]
    public void BossDefenseProbabilities_SumNotExceedOne()
    {
        var cfg = ScriptableObject.CreateInstance<CombatConfig>();
        Assert.LessOrEqual(cfg.bossParryChance + cfg.bossBlockChance, 1f);
    }

    [Test]
    public void DefaultAttack_HasPositiveDamage()
    {
        var cfg = ScriptableObject.CreateInstance<CombatConfig>();
        Assert.IsNotNull(cfg.playerAttack);
    }
}
```

要点：
- `ScriptableObject.CreateInstance<T>()` 直接测配置类的默认值，**不依赖磁盘上的 .asset 文件**
- 配置缺失字段时测试先 FAIL，逼出「该字段必须有默认值/必须注入」的修正——这是设计反馈，不是测试写错

### 2. 纯逻辑类单测（伤害计算 / 架势系统）

```csharp
public class PostureSystemTest
{
    [Test]
    public void AddDamage_ClampsAtMax()
    {
        var ps = new PostureSystem(100f);
        ps.AddDamage(30f);
        Assert.AreEqual(30f, ps.Current);

        ps.AddDamage(999f);
        Assert.AreEqual(100f, ps.Current);
        Assert.IsTrue(ps.IsBroken);
    }
}
```

要点：被测逻辑**必须是纯 C# 类（非 MonoBehaviour）**才能 EditMode 单测。MonoBehaviour 只做挂载与转调，核心计算抽出来——这同时满足架构上的可测性要求。

### 3. 状态机切换单测（用私有测试状态类）

```csharp
public class StateMachineTest
{
    private class TestStateA : State { public override void Enter() { } public override void Execute() { } public override void Exit() { } }
    private class TestStateB : State { public override void Enter() { } public override void Execute() { } public override void Exit() { } }

    [Test]
    public void TransitionTo_SwitchesState()
    {
        var sm = new StateMachine();
        sm.AddState<TestStateA>();
        sm.AddState<TestStateB>();
        sm.TransitionTo<TestStateA>();
        Assert.IsTrue(sm.CurrentState is TestStateA);
        sm.TransitionTo<TestStateB>();
        Assert.IsTrue(sm.CurrentState is TestStateB);
    }

    [Test]
    public void GetState_ReturnsInstance()
    {
        var sm = new StateMachine();
        sm.AddState<TestStateA>();
        Assert.IsNotNull(sm.GetState<TestStateA>());
    }
}
```

要点：用私有 `TestStateA/B` 最小实现被测接口，测试只关心**框架行为**（切换、获取），不依赖任何业务状态。

## PlayMode 集成测试写法

```csharp
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>M1 核心战斗闭环集成测试。</summary>
public class CombatIntegrationTest
{
    [UnityTest]
    public IEnumerator PlayerAttack_AddsBossPosture()
    {
        // 搭建最小场景：仅包含被测链路需要的对象
        var player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.tag = "Player";
        var boss = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boss.tag = "Boss";
        boss.transform.position = new Vector3(2f, 0f, 0f);

        yield return null;

        Assert.IsNotNull(player);
        Assert.IsNotNull(boss);
    }
}
```

要点：
- `[UnityTest]` + 返回 `IEnumerator`，用 `yield return null` 让 Unity 跑一帧
- **简化策略**：完整装配全套组件成本高，PlayMode 以「场景装配无异常 + 关键事件触发」为验收目标，深度交互留给用户手动验收（符合「AI 实现 → 用户验收」工作流）

## 常见反模式

| 反模式 | 表现 | 修正 |
|--------|------|------|
| 测试依赖磁盘资产 | 测试读 `Resources.Load` / 场景引用，缺资产就挂 | 用 `CreateInstance<T>()` / 代码内建对象 |
| 测 MonoBehaviour | EditMode 无法直接 new，测试复杂化 | 核心逻辑抽纯 C# 类再测 |
| 测全部逻辑 | 表现层、输入、动画全部试图自动化 | 自动化只覆盖数值与状态转换，手感走手动清单 |
| 先改代码后写测试防回归 | 改已有逻辑不跑旧测试 | 任务开始/修改前先跑一次 Test Runner |
| PlayMode 试图完整复刻战斗 | 装配成本爆炸、测试脆弱 | 简化策略：装配无异常 + 关键事件触发即可 |

## 提交约定

测试与实现代码**同批提交**，提交信息带 `test:` 前缀：

```bash
git add Assets/Scripts/Combat/PostureSystem.cs Assets/Tests/EditMode/PostureSystemTest.cs
git commit -m "test: PostureSystem 单测（含实现）"
```