# 02 战斗数据 - 验收清单

> 配合 `02-combat-data.md` 使用。

## 前置准备

1. 在 Project 面板创建两个 CharacterConfig 资源：`PlayerConfig`、`GenichiroConfig`。
2. 配好数值（如玩家 HP=1000、Boss HP=2000、架势 100、葫芦 10、复活 1）。
3. 玩家角色挂 `CharacterBody`，Inspector 把 `PlayerConfig` 拖进 `Config` 槽。
4. Boss 挂 `CharacterBody`，拖 `GenichiroConfig`。

## M2：属性初始化

| # | 操作 | 预期 |
|---|------|------|
| 1 | 进 Play 模式 | Console 无报错 |
| 2 | 写一行 Debug 打印 CurrentHP | 等于 PlayerConfig 里配的 maxHP（1000） |
| 3 | 手动调 `TakeDamage(100, 30)` | CurrentHP 变 900，CurrentPosture 变 30 |

## M9：架势系统

| # | 操作 | 预期 |
|---|------|------|
| 4 | 连续调 TakeDamage 把架势涨满 | CurrentPosture 封顶在 maxPosture，不溢出 |
| 5 | 架势满时 | 触发 OnPostureBroken 事件（Console 打印验证） |
| 6 | 停止受击超过 postureDecayDelay 秒 | 架势开始自动下降（每次 Update -postureDecayRate） |

## M16：葫芦

| # | 操作 | 预期 |
|---|------|------|
| 7 | 玩家受点伤，调 `UseGourd()` | GourdRemaining 减 1，CurrentHP 回复到上限封顶 |
| 8 | 葫芦用完（GourdRemaining=0）再调 | 返回 false，不加血 |
| 9 | HP 已满时调 | 返回 false，不浪费葫芦 |

## M14：复活

| # | 操作 | 预期 |
|---|------|------|
| 10 | 把玩家 HP 打到 0 | 触发 OnReviveAvailable（弹复活提示） |
| 11 | 按复活键（R） | 回满血 + 架势清零，ReviveRemaining 变 0 |
| 12 | 再死一次 | 直接触发 OnDeath（不弹复活提示） |

## 常见问题

- **数值没生效**：确认 CharacterConfig 资源已拖进 CharacterBody.Config 槽，且 InitCombat 在 Awake 调用。
- **葫芦没用但次数减少**：UseGourd 里 HP 已满时 return false 没写。
- **架势不回复**：检查 postureDecayDelay 计时逻辑。
