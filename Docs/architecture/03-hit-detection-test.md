# 03 命中判定 - 验收清单（M3 BoxCast + M17 危字）

> 配合 `03-hit-detection.md` 使用。当前验收范围：**M3 命中链路 + M17 识破**。

## 前置准备

### 1. 双方组件（对称，缺一不可）

| 角色 | 身体（被打） | 武器（打人） |
|------|------------|------------|
| 玩家 | Capsule Collider（自带）+ `Hurtbox` 组件 | 刀身 + `Hitbox` 组件 |
| Boss | Capsule Collider + `Hurtbox` 组件 | 刀：`sword_joint` 子节点 + `Hitbox`，拖到 `weaponHitbox`；Elbow 段：拳/指关节子节点 + `Hitbox`，拖到 `elbowHitbox` |

> - **Hurtbox** = "我是目标"的标记，挂身体（跟 Capsule Collider 同一物体即可）。**碰撞体必须包住身体**，过小会导致部分刀（尤其 Attack2）扫空。
> - **Hitbox** = 代码扫描器（不是碰撞体！），挂**武器中央**（刃中段）或肢体中段；刀/胳膊**不需要** Collider。挂偏（柄/手骨/根）时第一刀可能碰巧中、后面刀漏。
> - **判定点**：必须是骨骼/刀网格的**子级**，否则挥剑时判定点不跟刀动。
> - **多槽**：`AttackConfig.HitboxSlot` / 招式表 `hitboxSlot` 选本段用哪把。同时只亮一把；肘未拖则回退刀。

### 2. Hitbox 参数

| 参数 | 建议值 | 说明 |
|------|--------|------|
| 刀 `castRadius` | 0.1 ~ 0.2 | 扫描球半径 ≈ 刀身粗细；挂骨骼上时建议 0.2 |
| 拳 `castRadius` | 0.15 ~ 0.25 | 拳头大小；父物体用**手骨**，不要用肘骨（否则胶囊会扫整条小臂） |
| `targetLayers` | 对方角色所在层 | 玩家/Boss 放同一"战斗层"互相能扫到 |

### 3. 场景

- 放一个空物体挂 `CombatManager`（单例，自动 Awake）
- 玩家/Boss 都配好 `CharacterBody`（Config、Light Attack、GroundCheck）

### 4. 验收方式

Play `GameScene` 后用玩家普攻（点按）验收，不要再挂临时开闭 Hitbox 的测试脚本。攻击状态会按 `AttackConfig` 自己开关判定。

## M3 验收

| # | 操作 | 预期 |
|---|------|------|
| 1 | Play，看 Console | 0 个编译错误 |
| 2 | 走到 Boss 面前点按攻击，让刀扫过 Boss 身体 | Boss 掉血（`atk1.BaseDamage`）+ 进 StunnedState 播受击动画 |
| 3 | 一次挥砍反复扫过 Boss | **只结算一次**（hitTargets 去重，看 Boss 只掉一次血） |
| 4 | 挥砍扫向自己身体 | 不受伤（ReportHit 的 owner 排除） |
| 5 | 不按攻击、只走近 Boss | 无伤害（Hitbox 未开） |
| 6 | 玩家和 Boss 同时出刀，双方武器相交 | `ReportClash` 触发：双方涨架势 + `TriggerWeaponDeflected`（打铁火花/音效，配了资源才看得到） |
| 7 | 高速挥砍（动画快速摆动） | 能命中（上一帧→当前帧一段式扫描，防穿透） |
| 7b | Attack1 打中后接 Attack2 | 第二刀也能打中（Hitbox 在武器中央 + Boss 碰撞体包住身体） |
| 7c | 进入 `RecoveryWindowStart` 后再贴身（刀已进入可取消段） | 不再掉血；没取消则动画仍播到 `StateDuration` |

## M17：危字攻击

| # | 操作 | 预期 |
|---|------|------|
| 8 | Boss 放突刺（PerilousType.Thrust），玩家**举盾**（弹反窗口已过） | 格挡无效，全额受伤并进受击（普通格挡等于没防） |
| 8b | Boss 放突刺，玩家在**弹反窗口内**弹刀 | 仍弹开，Boss 硬直 |
| 8c | Boss 放 `Elbow`（Grab），举盾挨打 | 同 8：没防；窗口内弹反仍有效 |
| 8d | Boss 放 `JumpThrust` 起跳阶段 | **不弹危字**；起跳可被打但招不中断（霸体） |
| 8e | `JumpThrust` 落地 | 一定是突刺；弹「危」；应对同 8/9（可识破） |
| 9 | Boss 放突刺，玩家**不按方向**只按垫步 | 触发 MikiriCounterState，播踩刀动画，敌人架势大幅上涨；Boss 保持被打断时朝向，不反向、不追着玩家转 |
| 9b | Boss 放突刺，玩家**按后/左/右**再垫步 | 不识破；无敌帧内躲开或硬直后挨打；Boss 不进 `Mikiri_Deflect` |
| 10 | 普通攻击时玩家垫步 | 无敌帧判定（M4 细化） |

## 肘击 Hitbox 槽（Slash_SpinElbow）

前置：Boss `elbowHitbox` 已拖**拳头**采样点；刀仍在 `weaponHitbox`（或自动找到刀且不是拳）。

| # | 操作 | 预期 |
|---|------|------|
| 11 | 普通挥砍（玩家或 Boss 刀） | 仍用刀 Hitbox，手感与现在一致 |
| 12 | `Slash_SpinElbow` 第一段 `Slash_Spin` | 刀扫到才结算；肘采样点不开 |
| 13 | 第二段 `Elbow`，贴身让**拳**撞到玩家 | 结算一次；危字 Grab；双方播 `Elbow_Danger`（投技）；弹反窗口内可弹；**普通格挡等于没防**；垫步可躲；识破不触发 |
| 13b | Elbow 投技播出时按攻击/垫步/跳跃 | 双方都不切其他动作，直到 `Elbow_Danger` 播完回 Idle |
| 14 | `Elbow` 段刀从身侧刮过、拳没碰到 | 不结算（证明切到了拳槽，不是刀） |
| 15 | Boss 未拖 `elbowHitbox` 仍放肘击 | 回退刀 + Console Warning，不报错 |
| 16 | 退出攻击 / 被弹开 | 肘 Hitbox 关闭，不会残留扫描 |
| 16b | 贴身挨 `Boat` 飞舟连段 | 多段都能打中或弹到；弹反**不打断**后续刀。不要整段挥空 |

## 弓段关闭近战 Hitbox

贴身让刀碰到玩家，下列招的**弓段**不应掉血（刀段仍应打中）。不要依赖把红条缩到 0.01s。

| # | 操作 | 预期 |
|---|------|------|
| 17 | `Bow_Shot`、`Bow_Heavy`、`Bow_Air5`、`Bow_AirHeavy` 全程贴身 | 刀/拳 Hitbox 不亮，无近战伤害 |
| 18 | `Bow_ThenSlash` / `Kengeki_Bow2Slash` 第一段贴身，第二段（3015）挥到 | 弓段不伤；刀段落一次 |
| 19 | `Slash_RushThenBow` 第一段横砍、第二段 3011 贴身 | 砍中；射箭段不伤 |
| 20 | `Kengeki_Bow`、`Kengeki_JumpBow` 前段贴身 | 弓段不伤；若有 3015 刀段仍打中 |
| 21 | 时间轴打开弓段 | 无 0.01s 假红条；「关闭近战判定」后 HitStart=Recover=时长 |

## 射箭投射物

前置：Boss 已拖 `arrowSpawn` / `arrowPrefab` / `arrowTargetLayers`；箭 Prefab 只有模型 + `ArrowProjectile`（无 Hitbox、无 Collider）；时间轴已给弓段插出箭点并保存（`Bow_Air5` 5 点）。Clip 上没有 `SpawnArrow`、没有 `EnableWeaponHit`。玩家可选胸口 `projectileAimPoint`。

| # | 操作 | 预期 |
|---|------|------|
| 21b | `Bow_Shot`，站远处被箭打中 | 可见箭飞来；掉 Mid 箭伤 **15/15**；刀 Hitbox 不亮 |
| 21c | `Bow_Heavy` 被箭打中 | 掉 Heavy 箭伤 **20/20**，不是刀 Heavy 的 25 |
| 21d | 箭飞来时弹反窗口内 | 弹反成功，不受伤；**Boss 架势不变、不进 `Deflected`** |
| 21e | 箭飞来时垫步无敌 | 不受伤 |
| 21f | 贴身 `Bow_Shot` | 仍出箭，不靠刀判定 |
| 21g | `Bow_Air5` 时间轴 5 个出箭点 | 同一动画出 5 支箭；前 4 支 Light **10/10**，最后一支 Mid **15/15** |
| 21h | `ARPG/招式伤害` 展开 `Bow_Air5` 弓段 | 列出箭 1～5，可单独勾覆盖改伤/等级 |

## 段/刀伤害（ARPG/招式伤害）

前置：菜单 `ARPG/招式伤害` 打开 `GenichiroMoveTable`。测完把覆盖勾掉，避免污染默认数值。

| # | 操作 | 预期 |
|---|------|------|
| 22 | 不勾任何覆盖，打 `Slash_Rush2` 两刀 | 两刀都是招上的 Light 刀伤 **10/10** |
| 23 | 展开 `Slash_Rush2`，只给刀 2 勾覆盖，血量改成 20，第一刀仍继承 | 第一刀掉 10；第二刀掉 20 |
| 24 | 展开 `Slash_SpinElbow`，旋斩段 Mid | 旋斩 15/15；肘击 Light 10/10 |
| 25 | 展开 `Bow_Shot` / `Bow_Heavy` | 弓段标「·箭」，可勾覆盖；`Bow_Heavy` 默认 20/20 |
| 26 | 改完伤害后打开时间轴保存该段判定 | 伤害覆盖还在，没有被时间轴冲掉 |
| 27 | `Slash_Heavy` 等级 Mid，裸吃 | 玩家播 `Hurt_Mid`，掉 15/15 |
| 28 | `Kick` 第二段等级 Heavy，裸吃踢 | 玩家播 `Hurt_Heavy`，掉刀 Heavy **25/25** |
| 29 | 打开招式伤害窗口 | 每招有等级下拉；段/刀/箭勾覆盖后能改；改等级会按默认表填数字 |

## 常见问题

- **扫不到敌人**：`castRadius` 是否太小、`targetLayers` 是否含对方层、**Hitbox 是否在武器中央**（挂错层级会原地不动或只扫空处）、**对方 Hurtbox 碰撞体是否包住身体**
- **只有第一刀中、第二刀空**：优先查玩家 Hitbox 位置和 Boss 碰撞体大小
- **敌人不掉血但进硬直**：看 `atk1.BaseDamage` 是不是 0（伤害数据全在 SO）
- **一次挥砍多次伤害**：hitTargets 去重失效（检查 Enable 时是否 Clear）
- **打到自己**：`ReportHit` 里 `attacker == target` 判断
- **扫到后无事件**：确认场景里有 `CombatManager`（单例）且没被禁用
- **危字防御无效不生效**：防御分支对 isPerilous 的判断没写（M4 实现盾反窗口时处理）
