# 05 玩家输入 / 锁定 - 验收清单

> 配合 `05-input-lockon.md` 使用。

## 前置准备

1. 玩家挂 `PlayerBrain` + `CharacterBody` + `LockOnManager`。
2. InputAction 资源已生成（`PlayerInputActions`）。
3. 场景有敌人（带 CharacterBody + Hurtbox），在锁定视野范围内。

## M6：输入绑定

| # | 操作 | 预期 |
|---|------|------|
| 1 | 按 WASD | 角色移动，切 MoveState |
| 2 | 按攻击键 | 切 AttackState，播攻击动画 |
| 3 | 攻击中快速按攻击键 | 连招缓冲生效（0.2s 预输入） |
| 4 | 按空格 | 跳跃，切 AirState |
| 5 | 按防御键 | 切 DeflectState |
| 6 | 按闪避键 (Shift) | 切 DodgeState |
| 7 | 按 Heal (R) | 葫芦使用，回血 + 数量减 1 |
| 8 | 按键时正在受击硬直 | 指令进缓冲池，硬直结束立即执行（预输入窗口） |

## M11：锁定

| # | 操作 | 预期 |
|---|------|------|
| 9 | 按 Focus 键 | 锁定最近的敌人，身上出现白点（M13） |
| 10 | 锁定中按 WASD 移动 | 角色面向始终朝向目标，位移按摇杆方向 |
| 11 | 再按 Focus | 解锁，白点消失 |
| 12 | 锁定中目标死亡 | 自动解锁 |
| 13 | 锁定中敌人跑出范围 | 自动解锁 |

## 常见问题

- **按键无反应**：确认 PlayerBrain 里按键绑定写了，且 InputAction 资源已 Enable。
- **锁定不动**：LockOnManager 的 FindTarget 逻辑（最近敌人）没写或没找到。
- **锁定中还能转身**：MoveState 里面向目标的分支没生效。
