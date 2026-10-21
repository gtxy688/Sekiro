> 当前复习入口（2026-10-20）：[核心路线](Docs/项目复习/00-简历核心复习路线.md)与[唯一进度记录](Docs/项目复习/01-复习进度.md)。下文保留为历史材料，旧教学目标、阅读顺序、进度与能力评语不作为当前结论；引用的源码事实需要重新核对。

# 《苇名之刃》架构学习 Resources

原则：**本项目源码是第一事实来源**；下面的外部资料只在"补概念、对照通用做法、准备面试表达"时使用。凡与源码冲突，以源码为准。

## Knowledge（外部权威资料）

- [Unity 2022.3 — Order of execution for event functions](https://docs.unity3d.com/2022.3/Documentation/Manual/ExecutionOrder.html)
  2026-10-16 已核对 First Scene load 与 Before the first frame update。用于区分 Awake 的自身准备与 Start 的首次运行入口；不同对象的同阶段先后不能随意假定，运行时实例化需要单独考虑。
- [Unity 2022.3 — MonoBehaviour](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/MonoBehaviour.html)
  2026-10-16 已核对。用于理解 CharacterBody 作为 Unity 组件接收生命周期回调；项目的普通状态类与它区别讲解。

- [Dunlosky 等，2013 — Improving Students’ Learning With Effective Learning Techniques](https://www.psychologicalscience.org/journals/pspi/1529100612453266/)
  2026-10-16 已查阅出版方摘要；练习测试、分散练习获较高效用评价。用于设计项目复习中的主动回忆与跨日复测；不把通用研究直接当成 Unity 面试效果或固定日程的验证。
- [Karpicke 与 Blunt，2011 — Retrieval practice produces more learning than elaborative studying with concept mapping](https://pubmed.ncbi.nlm.nih.gov/21252317/)
  2026-10-16 已核对原始研究摘要；科学文本任务中提取练习的优势也出现在理解与推论测试。用于说明复习要练习脱离材料重建关系；不泛化为所有任务都优于所有其他方法。

- [Unity 2022.3 — AnimatorStateInfo.normalizedTime](https://docs.unity3d.com/2022.3/Documentation/ScriptReference/AnimatorStateInfo-normalizedTime.html)
  2026-10-16 已查阅；用于理解动画进度比例，配合项目 AttackAnimClock 的过渡匹配与秒数换算。
- [Microsoft Learn — readonly](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly)
  2026-10-16 已查阅；用于区分 readonly 引用字段不可重赋值与被引用对象内部字段可修改。

- [Unity Input System 1.14.2 — Actions](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.14/manual/Actions.html)
  2026-10-16 已查阅，与当前 manifest 的 1.14.2 对应。用于区分设备控件、Binding、InputAction，以及输入值与游戏动作执行。配合当前 PlayerBrain、BrainBase、CharacterBody 源码阅读。

- [Game Programming Patterns — State（含 FSM/HFSM 讨论）](http://gameprogrammingpatterns.com/state.html)
  免费在线书。用于：理解"大量 bool → FSM → 分层状态机"的通用推导；对照本项目 HFSM 的取舍。**注意**：书中把状态写成对象/类的做法与本项目一致，但它不涉及本项目的 Command 路由，别拿书里的结论套项目。
  2026-10-16 已核对作者官网检索结果及作者仓库章节：进入/退出动作与层级状态适合概念补充；本项目具体采用父层优先的命令路由，以 HierarchicalState 源码为准。
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
