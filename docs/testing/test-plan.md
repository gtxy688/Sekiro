# 测试计划

## 测试框架

- Unity Test Framework（EditMode + PlayMode）
- 运行方式：Unity Editor → Window → General → Test Runner

## 测试分层

### EditMode 单元测试

不需要运行游戏，用于测试纯逻辑：数值计算、状态转换条件、配置读取。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| DeflectSystem | DeflectSystemTest.cs | 弹刀窗口计算、抖刀惩罚递减、成功弹刀重置、连续加成链 |
| PostureSystem | PostureSystemTest.cs | 架势增加、恢复速率、崩溃判定、脱战恢复计时器 |
| DamageCalculator | DamageCalculatorTest.cs | 伤害计算、格挡减伤、弹刀伤害加成 |
| InputBuffer | InputBufferTest.cs | 缓冲窗口、超时清空、优先级排序 |
| LightningCounterSystem | LightningSystemTest.cs | 状态流转（None→Falling→Charged→Reflected/Failed）、输入超时 |

### PlayMode 集成测试

需要运行游戏，用于测试 Unity 组件交互：状态机转换、动画事件触发、物理检测。

| 被测系统 | 测试文件 | 必须覆盖的场景 |
|---------|---------|---------------|
| PlayerStateMachine | PlayerStateMachineTest.cs | 状态转换（Idle→Attack→Deflect→Hit）、优先级打断 |
| BossStateMachine | BossStateMachineTest.cs | AI 决策（距离判断、招式选择）、阶段转换 |
| CombatIntegration | CombatIntegrationTest.cs | 弹刀触发伤害计算+架势变化+事件广播的完整链路 |

## 测试编写规范

### 命名规则

测试方法命名：`[被测方法]_[输入条件]_[期望结果]`

例：
- `GetCurrentDeflectWindow_After3Spams_WindowReduced`
- `AddPosture_ExceedsMax_TriggersBreak`

### 测试结构

```csharp
[Test]
public void MethodName_Condition_ExpectedResult()
{
    // Arrange - 准备测试数据和依赖
    var system = CreateTestSystem();
    
    // Act - 执行被测操作
    var result = system.MethodUnderTest(input);
    
    // Assert - 验证结果
    Assert.AreEqual(expected, result);
}
```

### 禁止事项

- 避免在测试中使用 `Time.deltaTime`，用固定值模拟
- 避免在测试中加载真实资产，使用 Mock 或 ScriptableObject.CreateInstance
- 每个测试只验证一件事

### 测试辅助方法

```csharp
// 在测试基类或工具类中提供
protected static DeflectSystem CreateTestDeflectSystem()
{
    var config = ScriptableObject.CreateInstance<DeflectConfig>();
    config.baseDeflectWindow = 0.2f;
    // ... 设置默认值
    return new DeflectSystem(config);
}

protected static AttackData CreateTestAttackData(float damage = 100f, float postureDamage = 15f)
{
    return new AttackData
    {
        damage = damage,
        postureDamage = postureDamage,
        attackType = AttackType.Normal,
        canBeDeflected = true,
        // ... 其他默认值
    };
}
```

## 覆盖率目标

| 模块 | 目标覆盖率 | 说明 |
|------|-----------|------|
| DeflectSystem | 90%+ | 核心战斗逻辑，必须高覆盖 |
| PostureSystem | 90%+ | 核心战斗逻辑，必须高覆盖 |
| DamageCalculator | 90%+ | 核心战斗逻辑，必须高覆盖 |
| InputBuffer | 80%+ | 重要但逻辑相对简单 |
| LightningSystem | 80%+ | 状态流转需覆盖 |
| BossStateMachine | 70%+ | AI 逻辑复杂，覆盖关键路径 |
| UI/Camera | 手工验证 | 不要求自动化测试 |

## 测试执行时机

| 时机 | 执行内容 |
|------|---------|
| 编写新功能时 | 先写测试，再写实现，实时运行 EditMode 测试 |
| 修改已有代码前 | 先运行现有测试，确保不回归 |
| 提交前 | 运行全部 EditMode 测试 |
| 重要里程碑 | 运行全部测试（EditMode + PlayMode） |

## 测试文件组织

```
Assets/Tests/
├── EditMode/
│   ├── DeflectSystemTest.cs
│   ├── PostureSystemTest.cs
│   ├── DamageCalculatorTest.cs
│   ├── InputBufferTest.cs
│   └── LightningSystemTest.cs
├── PlayMode/
│   ├── PlayerStateMachineTest.cs
│   ├── BossStateMachineTest.cs
│   └── CombatIntegrationTest.cs
└── TestUtilities/
    ├── TestHelpers.cs          ← 测试辅助方法
    └── MockObjects.cs          ← Mock 对象
```
