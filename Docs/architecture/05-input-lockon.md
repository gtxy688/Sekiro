# 05 玩家输入与锁定

> 现状依据：PlayerBrain、BrainBase、Input System 资源与 LockOnManager。手动验收见 05-input-lockon-test.md。

## 输入路径

PlayerInput / PlayerInputActions 提供玩家动作。PlayerBrain 绑定 Move、Attack、Jump、Deflect、Dodge、Heal、LockOn 等动作，并在对象销毁时解绑回调。

- Move 是连续输入：PlayerBrain 在更新过程中读取方向并持续交给 CharacterBody。
- 攻击、跳跃、防御、垫步、喝药等离散意图被转换为 Command，经 BrainBase 缓冲后走状态机路由。
- 按住攻击达到 CharacterConfig 的时长后可以选择突刺配置；忍杀意图在攻击键按下时判定，使用 FinisherCommand。
- LockOn 由 PlayerBrain 请求 LockOnManager 切换目标状态；它不是叶子攻击状态命令。

具体动作是否在资源中绑定、键位是什么，以 PlayerInputActions.inputactions 当前内容为准。

## 锁定职责

LockOnManager 管理锁定目标与锁定状态事件。CharacterBody / 移动状态在需要时读取战斗目标处理面向和方向；相机与锁定指示器由表现层响应相关事件或目标数据。锁定相机的实际构图与切换以 Camera 模块实现为准。

## 核心源码与资源

- Player/Brain/PlayerBrain.cs
- Player/Brain/BrainBase.cs
- Player/Input/InputRebindService.cs
- Assets/Resources/Input/PlayerInputActions.inputactions
- Player/Control/LockOnManager.cs
- Mgr/CombatEventBus.cs
