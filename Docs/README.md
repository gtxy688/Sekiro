# ARPG 战斗 Demo 文档导航

复刻只狼弦一郎 Boss 战。AI 辅助开发，用户按模块验收。

## 文档层级

- **设计总纲**：[`Docs/策划案.md`](策划案.md) —— 玩法/机制/数值/范围的**唯一权威总纲**
- **进度总览**：[`Docs/progress.md`](progress.md) —— 已完成的模块 / 待办模块 / 下一步
- **用户决策记录**：[`Docs/total.md`](total.md) —— 框架决策、用户视角
- **模块架构**：`Docs/architecture/` 下 `0X-xxx.md`
- **验收清单**：`Docs/architecture/` 下 `0X-xxx-test.md`

## 模块 → 规格 → 验收清单 总表

> 状态图例：✅ 代码完成 / 🔶 半成品 / ❌ 未做（或未接线）

| 模块 | 架构文档 | 验收清单 | 代码状态 |
|------|---------|---------|---------|
| M1 状态机/受击路由 | `01-states.md` | `01-states-test.md` | ✅ 代码完成 |
| M2 战斗数据 | `02-combat-data.md` | `02-combat-data-test.md` | ✅ 代码完成 |
| M3 命中判定 | `03-hit-detection.md` | `03-hit-detection-test.md` | ✅ 代码完成 |
| M4 弹反/格挡 | `01-states.md` | `01-states-test.md` | 🔶 半成品（无弹反窗口） |
| M5 行为树 | `04-behavior-tree-ai.md` | `04-behavior-tree-ai-test.md` | 🔶 骨架（无黑板/Running） |
| M6 输入 | `05-input-lockon.md` | `05-input-lockon-test.md` | ✅ 代码完成 |
| M7 Boss AI | `04-behavior-tree-ai.md` | — | 🔶 简版（近战+追击） |
| M8 动画事件 | `07-anim-events.md` | `07-anim-events-test.md` | ❌ 未接线（攻击打不出伤害，硬伤） |
| M9 架势崩解 | `02-combat-data.md` | `02-combat-data-test.md` | 🔶 半成品（无 EndureState） |
| M10 处决/忍杀 | `01-states.md` | `01-states-test.md` | ❌ 未做 |
| M11 锁定 | `05-input-lockon.md` | `05-input-lockon-test.md` | 🔶 半成品（无 LockOnManager） |
| M12 相机 | `06-presentation.md` | `06-presentation-test.md` | 🔶 半成品（自研 OrbitCamera，Cinemachine 待写） |
| M13 UI | `06-presentation.md` | `06-presentation-test.md` | ✅ 框架 |
| M14 复活 | `02-combat-data.md` | `02-combat-data-test.md` | 🔶 半成品（无回生流程） |
| M15 音效 | `06-presentation.md` | `06-presentation-test.md` | ✅ 框架 |
| M16 葫芦 | `02-combat-data.md` | `02-combat-data-test.md` | 🔶 半成品（即喝即回，无动画） |
| M17 危字/识破 | `03-hit-detection.md` | `03-hit-detection-test.md` | ✅ 代码完成（突刺/识破；横扫待做） |

## 验收流程

1. AI 交付模块时附「验收清单」
2. 用户在 Unity 打开场景，逐条操作验证
3. 验收通过 → AI 提交；不通过 → 返回修正

## 美术资源

美术资源（`Assets/Resources/`、`Assets/Scenes/简洁/`）不上传 git，通过网盘分享。详见 `.gitignore`。
