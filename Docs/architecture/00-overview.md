# 项目架构总览

复刻只狼弦一郎 Boss 战。AI 辅助开发，用户按模块验收。

> 本文档是给 AI 开发者看的索引。用户决策记录见 `../total.md`。

## 技术栈

- Unity 2022 LTS + URP
- 输入：Input System
- 相机：Cinemachine
- UI：MVC + DoTween
- 数据：ScriptableObject
- 战斗驱动：命令模式 (Command Pattern) + HFSM + 行为树
- 表现：轻量事件总线 (CombatEventBus) + FX/音效管理器

## 模块清单（17 个）

### 基础层（无依赖，可并行开发）
| 模块 | 内容 | 文档 |
|------|------|------|
| M1 | HFSM 基架：OnHitReceived 路由 + HitData | `01-states.md` |
| M2 | 战斗属性：CharacterConfig SO + HP/架势/TakeDamage | `02-combat-data.md` |
| M5 | 行为树：黑板 + Running 记忆 | `04-behavior-tree-ai.md` |

### 战斗层（依赖 M1/M2）
| 模块 | 内容 | 文档 |
|------|------|------|
| M3 | 命中判定：BoxCast Hitbox/Hurtbox + CombatManager | `03-hit-detection.md` |
| M4 | 受击/盾反：各状态 OnHitReceived 实现 | `01-states.md` |
| M9 | 架势系统：Posture 累积/崩解 | `02-combat-data.md` |
| M10 | 处决/忍杀：忍杀动画 + 清命 | `01-states.md` |
| M17 | 危字攻击：突刺/横扫 + 识破/跳踩 | `03-hit-detection.md` |

> M17（危字攻击 + Mikiri 识破）与闪避（垫步）已确认**保留**（用户已解包对应动画资源）。横扫危字（跳踩反制）待实现。设计见 `../策划案.md`。

### 控制层（依赖 M1）
| 模块 | 内容 | 文档 |
|------|------|------|
| M6 | 玩家输入：全按键绑定 | `05-input-lockon.md` |
| M11 | 锁定系统：Lock-on | `05-input-lockon.md` |

### 表现层（依赖战斗层事件）
| 模块 | 内容 | 文档 |
|------|------|------|
| M12 | 相机：Cinemachine 锁定 + 震屏 | `06-presentation.md` |
| M13 | UI：MVC 布局 | `06-presentation.md` |
| M14 | 复活：1 次回生回满血 | `02-combat-data.md` |
| M15 | 音效：格挡/弹反池 + 受击/处决 | `06-presentation.md` |
| M16 | 葫芦：10 次回血 | `02-combat-data.md` |

### AI 层（依赖 M5/M3）
| 模块 | 内容 | 文档 |
|------|------|------|
| M7 | Boss AI：弦一郎 | `04-behavior-tree-ai.md` |

### 集成层（依赖 M3）
| 模块 | 内容 | 文档 |
|------|------|------|
| M8 | 动画事件：Hitbox 开关 + 忍杀帧 | `07-anim-events.md` |

## 依赖图

```
M1 ─┬─→ M2 ──→ M3 ──→ M4 ──→ M9 ──→ M10
    │         │
    │         └─→ M6 ──→ M11
    │
M5 ─┴─→ M7
M3 ──────→ M8

表现层 M12/M13/M14/M15/M16 订阅 CombatEventBus 事件
```

## 文档阅读指引

- **架构理解**：本文件（00-overview.md）→ 对应模块的 `0X-xxx.md`
- **验收测试**：实现后对照 `0X-xxx-test.md` 逐条验证
- **用户决策**：`../total.md`（用户视角，AI 不要当规范用）
