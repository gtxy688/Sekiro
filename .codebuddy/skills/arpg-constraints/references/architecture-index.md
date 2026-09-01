# 项目架构索引（模块清单 + 依赖图）

来源：`Docs/architecture/00-overview.md`。开发任何模块前先在此定位模块归属，再读对应架构文档。

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
| M17 | 危字攻击：突刺/投技 + 识破/弹反 | `03-hit-detection.md` |

> M17（危字攻击 + Mikiri 识破 + 肘击投技 Grab）与闪避（垫步）已确认保留。横扫已删除（未实现跳踩反制）。

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

## 关键实现决策速查（以 `Docs/实现指导.md` 为准）

- **弹反模型**：按下即开 0.3s 弹反窗口，窗口内挨打=弹反，窗口后按住=格挡，短按(<0.15s)松手播弹反专属动画；抖刀惩罚 0.5s 内连点 ≥3 次窗口 ×0.75。
- **M8 攻击接线**：代码驱动——`AttackConfig` 加"判定开始/结束时间"字段，AttackState 按计时开关判定（不用动画事件）。
- **玩家/Boss 共用状态**：两个 Animator 用**同名状态名**（Idle/Walk/Hurt_Ground/Attack…），各绑各的动画 Clip，代码不改。
- **Boss 架势回复**：`回复速度 = 基础(≈5/s) × (1 − 当前架势/最大架势)`，反函数手感。
- **hitstop**：命中 ~0.05s、完美弹反 ~0.1s，只冻战斗时间，UI/相机不冻。
- **回生确认键**：与攻击键共用（左键/RT）；回满血 + 架势清零 + 短暂无敌帧，Boss 不重置。
- **射箭**：投射物 prefab，匀速直线 + 上一帧→当前帧 SphereCast，开火瞬间锁定玩家胸口，不追踪。
