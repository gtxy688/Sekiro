# 测试计划

框架：Unity Test Framework (EditMode + PlayMode)

## EditMode 单元测试

| 被测系统 | 测试文件 | 覆盖场景 |
|---------|---------|---------|
| DeflectSystem | DeflectSystemTest.cs | 弹刀窗口/抖刀/加成链 |
| PostureSystem | PostureSystemTest.cs | 增加/恢复/崩溃/计时器 |
| DamageCalculator | DamageCalculatorTest.cs | 伤害计算/格挡减伤/弹刀加成 |
| InputBuffer | InputBufferTest.cs | 缓冲窗口/超时/优先级 |
| PlayerCombatController | PlayerCombatControllerTest.cs | 受击/弹刀转发/回血 |

## PlayMode 集成测试

| 被测系统 | 测试文件 | 覆盖场景 |
|---------|---------|---------|
| PlayerStateMachine | PlayerStateMachineTest.cs | 状态转换/优先级打断 |
| BossStateMachine | BossStateMachineTest.cs | AI 决策/距离判断 |
| CombatIntegration | CombatIntegrationTest.cs | 弹刀→伤害→架势→事件的完整链路 |

## 规范

- 方法命名：`[方法]_[条件]_[期望结果]`
- 结构：Arrange → Act → Assert（AAA）
- 避免 `Time.deltaTime`，用固定值模拟
- 避免加载真实资产，用 `ScriptableObject.CreateInstance`
- 每个测试只验证一件事

## 覆盖率目标

| 模块 | 目标 |
|------|------|
| DeflectSystem | 90%+ |
| PostureSystem | 90%+ |
| DamageCalculator | 90%+ |
| InputBuffer | 80%+ |
| BossStateMachine | 70%+ |
| UI/Camera | 手工验证 |

## 执行时机

| 时机 | 执行 |
|------|------|
| 新功能 | 先写测试 → 再实现 → 实时运行 |
| 修改前 | 先跑现有测试 → 不回归 |
| 提交前 | 全部 EditMode 测试 |
| 里程碑 | 全部 EditMode + PlayMode |
