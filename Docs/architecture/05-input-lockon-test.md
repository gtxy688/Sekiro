# 05 玩家输入 - 验收清单（M6 输入绑定）

> 配合 `05-input-lockon.md` 使用。当前验收范围：**M6 输入绑定**。锁定相机切镜见 `06-presentation-test.md`。

## 前置准备

- Player 挂 `PlayerBrain` 组件（全部按键已在代码里绑定，无需手动配）
- InputAction 资源 `PlayerInputActions`（已生成，PlayerBrain 自动创建并 Enable）
- Animator 状态名已配好（见 `01-states-test.md` 第 3 节：Idle/Walk/Hurt_Ground/Jump + 攻击状态）
- `CharacterBody.Light Attack` 槽已拖入 atk1.asset（攻击要有配置才切状态）

## M6 验收（键盘/鼠标直接操作）

| # | 操作 | 预期 |
|---|------|------|
| 1 | 按 WASD | 角色移动（切 MoveState），位移由 Walk 动画根运动驱动 |
| 2 | 鼠标左键 | 切 AttackState，播 `atk1.asset` 的 AnimName 动画 |
| 3 | 攻击中快速连按左键 | 连招预输入：在 `ComboWindowStart~End` 窗口内按下 → 切到 `NextCombo` 配置的下一段（需要 atk1.asset 的 NextCombo 链配好） |
| 4 | 按空格 | 跳起切 AirState：先播 `Jump`（起跳，上升由动画 Root 驱动），过最高点自动切 `Fall`（下落） |
| 5 | 鼠标右键**按住** | 进 DeflectState（防御/盾反姿态）；**松手** → 自动回待机（PlayerBrain 在松手时发 IdleCommand） |
| 6 | 按 Shift | 垫步：位移由垫步动画 Root 驱动，`Config.DodgeDuration` 秒后回待机 |
| 7 | 按 R（血量不满时） | 葫芦生效：回血 `Config.HealAmount` + 葫芦数量减 1 |
| 8 | 受击硬直中按攻击 | 命令进缓冲池（0.2s），硬直结束**立即执行**（预输入） |
| 9 | 受击硬直中按移动/攻击/跳 | 全部被吞（StunnedState 拦截），硬直内不能动 |

> 按键映射速查：WASD=移动、左键=攻击、空格=跳、右键按住=防御、Shift=垫步、R=葫芦、中键=锁定（相机切到锁定跟随，见 `06-presentation-test.md`）。

## 常见问题

- **按键无反应**：确认 PlayerBrain 挂在角色上；Console 无报错；`PlayerInputActions` 资源没被误删
- **攻击键切不了状态**：`Light Attack` 槽位是否为空（空配置会安全回退 Idle）；Animator 里攻击状态名是否与 atk1.asset 的 AnimName 一致
- **防御松手不退**：DeflectState 是否收到 IdleCommand（PlayerBrain 的 Defend.canceled 已绑定）
- **跳跃没跳起来**：`Config.JumpSpeed` 是否 > 0
- **连招不生效**：atk1.asset 的 `NextCombo` 是否指向 atk2，且 `ComboWindowStart < ComboWindowEnd`

## 暂不验收（后续模块）

- 无（M11 锁定 + M12 锁定相机见 `06-presentation-test.md`）
