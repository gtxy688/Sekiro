# 射箭投射物设计

日期：2026-08-27  
状态：已确认，按此实现

关系：补 `03-hit-detection.md` 投射物判定、`07-anim-events.md` 出箭走时间轴。弓段近战 Hitbox 保持 NoHit。

## 目标

所有弓 Clip 在时间轴标出的时刻生成可见箭。箭匀速直线飞，SphereCast 扫玩家 Hurtbox，结算走 `CombatManager`。可弹反/格挡，垫步无敌可躲，非危字。伤害读**招式表**（招默认，段 `overrideCombat` 可盖），不读弓段烤出来的 NoHit `AttackConfig`。

## 不做

- 箭 Prefab 不挂 `Hitbox`、不挂 Collider，不用 OnTrigger
- 不追踪、不对象池、不与刀拼刀
- 不改行为树选招，不恢复 `BT_BowShot`
- 弓段不重新开刀 Hitbox
- 不把伤害写在箭上或 `AttackConfig` 烘焙副本里
- 不在 Clip 上加 `SpawnArrow` 动画事件

## 飞行脚本

新建 `ArrowProjectile`（MonoBehaviour，挂箭 Prefab）：

- 出箭时 `Fire(...)` 写入：主人、方向、速度、半径、层、寿命、血/架势/击退
- `LateUpdate`：沿方向匀速位移；上一帧位置 → 当前帧 SphereCast（与刀同一套防穿透）
- 扫到非主人的 `Hurtbox` → `CombatManager.ReportProjectileHit` → 销毁
- 超时销毁；命中只结算一次

## 出箭

数据：`ArrowSpawnCue { time }`，挂在 `BossMoveWindow.arrowCues` / `AttackConfig.arrowCues`。baker 无论 NoHit 都拷进运行时配置。`AttackState` 当 `t >= cue.time` 调一次 `CharacterBody.SpawnArrow()`。

`CharacterBody.SpawnArrow()`：公开方法，给时间轴用，不要当动画事件配。

1. 缺 `arrowSpawn` / `arrowPrefab` → Warning，不出箭
2. 缺 `CurrentMoveEntry` → Warning（不在出招中误触）
3. `AttackCombatResolve.Resolve(entry, window)` 取伤害
4. 瞄准 `CombatTarget` 的 `projectileAimPoint`，空则对方 Hurtbox 中心
5. 方向 = `(瞄准点 - 出箭点).normalized`，锁死
6. Instantiate 箭并 `Fire`

`BT_ExecuteMove.FireCurrentSegment` 在 `StartAttack` 前写入 `CurrentMoveEntry` / `CurrentMoveWindow`。清表行在 `ResetMove`，**不要**在 `AttackState.OnExit` 清（打断接重箭会把刚写的表行清掉）。

## 数据

Boss `CharacterBody`：

| 字段 | 建议 |
|------|------|
| `arrowSpawn` | 弓弦空物体 |
| `arrowPrefab` | 带 `ArrowProjectile` 的 Prefab |
| `arrowSpeed` | 32 |
| `arrowCastRadius` | 0.08 |
| `arrowLifetime` | 2 |
| `arrowTargetLayers` | 与刀 `targetLayers` 相同 |

玩家可选 `projectileAimPoint`（胸口）。

伤害：招式行 `baseDamage` / `postureDamage` / `knockback`；该弓段勾了 `overrideCombat` 则用段上三数。`Bow_Heavy` 用招上 25/30。

## 时间轴弓段

菜单 **ARPG → 攻击时间轴**，打开 `GenichiroMoveTable`，对下列**弓段**拖进度、点「加出箭」、保存。`Bow_Air5` 插 5 点。不要点「填入弦一郎默认招式表」。

`Bow_Shot`、`3011`、`Bow_Air5`、`Bow_Heavy`、`Bow_AirHeavy`、`3031`、`3019`、`3029`、`3036`、`3018`、`3034`。

`3015`、垫步、近战段不加。弓 Clip 不要 `EnableWeaponHit`，也不要 `SpawnArrow`。

## 清理

删除已不再挂树的射箭占位 `BT_BowShot`（及其它已声明删除但仍残留的旧叶子，若磁盘上还有）。
