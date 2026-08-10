# 角色状态管理

玩家用 HFSM,弦一郎用行为树,

# 战斗判定系统：分离 Hitbox 与 Hurtbox  

● 技术方案： 基于动画事件 (Animation Events) + 射线/形状检测 (Physics.BoxCast/SphereCast)。
实现细节：

● Hitbox (攻击框)： 绑定在武器骨骼上。在攻击动画的特定帧（Active Frames）通过动画事件开启检测，结束帧关闭检测。通过记录已击中的目标 ID，防止一次挥砍造成多次伤害。

● Hurtbox (受击框)： 绑定在角色身体的不同部位。

● 拼刀逻辑： 当玩家的 Hitbox 与弦一郎的 Hitbox 在特定时间窗口内相交，触发“弹反（Deflect）”逻辑，增加架势条而不是扣血。

# 输入系统: 

使用 Input System,而不是Input Manager

# 数据解耦与表现方面

UI 使用 MVC 架构，同时使用 DoTween 加强视觉表现
boss 和主角的数据可以适当提取一部分放在 SO 中