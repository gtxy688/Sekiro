# ARPG 战斗 Demo 文档导航

复刻只狼弦一郎 Boss 战。AI 辅助开发，用户按模块验收。

## 文档层级

| 层 | 位置 | 用途 |
|----|------|------|
| L0 规则 | `CLAUDE.md` | 架构红线 + 代码规范 + 访问纪律 |
| L1 架构 | `Docs/architecture/` | 技术选型与设计决策 |
| L2 规格 | `Docs/specs/` | 各模块机制 + 参数 + 验收清单 |
| L3 参考 | `Docs/references/` | 原始素材（天守阁弦一郎 AI 分析） |
| L4 计划 | `Docs/superpowers/` | 设计规格 + 实现计划 |

## 模块 → 规格 → 验收清单 总表

| 模块 | 规格文档 | 验收清单 |
|------|---------|---------|
| 弹刀 | `specs/deflect/deflect-mechanics.md` | `deflect-params.md` 末尾 |
| 架势 | `specs/posture/posture-rules.md` | `posture-params.md` 末尾 |
| Boss 状态机 | `specs/boss/boss-state-machine.md` | 各状态 Enter/Execute 验收 |
| Boss AI | `specs/boss/boss-ai-decision.md` | `boss-params.md` 末尾 |
| 危字 | `specs/danger/danger-types.md` | `danger-params.md` 末尾 |
| 输入 | `specs/input/input-priority.md` | 各 input spec 末尾 |
| 动画 | `specs/animation/animation-strategy.md` | `animation-params.md` 末尾 |
| UI | `specs/ui/ui-overview.md` | `ui-damage-numbers.md` 末尾 |

## 验收流程

1. AI 交付模块时附「验收清单」
2. 用户在 Unity 打开场景，逐条操作验证
3. 验收通过 → AI 提交；不通过 → 返回修正

## 美术资源

美术资源（`Assets/Resources/`、`Assets/Scenes/简洁/`）不上传 git，通过网盘分享。详见 `.gitignore`。
