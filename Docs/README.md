# 文档索引

## 事实来源与阅读顺序

当前实现事实以 Unity 工程中的代码、序列化资源和场景为准。本文档与架构说明用于导航；若说明和当前实现不符，以当前实现为准，并修订活跃文档。

建议先读项目总览，再按关注模块阅读架构说明。以 *-test.md 结尾的文件是 Unity 手动验收清单，不是自动化测试说明。

## 当前项目说明

| 文档 | 用途 |
|---|---|
| ../Agents.md | 唯一 Agent 项目指引 |
| architecture/00-overview.md | 代码模块关系与依赖概览 |
| progress.md | 代码模块存在情况；不记录未经确认的用户验收状态 |
| 实现指导.md | 当前跨模块调用路径与实现边界 |
| 玩家指南.md | 玩家键鼠与手柄操作 |
| architecture/04-boss-ai-move-assets.md | Boss 动画资源对照；运行时招式以 BossMoveTable 与代码为准 |

## 架构说明与手动验收

| 模块 | 架构说明 | 手动验收 |
|---|---|---|
| 状态机与受击 | architecture/01-states.md | architecture/01-states-test.md |
| 战斗数据 | architecture/02-combat-data.md | architecture/02-combat-data-test.md |
| 命中判定 | architecture/03-hit-detection.md | architecture/03-hit-detection-test.md |
| Boss 行为树 | architecture/04-behavior-tree-ai.md | architecture/04-behavior-tree-ai-test.md |
| 输入与锁定 | architecture/05-input-lockon.md | architecture/05-input-lockon-test.md |
| UI、音效与相机 | architecture/06-presentation.md | architecture/06-presentation-test.md |
| 攻击动画时间轴 | architecture/07-anim-events.md | architecture/07-anim-events-test.md |
| 复战复位、阵营、时间尺度 | 对应说明分散在 Combat、Mgr 与 CharacterBody 源码中 | architecture/08-encounter-reset-test.md、09-faction-test.md、10-timescale-faction-test.md |

## 历史与参考资料

以下内容保留作决策背景、评审记录或外部参考，不作为当前源码事实：

- total.md：用户视角的历史决策记录。
- superpowers/plans/ 与 superpowers/specs/：历史计划与设计快照；其中的待办状态可能已过期。
- editor-review/：特定日期的代码评审、工具检查和求职准备记录。
- references/sekiro-genichiro-ai.md：外部玩法与动画研究参考，不是项目实现说明。

## 维护规则

- 修改功能后，只同步受影响的活跃说明和对应手动验收清单。
- 新增架构事实必须能从当前源码、资源或场景中定位；计划和推测要明确标注。
- 不把“代码中存在”写成“用户已验收”，也不把历史计划中的 TODO 当作当前待办。
