# 09 - 阵营（Faction）手动验收清单

> 状态：**待验收**。本环境无 unityMCP，也无可用的 C# 编译器（csc 被安全策略拦截），**Unity 编译未验证**。
> **这是一次重构，不是新功能**——验收目标是「行为与改动前完全一致」。若玩起来有任何不一样，就是改错了，回来找我。
> 命名按 `0X-xxx-test.md` 惯例。**尚未在 `00-overview.md` 索引表登记**（08 也未登记，可一并补）。

---

## 0. 改了什么，为什么

### 问题

项目里有**三份** `IsPlayer`，而且用了**两套**判定标准：

| 位置 | 判定方式 |
|------|---------|
| `HitReactionUtil.IsPlayer` | `body == CombatManager.Instance.PlayerRef` —— 比对象引用 |
| `AudioManager.IsPlayer` | `GetComponent<PlayerBrain>() != null` —— 查组件 |
| `FXManager.IsPlayer` | 同 AudioManager —— 查组件 |

比引用的那套意味着：**除 `PlayerRef` 指向的那一个对象外，世界上没有任何东西能是"玩家"**。
加第二个 Boss、加友方 NPC、复战换角色，它一律判错。两套标准还可能给出矛盾答案。

### 改法

给每个角色加一个**阵营字段**，判断身份时查字段，不比对象。三份实现合并成一份。

| 文件 | 改动 |
|------|------|
| `Configs/Faction.cs` | **新增**。枚举 `Enemy = 0` / `Player = 1` |
| `FrameWork/Body/CharacterBody.cs` | 新增序列化字段 `faction`（默认 `Enemy`）+ 只读属性 `Faction` |
| `Combat/HitReactionUtil.cs` | `IsPlayer` 改查阵营字段 |
| `Audio/AudioManager.cs` | 删掉私有实现，改调 `HitReactionUtil.IsPlayer` |
| `Mgr/FXManager.cs` | 同上 |
| `Mgr/GameplaySettings.cs` | `target != BossRef` → `target.Faction != Faction.Enemy`；顺带去掉不再需要的 `CombatManager.Instance == null` 判空 |
| `FrameWork/Body/CharacterBody.cs` | `ResolveOpponentOverlap` 里 `this != PlayerRef` → 查阵营 |
| `FrameWork/States/Base/AttackStateBase.cs` | `body == PlayerRef` → `body.Faction == Faction.Player` |
| `FrameWork/States/Ground/GroundedState.cs` | 同上 |
| `Combat/CombatManager.cs` | 新增 `ValidateFaction()`：启动时玩家阵营不是 Player 就报错 |

### 两个刻意的设计取舍

1. **用枚举，不用 ScriptableObject。** 阵营是类型标签，不是可调数值；新增阵营必然伴随新逻辑，是代码级变更。用 SO 反而会引入「两个不同实例代表同一阵营」的隐患，判断时又得比引用——绕回原问题。

2. **不做自动推导，宁可手工设一次 + 启动报错。** 自动推导只有两条路，都有坑：
   - 靠 `GetComponent<PlayerBrain>()` → `PlayerBrain` 在 `ARPG.Player`（玩家层），而 `CharacterBody` 在 `ARPG.FrameWork.Body`（框架层）。让框架层引用玩家层是**下层依赖上层**，方向错了。
   - 靠 `CombatManager` 在 `Start` 里回填 → 各组件 `Start` 顺序不定，谁先查谁就拿到未回填的错值（和 `EncounterScope` 那次同一个坑）。

   序列化字段在 `Awake` 时就已就位，两条坑都绕开。代价是要手工设一次，但**选错会有启动报错**，不会静默。

---

## 1. 编译验证

**操作**：回到 Unity，等编译完成，看 Console。

**预期**：0 error。

若报错，最可能的两个点：
- **CS0103 `Faction` 不存在** → 漏 `using ARPG.Configs;`。已确认这些文件都有：`AttackStateBase` / `CharacterBody` / `HitReactionUtil` / `GameplaySettings` / `CombatManager`；`GroundedState` 本批补上了。
- **`Configs.Faction` 解析不了** → `CharacterBody` 内部为避开「属性名与类型名同名」写了完整限定 `Configs.Faction.Player`。若报找不到，改回 `Faction.Player`（C# 的 "Color Color" 规则允许这种写法，只是读起来绕），或直接写全 `ARPG.Configs.Faction.Player`。

---

## 2. 必须做的一步：把玩家设成 Player

**为什么必须手工**：`faction` 是新增字段，默认值 `Enemy`。旧 prefab / 旧场景对象反序列化出来**全是 Enemy**，玩家也不例外。

**操作**：
1. 在 Hierarchy 里选中玩家角色（就是 `CombatManager` 上 `PlayerRef` 拖的那个）
2. 找到 `CharacterBody` 组件，新增的 **阵营（Faction）** 下拉框
3. 从 `Enemy` 改成 **`Player`**
4. 保存场景 / 应用到 prefab

**验证**：进 Play，Console **不应**出现 `[CombatManager] 玩家角色「XXX」的 Faction 是 Enemy，应当是 Player`。出现了就是没改到。

> 不改不会崩。但玩家会被当成敌人，**静默失去**专属受击表现、无敌血、玩家侧音效与特效等一整批按身份分支的逻辑——这类错误最难查，所以专门加了启动报错。

---

## 3. 回归验收（核心：玩法必须和改之前一模一样）

Faction 是等价替换。逐个确认下面这些**行为没变**：

| # | 操作 | 预期（与改动前一致） |
|---|------|---------------------|
| 1 | 被 Boss 普通攻击打中 | 播放**玩家专属**受击动画，不是敌人的 |
| 2 | 格挡 Boss 攻击 | 火花特效、格挡音效都出（这些走 `IsPlayer`） |
| 3 | 完美弹反 | 弹反火花 + 弹反音效 + 顿帧，都在 |
| 4 | 开无敌血开关（`GameplaySettings.InfiniteHealth`） | **玩家不掉血**；Boss 该掉还是掉 |
| 5 | 开一键崩防开关 | **只有 Boss** 被一下打崩架势；玩家不会被一下打崩 |
| 6 | 玩家死亡 → 回生 | 回生计数、回生后 Boss 的连弹计数重置，都正常 |
| 7 | 弹反到 Boss 架势崩解 → 按攻击键 | 触发处决（这条走 `GroundedState` 的阵营判断） |
| 8 | Boss 被打 | **不**播放玩家专属受击动画（Boss 不走这套） |
| 9 | 玩家走到 Boss 身上 | 玩家被胶囊挤开，Boss 不动 |

> 第 4、5 项最能验出阵营设错：开关对玩家/Boss 的区分完全依赖 `IsPlayer`。

---

## 4. 已知未覆盖（不是 bug）

1. **`BossRef` 仍被 4 处用于「找对象」**（锁定目标、算朝向、挤开对手、回生重置连弹）。这些属于「找对手是谁」，不是「判身份」，本次不动。多 Boss 时要改成遍历 `EncounterScope.Opponents`，已在 `CharacterBody.ResolveOpponentOverlap` 留了 TODO。
2. **`AudioManager` / `FXManager` 里 `using ARPG.Player;` 现在没用了**（原本靠它查 `PlayerBrain`）。未使用的 using 零成本，删了有编译风险，暂留。
3. **`EncounterScope` 不自动填阵营**。将来做复战换 Boss 时，新 Boss 的 `faction` 要在 prefab 上就设成 `Enemy`（默认值就是，通常不用管）。

---

## 5. 硬约束

1. **判断身份一律查 `body.Faction`，禁止写 `body == CombatManager.Instance.PlayerRef`。**
   见到旧写法就改掉——它意味着「除这一个对象外没有东西能是玩家」。

2. **`IsPlayer` 全项目只有 `HitReactionUtil` 一份。** 别在别的类里再写私有副本，三份两套标准就是这么来的。

3. **新增角色 prefab 必须设 `faction`。** 默认是 `Enemy`；是玩家就改成 `Player`，忘了会有启动报错。

4. **`CharacterBody` 内部写 `Configs.Faction.X`，不写 `Faction.X`。**
   属性名与类型名同名（`Faction Faction`），虽然 C# 允许，但写全了更好读。类外部照常写 `Faction.Player`。

5. **区分「判身份」与「找对象」。**
   - 判身份（这是谁）→ 用 `Faction`
   - 找对象（对手是谁）→ 用 `EncounterScope.Opponents` / `PrimaryOpponent`，不要用 `PlayerRef` / `BossRef`
