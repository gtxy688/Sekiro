# ARPG 战斗 Demo 文档导航

复刻只狼弦一郎 Boss 战。AI 辅助开发，用户按模块验收。

## 文档层级

- **进度总览**：[`Docs/progress.md`](progress.md) —— 已完成的模块 / 待办模块 / 下一步
- **用户决策记录**：[`Docs/total.md`](total.md) —— 框架决策、用户视角
- **模块架构**：`Docs/architecture/` 下 `0X-xxx.md`
- **验收清单**：`Docs/architecture/` 下 `0X-xxx-test.md`

## 模块 → 规格 → 验收清单 总表

| 模块 | 架构文档 | 验收清单 | 代码状态 |
|------|---------|---------|---------|
| M1 状态机/受击 | `01-states.md` | `01-states-test.md` | ⬜ 受击路由待做 |
| M2 战斗数据 | `02-combat-data.md` | `02-combat-data-test.md` | ✅ 完成 |
| M3 命中判定 | `03-hit-detection.md` | `03-hit-detection-test.md` | ⬜ |
| M5 行为树 | `04-behavior-tree-ai.md` | `04-behavior-tree-ai-test.md` | 🔶 骨架 |
| M6 输入/锁定 | `05-input-lockon.md` | `05-input-lockon-test.md` | 🔶 移动通 |
| M7 Boss AI | `04-behavior-tree-ai.md` | — | ⬜ 最后做 |
| M12 相机 | `06-presentation.md` | `06-presentation-test.md` | 🔶 包已装，代码待写 |
| M13 UI | `06-presentation.md` | `06-presentation-test.md` | ✅ 框架 |
| M15 音效 | `06-presentation.md` | `06-presentation-test.md` | ✅ 框架 |
| M8 动画事件 | `07-anim-events.md` | `07-anim-events-test.md` | ⬜ |
## 验收流程

1. AI 交付模块时附「验收清单」
2. 用户在 Unity 打开场景，逐条操作验证
3. 验收通过 → AI 提交；不通过 → 返回修正

## 美术资源

美术资源（`Assets/Resources/`、`Assets/Scenes/简洁/`）不上传 git，通过网盘分享。详见 `.gitignore`。
