# Boss 战宏观流程

这一篇先不进入某个类的内部，而是建立整场 Boss 战的运行地图。

## 1. 战斗开始

进入场景后：

- `EncounterScope` 声明本场战斗的玩家与 Boss，并收集需要参与复战重置的对象。
- 玩家和 Boss 的 `CharacterBody` 初始化组件、战斗数值、武器模块与 HFSM。
- 双方进入初始的 `GroundedState → IdleState`。
- `PlayerBrain` 开始读取玩家输入。
- `BTBrain` 建立行为树，以玩家为目标进行决策。
- UI、音频、相机和特效系统订阅战斗事件。

此时最重要的几个角色是：

```text
PlayerBrain / BTBrain：决定想做什么
CharacterBody + HFSM：执行角色行为
Hitbox / Hurtbox：发现攻击接触
CombatManager / CombatResolver：转发和结算命中
CombatStats：维护生命、架势、命数和回生次数
CombatEventBus：把结果通知表现层
EncounterScope：定义并重置这一场战斗
```

## 2. 玩家与 Boss 进入交战循环

玩家侧：

```text
输入
→ PlayerBrain 产生行为意图
→ CharacterBody
→ HFSM 判断当前能否执行
→ 播放动画并执行移动或攻击
```

Boss 侧：

```text
距离、冷却、权重和战斗状态
→ BTBrain 行为树选择行为
→ CharacterBody
→ HFSM 执行走位或招式
```

两者的脑子不同，但共用角色执行层。

## 3. 一次攻击如何形成命中

`AttackState` 按动画播放时间推进招式。到达配置的攻击窗口后：

```text
开启武器 Hitbox
→ 采样武器上一帧与当前帧位置
→ SphereCast 扫过中间路径
→ Overlap 补查贴身与起点重叠
→ 找到对方 Hurtbox
→ CombatManager.ReportHit
→ CombatResolver 转成 ReceiveHit
```

动画决定攻击何时生效，Hitbox 决定是否接触目标，CombatResolver 负责把配置数据交给目标。

## 4. 命中后如何分叉

目标收到 `HitData` 后，先让当前 HFSM 状态处理：

- 格挡状态可以把普通受击解释为格挡。
- 弹反窗口可以把攻击反作用到攻击者的架势和硬直。
- 闪避无敌帧可以直接吞掉命中。
- 无方向垫步遇到突刺危字，可以进入识破。
- 状态没有拦截时，才扣生命、增加架势并进入受击。

因此，格挡、弹反、闪避和识破不是四套互不相关的系统，而是“目标当前状态如何解释这次命中”的不同分支。

## 5. 架势崩解与忍杀

攻击、格挡、弹反和识破都可能改变架势。架势达到上限后：

```text
CombatStats 标记架势崩解
→ 记录崩解来源 Attack / Deflect / Mikiri
→ Boss 进入崩解状态
→ UI 显示忍杀机会
→ 玩家再次发出 AttackCommand
→ GroundedState 优先尝试忍杀
→ DuelDirector 同步双方处决状态与动画
→ 处决结算清除 Boss 一条命
```

Boss 还有命时，生命回满、架势清空并继续战斗；没有命时触发胜利事件。

当前项目的多命系统不等于完整阶段 AI。第一条命被清除后会继续现有选招循环，`LivesRemaining` 没有直接切换另一套招式池。

## 6. 玩家死亡与回生

玩家生命归零后，`CombatStats` 检查剩余回生次数：

- 有回生：进入 `RevivePendingState`，等待玩家选择。
- 按攻击：消耗一次回生，生命回满、架势清零并播放起身。
- 按防御：放弃回生，进入真正死亡。
- 没有回生：直接触发死亡事件。

玩家失能期间，Boss 行为树停止正常出招，改为保持距离或绕行。玩家回生后，Boss 等待起身并执行后撤，再恢复正常战斗。

## 7. 表现层如何跟随

生命、架势、弹反、危字、忍杀、回生、死亡和胜利都会发布事件。UI、音频、相机、特效和顿帧系统分别订阅，不需要战斗结算逐个调用它们。

## 8. 复战如何恢复

当前 F8 是调试用复战入口：

```text
EncounterResetDebug
→ EncounterScope.ResetAll()
→ 所有 ICombatResettable 恢复到战斗开始状态
```

它重置角色数值与状态、AI 冷却、处决演出、UI、输入和顿帧等临时状态。F8 不是正式产品流程，但底层重置契约可以被未来的“重新挑战”按钮复用。

## 一句话总结

> 玩家输入和 Boss 行为树产生行为，HFSM 执行行为，Hitbox 检测攻击，当前状态解释命中，CombatStats 决定崩解或死亡，事件总线把结果交给表现层。

## 面试追问梯度

### 基础概念

1. 这场 Boss 战由哪些核心系统组成，各自负责什么？
2. Unity 每帧里，玩家与 Boss 是怎样同时推进的？

### 项目实现

3. 从玩家按攻击到 Boss 掉血，完整链路是什么？
4. 从 Boss 架势崩解到第一条命被清除，完整链路是什么？

### 方案取舍

5. 为什么不让 PlayerBrain、BTBrain 直接控制 Animator 和伤害？
6. CombatManager、CombatResolver、CombatStats 为什么要分开？

### 扩展设计

7. 如果再加入一个 Boss，哪些系统能复用，哪些配置必须新增？
8. 如果做正式第二阶段，应该在哪一层切换招式集合？

### 故障排查

9. 动画正常播放但没有伤害，你会按什么顺序排查？
10. 忍杀动画播放了但 Boss 没扣命，应该查看哪条链路？

回答宏观题时使用“决策 → 执行 → 检测 → 结算 → 状态结果 → 表现”的顺序，不要一开始就罗列类名。

## 自测

1. 为什么攻击动画播放了，不代表目标一定会扣血？
2. 格挡、弹反、闪避和识破为什么可以视为同一条命中链路的不同分支？
3. Boss 第一条命被忍杀后，哪些数据会恢复？当前是否真的切换了另一套阶段 AI？
4. `EncounterScope` 与 `CombatManager` 的职责有什么区别？
