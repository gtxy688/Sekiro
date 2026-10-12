# 《苇名之刃》架构学习 Resources

原则：**本项目源码是第一事实来源**；下面的外部资料只在"补概念、对照通用做法、准备面试表达"时使用。凡与源码冲突，以源码为准。

## Knowledge（外部权威资料）

- [Game Programming Patterns — State（含 FSM/HFSM 讨论）](http://gameprogrammingpatterns.com/state.html)
  免费在线书。用于：理解"大量 bool → FSM → 分层状态机"的通用推导；对照本项目 HFSM 的取舍。**注意**：书中把状态写成对象/类的做法与本项目一致，但它不涉及本项目的 Command 路由，别拿书里的结论套项目。
- [Game Programming Patterns — Command](https://gameprogrammingpatterns.com/command.html)
  用于：Command 模式的标准动机（把意图对象化、可排队/可撤销）。对照本项目"Command 是意图、不是执行"的语义。
- [Game Programming Patterns — Bytecode / Behavior Tree 相关章节（Behavioral Patterns 索引）](https://gameprogrammingpatterns.com/contents.html)
  用于：行为树、类型对象等模式的全书目录入口，按需查阅。
- [Unity Manual (2022.3) — ScriptableObject](https://docs.unity3d.com/2022.3/Documentation/Manual/class-ScriptableObject.html)
  用于：确认 SO 的内存/序列化语义，解释"战斗数值为什么放 SO 而不是硬编码"。
- [Unity Scripting API — ScriptableObject](https://docs.unity3d.com/2022.2/Documentation/ScriptReference/ScriptableObject.html)
  用于：确认 MonoBehaviour 引用 SO 时的加载行为与序列化规则。
- [Unity Input System 1.14 官方文档（How do I…?）](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/HowDoI.html)
  用于：Action / Binding / Rebinding 的官方做法，对照本项目改键服务的实现。
- [Unity Input System 1.14 — Workflows](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Workflows.html)
  用于：区分"Action 引用式"与"直接读设备"两种工作流，判断本项目属于哪一种。
- [Cinemachine 官方文档（Blending between Virtual Cameras）](https://docs.unity3d.com/Packages/com.unity.cinemachine@2.7/manual/CinemachineBlending.html)
  用于：理解 Virtual Camera 的 Priority/Blend 机制，解释锁定与普通相机的切换方式。
- [Cinemachine 包文档入口](https://docs.unity3d.com/Packages/com.unity.cinemachine@2.3/manual/index.html)
  用于：跟随/构图/切换的通用概念速查。
- [Unity Manual — Physics 查询（Cast / Overlap 系列）](https://docs.unity3d.com/2022.3/Documentation/Manual/PhysicsOverview.html)
  用于：确认 SphereCast/BoxCast 与 Overlap 的差别（本项目命中判定为何用扫描而非触发器）。

## Wisdom（社区与真人反馈）

- [Unity Discussions（官方论坛）](https://discussions.unity.com/)
  用于：状态机/事件总线/命中判定这类具体实现的追问，以及"我的做法是否常见"的横向对照。
- [r/Unity3D](https://www.reddit.com/r/Unity3D/)
  用于：作品集与求职视角的反馈（Demo 如何展示、面试官会问什么）。
- [只狼 Wiki — Combat / Posture 机制](https://sekiroshadowsdietwice.fandom.com/wiki/Combat)
  用于：核对"架势、崩解、危字、识破"的原作规则，避免把机制讲反。
- **用户的具体社区偏好尚未确认**（未表态）。若用户表示不加入社区，此节应改记为"已选择不参与社区"。

## Gaps

- 尚无"Unity 客户端秋招面试真题/题频"的可信来源（企业题频多为营销内容，不予采信）；面试表达部分以本项目源码 + 通用 C#/Unity 官方文档为准。
- 尚无 Profiler 实测数据：所有性能相关结论只能停在"潜在分配点"，不能升级为"已确认瓶颈"。
- 本项目的"命中判定用扫描而非触发器""自研行为树"缺少公开权威对照文章，暂以源码 + Unity 物理文档互相印证。
