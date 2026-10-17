# 05 玩家输入与锁定

> 现状依据：PlayerBrain、BrainBase、Input System 资源与 LockOnManager。

## 输入路径

PlayerInput 提供并管理玩家动作实例。PlayerBrain 直接读取同物体 PlayerInput 的 Actions，在 Player Map 中查找 Move/Attack/Jump/Deflect/Dodge/Heal/LockOn，再直接订阅和使用；缺少配置时由实际使用处暴露错误。角色配置和 ThrustAttack 按必需依赖使用，不另设启动校验、输入实例回退或默认长按时间。输入启停由 PlayerInput 管理；PlayerBrain 绑定动作回调，并在对象销毁时解绑已完成的订阅。

- Move 是连续输入：PlayerBrain 在更新过程中读取方向并持续交给 CharacterBody。
- 攻击、跳跃、防御、垫步、喝药等离散意图被转换为 Command，经 BrainBase 缓冲后走状态机路由。
- 按住攻击达到 CharacterConfig 的时长后可以选择突刺配置；忍杀意图在攻击键按下时判定，使用 FinisherCommand。
- LockOn 由 PlayerBrain 请求 LockOnManager 切换目标状态；它不是叶子攻击状态命令。

具体动作是否在资源中绑定、键位是什么，以 PlayerInputActions.inputactions 当前内容为准。

## 防御按键的身份与释放

防御是唯一需要区分"玩家真实按下"与"系统请求"的动作，两者不能靠 `TryExecuteCommand` 的返回值区分：

- PlayerBrain 在 Deflect 的按下回调里向 CharacterBody 申请新的 PressId，再缓冲带身份的 `DeflectCommand`（`DeflectCommandSource.PhysicalPress`）。同一个 PressId 在缓冲重试或状态重入时不会重复开窗，也不会重复记账。
- 松开不再缓冲 `IdleCommand` 去覆盖尚未消费的防御按下，而是通过 CharacterBody 的语义入口记录该 PressId 已释放。释放按"最高已释放 ID"保存，延迟处理旧释放不会关掉之后的新按下；防御状态据此判断短点按、收刀与是否继续举刀。
- 崩解恢复处的自动回防、UI 的防御选择使用系统来源 `DeflectCommand`（PressId = 0），只恢复普通格挡：不开启完美弹反窗口、不累计空按，也不伪造物理按键身份。自动回防请求不会顶掉缓冲里玩家真实按下的防御命令。
- 输入被暂停、胜利结算或对象停用时，PlayerBrain 清除防御按住状态并定向丢弃缓冲中的 `DeflectCommand`；BrainBase 仍保持通用单槽缓冲，只增加了按类型定向清理能力。
- 是否真的开启弹反窗口由身体 / 状态入口确认（`CharacterBody.BeginDeflectAttempt` 的返回值），不由命令是否被消费推断。

## 锁定职责

LockOnManager 管理锁定目标与锁定状态事件。CharacterBody / 移动状态在需要时读取战斗目标处理面向和方向；相机与锁定指示器由表现层响应相关事件或目标数据。锁定相机的实际构图与切换以 Camera 模块实现为准。

## 核心源码与资源

- Player/Brain/PlayerBrain.cs
- Player/Brain/BrainBase.cs
- Player/Input/InputRebindService.cs
- Assets/Resources/Input/PlayerInputActions.inputactions
- Player/Control/LockOnManager.cs
- Mgr/CombatEventBus.cs
