# 08 - 复战复位契约 手动验收清单

> 状态：**待验收**。本环境无 unityMCP，只做了静态符号核验，Unity 编译与运行时行为均未验证。
> 命名按 `0X-xxx-test.md` 惯例（`-test` = 手动验收清单，非自动化测试）。
> **尚未在 `00-overview.md` 索引表登记**——是否补一份 `08-encounter-reset.md` 设计文档，等你决定。

---

## 0. 本批改了什么

| 文件 | 改动 |
|------|------|
| `Combat/EncounterScope.cs` | 新增（上一批）。一场战斗的边界 + `ResetAll()`。**本批两处改动**：① 删掉"倒序保证顺序"的错误说法，改为硬约束「ResetForEncounter 必须顺序无关」；② 新增 `Ensure()` —— 场景没挂时自动兜底创建一个空的，并新增 `HasParticipants` 属性 |
| `Combat/ICombatResettable.cs` | 新增（上一批）。`ResetForEncounter()` 接口 |
| `FrameWork/Body/CombatStats.cs` | 抽出 `ResetForEncounter()`，构造函数改为调用它 |
| `FrameWork/Body/CharacterBody.cs` | 实现接口；9 组状态复位；记录并复位初始站位 |
| `Boss/BTBrain.cs` | 实现接口；清黑板 / 回生后阶段 / 连招执行器（`moveFilter` 属设定，不清） |
| `UI/CombatUIController.cs` | 实现接口；清三块提示 UI + `CombatInputGate` + `PlayerInput` + 命数点/回生点/葫芦 |
| `Combat/ArrowProjectile.cs` | 实现接口；复战时销毁自己 |
| `Combat/CombatManager.cs` | `Start()` 开头 `EncounterScope.Ensure()`；`ValidateFinisherRefs()` 改判 `HasParticipants`（避免兜底把"忘了拖引用"这个配置错误一起吞掉） |
| `Combat/EncounterResetDebug.cs` | **新增·临时**。F8 触发 `ResetAll()`，供本清单执行；验收完建议删 |

---

## 1. 编译验证（先做这个）

**操作**：Unity 回到编辑器，等编译完成，看 Console。

**预期**：0 error。

若报错，最可能是以下两类，按顺序排查：
- **CS0103 类型不存在** → 新文件漏 `using`。本项目跨模块命名空间映射见本文末「硬约束」第 4 条。
- **CS0246 / 命名冲突** → `CombatUIController.cs` 本批新增了 `using ARPG.Combat;`，检查是否与 `ARPG.UI` / `ARPG.Configs` 撞名。

---

## 2. 前置：只挂一个调试触发器

**为什么不再需要手工挂 EncounterScope**：上一版我把「场景里必须挂 EncounterScope」写成必做步骤（小金按上一版跑到这步时，Console 确实弹出了那句 `场景里没有 EncounterScope` 警告——说明报警机制是生效的，但也说明这一步纯仪式：只有一场战斗时，手工挂出来的通常也是个空组件）。

我当初否决自动兜底的理由是「跨组件 Awake 顺序不定，CombatManager 里 AddComponent 可能造出第二个 Scope」——**这个理由是错的**。Unity 保证所有 `Awake` 跑完才进 `Start`，所以在 `Start` 里判断 `Current == null` 就一定是场景里真没有，不可能重复创建。

已改为自动兜底（`EncounterScope.Ensure()`）。忘了挂的失效模式直接消失，不用再靠一条警告提醒人去点一下。

**操作**（只剩一步）：
1. 打开 `Assets/Scenes/GameScene.unity`
2. 任意物体上 `Add Component` → `EncounterResetDebug`
3. 保存场景

> 想显式配置也随时可挂 `EncounterScope` 并填 Player / Opponents——挂了就以你那个为准，自动兜底不会触发。**做多 Boss / 连战时建议这么做**（参与者该由关卡作者声明，不该靠回退猜）。

---

## 3. 验收项

### A. 开局自检

| 步骤 | 预期结果 |
|------|---------|
| 进入 Play | Console 出现一条 `[EncounterScope] 场景里没有 EncounterScope，已自动创建一个空的`（**Log 级，不是警告**） |
| 看 Hierarchy | 出现一个 `EncounterScope (Auto)` 物体，挂着 `EncounterScope` 组件 |
| 若看到 `场景中有多个 EncounterScope` 警告 | 说明场景里已挂了一个、同时又兜底创建——不应发生，回来找我 |
| 若仍看到 `PlayerRef 未绑定` 之类的报错 | 自动兜底出来的空 Scope 不提供参与者，仍按旧路径检查拖引用——按提示把引用拖好 |

### B. 战斗中复位（最基础的一条）

| 步骤 | 预期结果 |
|------|---------|
| 打一会儿，把玩家血量打掉一半、架势条打到一半 | — |
| 按 **F8** | Console 打出 `=== ResetAll() 开始 ===` 与 `结束`，中间无报错 |
| 观察血条 / 架势条 | 玩家与 Boss **均回满血、架势条归零**（不是停在复位前的残值） |
| 观察站位 | 双方回到各自出生点与初始朝向，**没有被传送到世界原点 (0,0,0)** |
| 观察状态 | 双方站待机，不残留出招动作、不倒地、不卡处决 |
| 继续操作 | 能正常移动 / 攻击 / 防御 / 弹反 |

### C. **胜利后复位（本批最致命的一项，务必验）**

> 原因：`HandleVictory` 会打 `CombatInputGate.SetBlocked(true)` 并 `PlayerInput.DeactivateInput()`，
> 而解锁原本只写在 `VictoryView.RestartScene()`（重载场景）里。复战不重载场景，
> 不还原就是**开局人物完全不能动**。

| 步骤 | 预期结果 |
|------|---------|
| 正常打死 Boss（两次处决打光命数） | 胜利面板出现，此时角色不可操作（这是原有正确行为） |
| 按 **F8** | 胜利面板消失 |
| 观察 | **玩家立刻能走动、能出刀**——不能动就是没修好，回来找我 |
| 观察 Boss | 血条回满、命数点恢复（左上红点数量回到初始值） |
| 观察玩家 | 血条回满、回生点恢复、葫芦次数恢复 |

### D. 死亡 / 回生提示中复位

| 步骤 | 预期结果 |
|------|---------|
| 让玩家被打死，停在「回生 / 放弃」选项界面 | — |
| 按 **F8** | 回生提示与暗红压暗**全部消失**，玩家复活站起 |
| 观察 | 回生点数量恢复，能正常操作 |
| 另一路：放弃回生 → 进 Game Over 界面 → 按 F8 | Game Over 面板消失，玩家恢复正常可控 |

### E. 箭矢

| 步骤 | 预期结果 |
|------|---------|
| 引 Boss 出射箭招，箭在飞行途中按 **F8** | 空中残留的箭**立即消失** |
| 复位后 | 不会有"上一场射出的箭"砸中新一局的玩家 |

### F. Boss AI

| 步骤 | 预期结果 |
|------|---------|
| 打一会儿（让 Boss 累积若干招式历史 / 进入回生后阶段） | — |
| 按 **F8** | Boss 回到初始行为，不保留上一场的连招记忆与选招倾向 |
| 死亡后回生时 Boss 的"追打倒地玩家"行为 | 复位后不再追打，正常开打 |

---

## 4. 已知未覆盖（不是 bug，是这次没做）

1. **「危」字警告**：复位瞬间若 Boss 正在放危字招，警告会多停留一帧到几十毫秒（靠自身计时器消失）。影响极小，未处理。
2. **Boss 被处决后 GameObject 若被禁用**：复位只重置状态，**不会重新启用被禁用的 GameObject**。若后续发现胜利时把 Boss 物体 SetActive(false) 了，这块要补。
3. **`EncounterScope` 本身没有多 Boss 流程**：换 Boss / 连战编排属流程层（你说要做时需求才清楚），本批只做边界与复位。
4. **真正的复战入口 UI 没有**：F8 是临时触发器，不是产品功能。

---

## 5. 硬约束（后续改动必守）

1. **`ResetForEncounter()` 必须顺序无关且可重复调用。**
   所有组件都在各自 `Start` 里注册，Unity 不保证跨组件 `Start` 顺序，所以 `ResetAll()` 的倒序遍历**不构成任何顺序保证**。
   → 不要读别人还没复位的字段；需要初始值就从 `Config`（序列化 SO，整局不变）读，**不要从 body 的当前值读**。

2. **接口引用上的 `==` 不触发 `UnityEngine.Object` 销毁判定。**
   `EncounterScope.IsGone()` 已处理（`target is Object unityObj && unityObj == null`）。新增实现者时不要绕过它。

3. **新增"持有战斗状态"的组件，必须实现 `ICombatResettable` 并注册/注销。**
   漏一个，复战时它就带着上一场的残值进新一场。
   写法固定为：
   - `Start()` 里 `EncounterScope.Ensure()?.Register(this);`（**用 Ensure，不要写 `Current?.`**——各组件 Start 顺序不定，谁先跑到谁负责建 Scope）
   - `OnDestroy()` 里 `EncounterScope.Current?.Unregister(this);`（**这里必须用 Current，不能用 Ensure**——销毁时创建新对象毫无意义）
   - `Ensure()` 只能在 `Start` 及之后调用，**不可在 `Awake` 里调**，否则跨组件 Awake 顺序不定会重复创建。

4. **跨模块命名空间映射**（新建文件时先照抄 `CombatManager` 的 using 再删多余的，别从零手写）：
   - `CombatEventBus` / `DeflectType` / `GamePause` / `CombatInputGate` / `CursorController` → `ARPG.Mgr`
   - `AnimUtil` / `ArenaBoundary` → `ARPG.FrameWork`
   - `CharacterBody` → `ARPG.FrameWork.Body`
   - `Hitbox` / `Hurtbox` / `CombatFxPoint` / `EncounterScope` / `ICombatResettable` → `ARPG.Combat`
   - `FinisherKind` / `PostureBreakSource` → `ARPG.FrameWork.States`
   - `HitGrade` / `PerilousType` / `AttackHitboxSlot` → `ARPG.Configs`

5. **`PlayerRef` / `BossRef` 字段名不可改** —— 改名会丢失场景/prefab 已拖好的序列化引用。过渡期用 `ActivePlayer` / `ActiveBoss` 覆盖语义。

---

## 6. 验收完的收尾

- [ ] 删除 `Assets/Scripts/Combat/EncounterResetDebug.cs`（临时触发器，不应留在作品集里）
- [ ] 决定是否补 `08-encounter-reset.md` 设计文档，并在 `00-overview.md` 索引表登记
