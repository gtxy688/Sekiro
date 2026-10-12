# 学习目标确立：秋招面试讲清架构 + 事实来源只认源码

用户确认学习目标为「在秋招面试中把项目架构讲清楚」（第一优先）与「能看懂源码、继续开发」（第二优先）；并明确要求**只依据项目真实代码**教学——`Docs/` 下的复习文档与 `Docs/CODEX_TEACHING_GUIDE.md` 不作为架构事实来源。这决定了此后每节课的结论都必须能落到 `文件路径:行号`。

## Evidence

- 用户在 `/teach` 会话中直接答复目标："看懂架构，我想在秋招面试讲清楚"，并两次强调先看代码（"不要看哪个，只根据项目实际代码来"）。

## Implications

- 所有课程、术语表、速查文档一律以源码口径书写；引用外部权威资料仅用于补概念与对照，冲突时以代码为准。
- 首轮源码核对发现**五处项目规范与代码不一致**，已登记在第一课「§5」中待用户裁定，在此之前不得把这些规范描述当作代码事实讲授：
  1. 规范称命中判定用「动画事件」，实际是 `AttackAnimClock` 时间轴驱动判定段（全 Assets 无 AnimationEvent / StateMachineBehaviour / Timeline Signal）。
  2. 规范称 `BoxCast`，实际只有 `SphereCastAll` + 两种 `Overlap*NonAlloc`（`WeaponController.cs:25` 注释为过期描述）。
  3. 规范称伤害只归 `AttackConfig`(SO)，实际存在按 `HitGrade` 写死的默认档位表并回写 cfg（`AttackCombatResolve.cs:8-25`）。
  4. 概览称 UI 用 MVC，实际无 Model 层（`UIView` 无 Bind/OnRefresh，View 自找引用、自绑输入）。
  5. 规范要求编辑器改序列化字段走 `SerializedObject`/`SerializedProperty`，实际零命中，用的是直接赋值 + `EditorUtility.SetDirty`。
- 用户尚未对自己的基础做具体自评，仅表示"比较弱"（见 `Docs/CODEX_TEACHING_GUIDE.md`，非事实来源，仅作参考）；下一课结束时应通过 Q1–Q4 的作答情况判断是否需要回补 FSM/HFSM 推导。
