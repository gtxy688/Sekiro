# 项目架构总览

复刻《只狼》弦一郎 Boss 战。以下描述以当前 Unity 源码与资源为准；类名用于定位职责，不表示每个场景都已运行验证。

## 技术组成

- 引擎与渲染：Unity 2022.3 LTS、URP。
- 玩家输入：Unity Input System，由 PlayerBrain 转成移动意图或 Command。
- 玩家角色控制：CharacterBody + HFSM；BrainBase 管理离散命令缓冲。
- Boss 决策：行为树节点 + Blackboard + BossMovePicker / BossMoveTable。
- 攻击时间：AttackStateBase 读取 Animator 播放时间，由 AttackConfig 或 BossMoveTable 的窗口、音效点和出箭点驱动。
- 命中：Hitbox 的 SphereCastAll 与 Overlap 查询；箭矢使用 ArrowProjectile 的独立扫描。
- 结算：CombatManager 将命中交给 CombatResolver，再调用 CharacterBody.ReceiveHit。
- 配置：CharacterConfig、AttackConfig 等 ScriptableObject；Boss 招式另由 BossMoveTable 表达。AttackCombatResolve 还提供代码侧默认伤害档位。
- 表现：CombatEventBus 将战斗事件发给 UI、AudioManager、Camera 等订阅方；相机使用 Cinemachine，部分表现使用 DOTween。

## 运行时关系

- PlayerBrain 读取玩家输入。Move 每帧作为连续意图处理；Attack、Jump、Deflect、Dodge、Heal 等离散动作走命令缓冲与状态机路由。LockOn 由 LockOnManager 处理。
- BTBrain 构建并更新 Boss 行为树。选招节点查询 BossMoveTable，执行节点通过 CharacterBody 启动相应攻击状态。
- CharacterBody 是 MonoBehaviour 外壳，承载角色配置、模块入口和 MainStateMachine。状态类是普通 C# 对象。
- MainStateMachine 顶层放 HierarchicalState 父状态。GroundedState、AirState、StunnedState、DeadState 各自管理子状态机与叶子状态。
- AttackStateBase 根据当前动画时间启停 Hitbox、发出攻击音效事件、生成箭矢并处理攻击结束与连段。
- CombatManager / CombatResolver 是近战、投射物和拼刀结算的中间层。最终受击交给目标 CharacterBody.ReceiveHit。
- UI、音频、相机等表现订阅 CombatEventBus；具体职责和订阅事件见各自源码。

## 架构图

~~~mermaid
flowchart TB
    subgraph Intent[意图层]
        Input[Unity Input System]
        PlayerBrain[PlayerBrain]
        Buffer[BrainBase 命令缓冲]
        BossBrain[BTBrain 行为树与 Blackboard]
        Picker[BossMovePicker + BossMoveTable]
        Executor[BT_ExecuteMove]
    end

    subgraph Runtime[角色控制与状态层]
        Body[CharacterBody]
        HFSM[HFSM：父状态 + 子状态机]
    end

    subgraph Combat[战斗执行层]
        Config[CharacterConfig / AttackConfig]
        Timeline[AttackStateBase + AttackAnimClock]
        Hitbox[Hitbox：SphereCastAll + Overlap]
        Arrow[ArrowProjectile]
        Manager[CombatManager]
        Resolver[CombatResolver]
    end

    subgraph Presentation[表现层]
        Bus[CombatEventBus]
        UI[CombatUIController / Views]
        Audio[AudioManager]
        Camera[CameraController / Cinemachine]
    end

    Input --> PlayerBrain
    PlayerBrain -->|连续移动| Body
    PlayerBrain -->|离散动作| Buffer
    Buffer --> Body
    BossBrain --> Picker
    Picker --> Executor
    Executor --> Body
    Config -.-> Body
    Config -.-> Timeline
    Body <--> HFSM
    HFSM --> Timeline
    Timeline --> Hitbox
    Timeline --> Arrow
    Hitbox --> Manager
    Arrow --> Manager
    Manager --> Resolver
    Resolver -->|ReceiveHit| Body
    Manager --> Bus
    Body --> Bus
    Bus --> UI
    Bus --> Audio
    Bus --> Camera
~~~

源码入口包括：

- 输入与 Boss 决策：Assets/Scripts/Player/Brain/PlayerBrain.cs、Assets/Scripts/Boss/BTBrain.cs
- 角色与状态：Assets/Scripts/FrameWork/Body/CharacterBody.cs、Assets/Scripts/FrameWork/States/Base/HierarchicalState.cs
- 攻击与命中：Assets/Scripts/FrameWork/States/Base/AttackStateBase.cs、Assets/Scripts/Combat/Hitbox.cs、Assets/Scripts/Combat/CombatManager.cs、Assets/Scripts/Combat/CombatResolver.cs
- 表现：Assets/Scripts/Mgr/CombatEventBus.cs、Assets/Scripts/UI/CombatUIController.cs、Assets/Scripts/Audio/AudioManager.cs

## 当前文档常见误读

- 近战命中不是 BoxCast，也不是由 Animation Event 开关；当前攻击状态读取动画时间并使用 SphereCastAll / Overlap 扫描。
- 项目并未按标准 MVC 组织 UI。
- 不能笼统说全部战斗数值只存在 SO：玩家与角色配置主要由 SO 提供，BossMoveTable 承载 Boss 招式数据，默认档位值也在代码中。
- 代码模块存在不等于场景装配完成，也不等于已经运行验证。

## 模块入口

| 关注内容 | 当前说明 |
|---|---|
| 状态机、受击、防御与忍杀 | 01-states.md |
| 角色属性、攻击配置与结算数值 | 02-combat-data.md |
| 近战、投射物、拼刀与命中结算 | 03-hit-detection.md |
| Boss 行为树与选招 | 04-behavior-tree-ai.md |
| 玩家输入与锁定 | 05-input-lockon.md |
| UI、音效与相机 | 06-presentation.md |
| 动画时间轴驱动的攻击与表现 | 07-anim-events.md |
