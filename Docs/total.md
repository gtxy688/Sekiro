# 角色状态管理

玩家通过输入做大脑思考要干什么，向 body 发送指令，然后用 HFSM 判断能不能干；
Boss 用行为树做大脑思考要干什么，向 body 发送指令，然后用 HFSM 判断能不能干；

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

# 事件处理

以「命令模式 (Command Pattern)」为核心驱动战斗，辅以极轻量级的「事件总线」处理表现层。绝对不需要「消息队列」。

● 输入采集 (Input / AI)： 玩家按键或 AI 思考树触发，生成对应的 Command（如 AttackCmd）。
● 角色控制面板 (Controller & State Machine)： 接收 Command。检查当前状态（是否在受击硬直中？是否在霸体中？），如果允许，则进入新状态并播放动画。
● 动画事件 (Animation Events)： 在挥刀的特定帧开启/关闭武器的碰撞盒，检测到交锋时直接计算伤害和架势条（同步逻辑）。
● 表现广播 (Event Bus)： 数据发生变化后，抛出事件（如 OnPostureBroken），由外部的 VFX 脚本播放火花特效，Camera 脚本触发屏幕震动。

---

# 框架决策记录

> 以下为代码审查后确定的框架级决策，开发时以此为基准。

## 1. HFSM 层级规则

● **MainStateMachine 顶层只装 HierarchicalState（父状态）**：GroundedState、AirState、StunnedState。
● **叶子状态（Idle/Move/Attack/Deflect/Dodge 等）在父状态的 SubStateMachine 内部**。
● `MainStateMachine.CurrentState is DodgeState` 永远为 false —— 叶子状态类型不可直接判，必须逐层往下查。

## 2. Hit 响应路由（镜像 Command 路由）

● BaseState 新增 `OnHitReceived(HitData)` 虚方法，默认返回 false。
● HierarchicalState 转发逻辑：`OnParentHandleHit(HitData)`（父拦截）→ 子状态 `OnHitReceived`。
● DeflectState 重写 OnHitReceived：弹反窗口内 → 弹反处理，返回 true；窗口外 → 返回 false 硬吃。
● DodgeState 重写 OnHitReceived：无敌帧内 → 返回 true 吞掉伤害。
● StunnedState.OnParentHandleHit：受击期间全部拦截（防止二次硬直覆盖）。
● ReceiveHit 只做入口调用，不直接判状态类型。

## 3. 命中判定：动画事件驱动 BoxCast

● **武器 Hitbox**：不再用 OnTrigger。攻击动画特定帧通过动画事件开启/关闭判定。
● **判定方式**：每帧 BoxCast（武器上一帧位置 → 当前帧位置），防止高速挥砍穿透。
● **Hurtbox**：角色身上单个胶囊体/Hurtbox 组件，不分部位（无暴击/部位伤害需求）。
● **结算链路**：Hitbox 扫到 Hurtbox → 报告 CombatManager → CombatManager 查全局规则 → 调 target.ReceiveHit。
● **拼刀**：双方 Hitbox 相交 → 触发弹反判定（不在本模块展开）。

## 4. 受击状态 (StunnedState) 设计

● StunnedState 是**顶层 HierarchicalState**，与 GroundedState、AirState 平级。
● 进入时 `GetInitialSubState()` 按 `body.IsGrounded` 分派到 GroundStunnedState 或 AirStunnedState。
● 空中受击结束：仍按 IsGrounded 决定去向（落地回地面，没落地继续下落）。
● 受击期间吞掉所有 Command（OnParentHandleCommand 返回 true）。

## 5. 伤害数据归属

● 伤害数值（BaseDamage、PostureDamage）**统一归属 AttackConfig（SO）**，不在 WeaponHitbox 或其他地方重复定义。
● 不同招式配不同 AttackConfig，伤害跟着招式走，策划只改 SO 一个入口。

## 6. 战斗属性管理

● CharacterBody 持有运行时战斗属性：HP、Posture、`TakeDamage()` 方法。
● 数值不硬编码在 Body 里：抽象 `CharacterConfig（SO）`，包含 MaxHP、MaxPosture、StunDuration 等。
● 角色和 Boss 各自挂一份 CharacterConfig，共用 CharacterBody 逻辑，数值独立配置。
● 后续属性膨胀时可在 CharacterConfig 内部继续拆分，不影响 Body 接口。

## 7. 行为树设计

● **黑板 (Blackboard)**：`Dictionary<string, object>` 键值字典，挂在行为树根节点上，所有节点共享。节点从黑板读写数据（如 target、attackRange），不通过构造函数传参。
● **Running 记忆**：Selector 和 Sequence 需记录 `currentChildIndex`。子节点返回 Running 时，下一帧从同一索引继续，而非从头遍历。
● BTBrain 构建树时将黑板注入根节点。

## 8. 待办（非框架问题，留待模块实现）

● PlayerBrain 输入绑定不完整 —— Attack/Jump/Defend 等按键未生成 Command，缓冲池机制就绪但无人触发。

## 9. 已修复代码坑

● **AirIdleState NRE**：删除未赋值的 `config` 字段，AttackState 加 null config 保护。
● **BT_Attack isHeavy**：只狼无重攻击，删除 `isHeavy` 参数。轻重击通过不同 AttackConfig 区分，不通过 Command 字段。
● **IdleCommand**：保留。玩家松摇杆 / AI 停止移动时，需要显式指令让人物回到待机。
● **StunnedState.cs 位置**：已从 `States/Ground/` 移到 `States/`（它是顶层父状态，不在 Ground 子目录下）。
● **Command 装箱 (B)**：暂时不改。1v1 每秒几十个 Command，GC 影响可忽略。
● **硬编码数值 (C)**：后续统一迁入 CharacterConfig（SO），随 F 一起实施。

## 10. UI 系统（MVC，参考截图）

● 布局：

- 正上方居中：Boss 架势条（中心双向增长，黄/橙色带箭头端点）
- 底部居中：玩家架势条（中心双向增长，样式同 Boss）
- 左上角：忍杀提示灯（2 红点）+ Boss 血条 + 名称"苇名弦一郎"
- 左下角：回生节点（1 粉色花瓣图标）+ 玩家血条
- 右下角：葫芦槽位（图标 + 数量数字）
- 世界空间：锁定点（白点挂 Boss 身上）→ 崩解时变大红点高亮
- "危"字：玩家头顶 World 坐标 → Canvas 投影（避免被场景遮挡）
  ● 架构：MVC。View 只负责视觉（SetProgress/SetDots/ShowPerilous 接口），Controller（CombatUIController）订阅 CombatEventBus 事件驱动 View。
  ● 动画：架势条快满变亮+尖刺、忍杀红点脉动、"危"字放大淡入+红光（DoTween）。
  ● 不显示：忍义手武器栏、纸人数量。物品区只显示葫芦。

## 11. Boss AI 参考文档

● 弦一郎 AI 逆向研究：`Docs/references/sekiro-genichiro-ai.md`
● 核心三层结构（M7 实现时参考）：

1. **主动计划 (Goal.Activate)**：按距离分段选择招式（>7m 接近+砍/射箭，5-7m 接近+横砍+射箭/飞渡符舟，3-5m 近战连段，≤3m 贴身战），带冷却时间。
2. **交锋计划 (Goal.Kengeki_Activate)**：被玩家弹开后变招——按连续被弹开次数决定反击（0-1 次砍一刀，≥2 次侧垫步+重砍/突刺/射箭）。
3. **变招/防御 (Goal.Interrupt + Parry)**：玩家攻击时防御/招架，蓄力重箭打断玩家使用道具。
   ● 动画帧：招式编号 3000 系 = 近战连段，3014/3015 = 射箭+砍，3040/3041 = 飞渡符舟（水鸟乱舞前身），3062 = 突刺，5201 = 垫步。
