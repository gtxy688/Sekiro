# 10 - 时间尺度收编 + 处决资格去身份化 验收清单

> 状态：**待验收**。本环境无 unityMCP，也无可用的 C# 编译器（csc 被安全策略拦截），**Unity 编译未验证**。
> 命名按 `0X-xxx-test.md` 惯例。**尚未在 `00-overview.md` 索引表登记**（08、09 也未登记，可一并补）。
>
> 本次是一次**重构，不是新功能**——A、B 两组跑下来应当「和改动前玩起来一样」。
> 唯一**故意**改变了行为的是 A3（顿帧途中暂停再取消），改后更正确，见下方说明。

---

## 0. 改了什么，为什么

### 问题 1：处决资格被编码成了对象身份比较

```csharp
// 改前，DuelDirector.TryExecuteFinisher
if (initiator != playerRef) return false;
```

「只有玩家能处决」是一条**游戏规则**，却被写成「发起者是不是那一个对象」。
后果：加第二个敌人、想让杂兵也能处决、乃至将来想让 Boss 反杀，都得回来改这条分支。
与 `CLAUDE.md` 第 5 条（数值走 SO）是同一族病——**规则不该焊死在具体实例上**。

附带问题：`PlayerRef` / `BossRef` 是 public 序列化字段 + `static Instance` 单例，
全局可访问、却又依赖特定场景的拖引用装配，是最糟的组合。

### 问题 2：顿帧与暂停在抢同一个全局变量

```csharp
// 改前，CombatManager.HitStopRoutine
Time.timeScale = GamePause.IsPaused ? 0f : 1f;
```

这一行就是在承认「我不知道该写什么，得先去问另一个系统」。那不是防御，是裂缝。
两个系统各自回写 `Time.timeScale`，谁最后写谁赢。

**旧实现确实有两个实际缺陷**（不只是"理论上有风险"）：

| 场景 | 旧行为 | 说明 |
|------|--------|------|
| 顿帧途中打开暂停、再立刻取消 | 顿帧被吃掉，时间直接回 1 | `SetPaused(false)` 无条件写 1，不管顿帧是否还在计时 |
| 顿帧进行中切场景 | `timeScale` 可能**永久停在 0.05** | `CombatManager` 随场景销毁，协程被杀，没人负责恢复 |

### 改法

| 文件 | 改动 |
|------|------|
| `Mgr/TimeScaleController.cs` | **新增**（126 行）。全项目唯一写 `Time.timeScale` 的地方 |
| `Mgr/GamePause.cs` | 不再自己写 `timeScale`，改为 `TimeScaleController.Refresh()` 申报 |
| `Combat/CombatManager.cs` | 删除 `HitStop()` / `HitStopRoutine` / `hitStopRoutine`；顿帧改成向 `TimeScaleController` 申报；新增 Boss 阵营校验；`Configure` 不再注入玩家 |
| `Combat/DuelDirector.cs` | 删除 `playerRef` 字段；处决资格改为 `CanInitiateFinisher()`（查阵营）；删除已被覆盖的死分支 `initiator == bossRef` |
| `FrameWork/States/Ground/DeflectState.cs` | 2 处 `CombatManager.Instance?.HitStop()` → `TimeScaleController.HitStop()` |
| `Combat/CombatResolver.cs` | 更新注释里已失效的引用 |

**验证手段**：`grep -rn "Time\.timeScale" --include=*.cs Assets/Scripts`
应只剩 `TimeScaleController.cs` 里**一处**真实赋值（其余全是注释）。

---

## 1. 心智模型：单一写入者

顿帧和暂停都**不再直接写** `timeScale`，只向 `TimeScaleController` 申报自己的状态，
由唯一的求值函数按优先级算出结果：

```
Time.timeScale = 暂停中 ? 0
               : 顿帧中 ? 0.05
               :        1
```

暂停（系统级，玩家主动要求世界停下）压过顿帧（表现级，50ms 的打击重量感）。
任何一方都不需要知道另一方存在，所以"谁先谁后"不再影响结果。

---

## A 组：顿帧与暂停

### A1 普通命中仍有顿帧（回归）

1. 进战斗，用轻攻击砍中 Boss
2. **预期**：有轻微的卡顿/顿挫感，之后游戏恢复正常速度
3. 再连续快速攻击
4. **预期**：连续命中时顿帧不叠加、不拖慢（重复调用只重启计时）

### A2 顿帧途中打开暂停 —— 核心竞态

1. 进战斗，连续攻击制造持续命中的节奏
2. 在命中瞬间按 ESC 打开暂停菜单
3. **预期**：世界立即冻结
4. **静静等 2 秒**（50ms 的顿帧早该结束了）
5. **预期**：**仍然冻结**，不会自己恢复运行
6. 取消暂停
7. **预期**：游戏恢复正常速度

> 旧实现在第 5 步也基本正确（它回读了 `IsPaused`），所以这条主要是回归验证。
> 真正区分新旧的是 A3。

### A3 顿帧途中暂停再取消 —— 本次唯一改变的行为

1. 进战斗，连续攻击
2. 命中瞬间打开暂停
3. **立刻**取消暂停（1 秒内）
4. **预期（改后）**：顿帧继续走完剩余时间，然后恢复正常
5. **预期（改前）**：顿帧被直接吃掉，时间立刻回 1

> 这条改前是 bug，改后更正确。差异只有 50ms，肉眼可能看不出来——
> **看不出来没关系，不要为了"验证"它去勉强卡时间。** 只要 A1、A2、A4 正常即可。

### A4 暂停中不受顿帧干扰

1. 打开暂停菜单，保持暂停状态
2. **预期**：世界完全静止，不会有任何顿帧残留导致时间被拨动
3. 取消暂停
4. **预期**：正常恢复

### A5 复战复位后时间正常

1. 打完一场（或随时）按 **F8** 触发 `EncounterScope.ResetAll()`
2. **预期**：复位后游戏**没有**被卡在慢动作或静止状态
3. 移动、攻击都正常

> 旧实现依赖 `CombatManager.ResetForEncounter` 手动停协程；
> 现在改为 `TimeScaleController.CancelHitStop()`，且宿主跨场景存活，
> 即使切场景也不会出现"协程被杀、timeScale 永久停在 0.05"。

---

## B 组：处决资格

### B1 玩家能处决 Boss（回归，不能退化）

1. 正常打崩 Boss 架势
2. 出现处决提示时按攻击键
3. **预期**：处决演出正常播放，Boss 掉一条命

### B2 Boss 不能处决玩家

1. 让 Boss 打崩**玩家**的架势
2. **预期**：玩家进入硬直（StunnedState），**Boss 不会进入处决演出**
3. **预期**：不会出现"双方播处决动画"的情况

> 这条验证 `CanInitiateFinisher` 生效：Boss 是 Enemy 阵营，拿不到处决资格。

### B3 启动校验 —— Boss 阵营配错会报错

1. 在 Hierarchy 选中 Boss 角色
2. 在 `CharacterBody` 组件上把 **Faction 临时改成 `Player`**
3. 进入 Play 模式
4. **预期**：Console 出现红色 Error，
   内容包含 `Boss 角色「xxx」的 Faction 是 Player，应当是 Enemy`
5. **测完务必改回 `Enemy`**

> 这条是新增防线。原来靠 `DuelDirector` 里的 `if (initiator == bossRef)` 兜；
> 改成阵营判断后那行删了（逻辑上被 `CanInitiateFinisher` 覆盖），
> 但覆盖的前提是「Boss 确实不是 Player」——前提必须有人验，否则 Boss 会获得处决资格。

### B4 启动校验 —— 玩家阵营配错会报错

见 `09-faction-test.md`。若玩家 Faction 不是 `Player`，Console 应报 Error。

---

## C 组：整体回归

这几条是**兜底**：本次改动碰了时间系统和处决资格，以下玩法必须和改动前一致。

- [ ] **C1** 弹反（完美格挡）有顿帧、有火花、有镜头震动
- [ ] **C2** 普通格挡有顿帧（不掉血也要有）
- [ ] **C3** 箭矢命中玩家有顿帧
- [ ] **C4** 处决演出的镜头/音效/慢动作全部正常
- [ ] **C5** 抓取投技（Elbow）演出正常，双方同步回 Idle
- [ ] **C6** 暂停菜单开关多次，时间表现始终正常
- [ ] **C7** Console **无新增报错**（本次改动前后的报错应当完全一致）

---

## 3. 已知边界（没做的事，别当成漏了）

### 边界 1：Boss 反杀这条规则表达不了

`CanInitiateFinisher` 现在的语义是「发起者必须是玩家阵营」。
你提到的三个场景里，它解决了两个：

| 场景 | 是否自动成立 |
|------|------------|
| 加第二个敌人 | ✅ 敌人是 Enemy，自动没有处决资格 |
| 让杂兵也能处决 | ✅ 把杂兵 Faction 设成 Player 即可 |
| **Boss 反杀**（敌对阵营发起处决） | ❌ 表达不了 |

第三个要真做，判断得从「查发起者资格」换成「查一对关系」——
例如在 `CharacterConfig` 上加一个「能否发起处决」开关，按角色配置而不是按阵营。
**那属于新增需求，不是重构，没提前做。** 需要的话跟我说。

### 边界 2：顿帧的触发点仍在 CombatManager

`CombatManager.ReportHit` 里仍有一行 `TimeScaleController.HitStop()`。

彻底做法是由表现层订阅 `CombatEventBus.OnTakeDamage` 来触发顿帧。
但被格挡/弹反的攻击不掉血、不发该事件，改过去会**改变格挡时的顿帧行为**——
那是需求变更，不是重构。本次只交出「写入权」，保留「触发点」，行为零变化。

### 边界 3：顿帧参数的 Inspector 入口还在 CombatManager

`TimeScaleController` 是静态类，没有 Inspector。
`CombatManager` 上的 `enableHitStop` / `hitStopDuration` 保留为**过渡期配置入口**，
`Start()` 时同步给 `TimeScaleController`。这样 Inspector 上调过的值不会丢。

---

## 4. 需要你做的事

1. Unity 里打开项目，**先看 Console 有没有编译错误**
2. 按 A → B → C 顺序跑一遍
3. 跑完把结果告诉我，**我不自己宣称完成**
