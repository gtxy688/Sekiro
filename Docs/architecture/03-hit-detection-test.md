# 03 命中判定 - 验收清单（M3 BoxCast + M17 危字）

> 配合 `03-hit-detection.md` 使用。当前验收范围：**M3 命中链路 + M17 识破**。

## 前置准备

### 1. 双方组件（对称，缺一不可）

| 角色 | 身体（被打） | 武器（打人） |
|------|------------|------------|
| 玩家 | Capsule Collider（自带）+ `Hurtbox` 组件 | 刀身 + `Hitbox` 组件 |
| Boss | Capsule Collider + `Hurtbox` 组件 | `sword_joint` 骨骼（或子空节点）+ `Hitbox` 组件 |

> - **Hurtbox** = "我是目标"的标记，挂身体（跟 Capsule Collider 同一物体即可）
> - **Hitbox** = 代码扫描器（不是碰撞体！），挂刀刃；刀**不需要** Collider
> - **判定点**：刀刃中部的空节点（必须是骨骼/刀网格的**子级**，否则挥剑时判定点不跟刀动）；或直接挂武器骨骼 + 调大 castRadius

### 2. Hitbox 参数

| 参数 | 建议值 | 说明 |
|------|--------|------|
| `castRadius` | 0.1 ~ 0.2 | 扫描球半径 ≈ 刀身粗细；挂骨骼上时建议 0.2 |
| `targetLayers` | 对方角色所在层 | 玩家/Boss 放同一"战斗层"互相能扫到 |

### 3. 场景

- 放一个空物体挂 `CombatManager`（单例，自动 Awake）
- 玩家/Boss 都配好 `CharacterBody`（Config、Light Attack、GroundCheck）

### 4. 测试键（M1HitTest.cs 挂在玩家上）

- **K** = 开启武器判定（使用玩家 `LightAttack` 的配置）
- **L** = 关闭武器判定

## M3 验收

| # | 操作 | 预期 |
|---|------|------|
| 1 | Play，看 Console | 0 个编译错误 |
| 2 | 玩家按 **K** 开启判定，走到 Boss 面前让刀扫过 Boss 身体 | Boss 掉血（`atk1.BaseDamage`）+ 进 StunnedState 播受击动画 |
| 3 | 一次挥砍反复扫过 Boss | **只结算一次**（hitTargets 去重，看 Boss 只掉一次血） |
| 4 | 判定开启时扫向自己身体 | 不受伤（ReportHit 的 owner 排除） |
| 5 | 按 **L** 关闭判定后再扫 Boss | 无伤害 |
| 6 | 玩家和 Boss 都开判定，双方武器相交 | `ReportClash` 触发：双方涨架势 + `TriggerWeaponDeflected`（打铁火花/音效，配了资源才看得到） |
| 7 | 高速挥砍（动画快速摆动） | 能命中（上一帧→当前帧一段式扫描，防穿透） |

## M17：危字攻击

| # | 操作 | 预期 |
|---|------|------|
| 8 | Boss 放突刺（PerilousType.Thrust），玩家举盾防御 | 防御无效，玩家受伤（危字不可防） |
| 9 | Boss 放突刺，玩家按垫步 | 触发 MikiriCounterState，播踩刀动画，敌人架势大幅上涨 |
| 10 | 普通攻击时玩家垫步 | 无敌帧判定（M4 细化） |

## 常见问题

- **扫不到敌人**：按 K 后检查 `castRadius` 是否太小、`targetLayers` 是否含对方层、判定点是否跟骨骼动（挂错层级会原地不动）
- **敌人不掉血但进硬直**：看 `atk1.BaseDamage` 是不是 0（伤害数据全在 SO）
- **一次挥砍多次伤害**：hitTargets 去重失效（检查 Enable 时是否 Clear）
- **打到自己**：`ReportHit` 里 `attacker == target` 判断
- **扫到后无事件**：确认场景里有 `CombatManager`（单例）且没被禁用
- **危字防御无效不生效**：防御分支对 isPerilous 的判断没写（M4 实现盾反窗口时处理）
