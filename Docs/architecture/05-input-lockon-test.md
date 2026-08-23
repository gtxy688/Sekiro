# 05 玩家输入 - 验收清单（M6 输入绑定）

> 配合 `05-input-lockon.md` 使用。当前验收范围：**M6 输入绑定**。锁定相机切镜见 `06-presentation-test.md`。

## 前置准备

- Player 挂 `PlayerBrain` 组件（全部按键已在代码里绑定，无需手动配）
- InputAction 资源 `PlayerInputActions`（已生成，PlayerBrain 自动创建并 Enable）
- Animator 状态名已配好（见 `01-states-test.md` 第 3 节：Idle/Walk/Hurt_Ground/Jump + 攻击状态）
- `CharacterBody.Light Attack` 槽已拖入 atk1.asset（攻击要有配置才切状态）
- `CharacterBody.Thrust Attack` 槽已拖入 `ThrustAttack.asset`，Animator 有 `Thrust`

## M6 验收（键盘/鼠标直接操作）

| # | 操作 | 预期 |
|---|------|------|
| 1 | 按 WASD | 角色移动（切 MoveState），位移由 Walk 动画根运动驱动 |
| 2 | 短按鼠标左键 | 松开时切 AttackState，播 `atk1.asset` 的 AnimName 动画 |
| 2b | 按住鼠标左键超过 `AttackHoldDuration` | 达到阈值时自动播 `Thrust`；继续按住不会重复出招 |
| 3 | 在后摇前不超过约 0.2s 再按一次攻击 | 命令先进入缓冲，在 `RecoveryWindowStart` 自动衔接 `NextCombo` |
| 3b | 在 `RecoveryWindowStart~ComboWindowEnd` 按攻击 | 立即衔接 `NextCombo` |
| 3c | 过早按攻击或晚于 `ComboWindowEnd` | 不错误衔接本段 `NextCombo` |
| 3d | 后摇窗口按移动/格挡/垫步/跳跃/喝药 | 立即取消后摇并执行对应行为 |
| 3e | 锁定攻击时 Boss 横向移动 | `RotationWindowEnd` 前玩家持续追踪 Boss；窗口结束后不再强转 |
| 3f | 未锁定攻击时输入不同方向 | `RotationWindowEnd` 前攻击朝输入方向调整 |
| 3g | 锁定攻击期间持续按移动，进入后摇 | 立即衔接 `Walk_Strafe`；不会插入 `IdleToWalk`，也不会额外朝 Boss 前冲 |
| 4 | 按空格 | 跳起切 AirState：先播 `Jump`（起跳，上升由动画 Root 驱动），过最高点自动切 `Fall`（下落） |
| 5 | 鼠标右键**按住** | 进 DeflectState（防御/盾反姿态）；**松手** → 自动回待机（PlayerBrain 在松手时发 IdleCommand） |
| 6 | 未锁定按 Shift | 播 `Dodge`，位移由垫步动画 Root 驱动，`Config.DodgeDuration` 秒后回待机 |
| 6b | 锁定后 W/S/A/D + Shift | 分别播 `Dodge_Forward` / `Dodge_Back` / `Dodge_Left` / `Dodge_Right`，身体朝 Boss；无方向只按 Shift → `Dodge_Back` |
| 7 | 按 R（血量不满时） | 葫芦生效：回血 `Config.HealAmount` + 葫芦数量减 1 |
| 8 | 受击硬直中按攻击 | 命令进缓冲池（0.2s），硬直结束**立即执行**（预输入） |
| 9 | 受击硬直中按移动/攻击/跳 | 全部被吞（StunnedState 拦截），硬直内不能动 |

> 按键映射速查：WASD=移动、左键=攻击、空格=跳、右键按住=防御、Shift=垫步、R=葫芦、中键=锁定（相机切到锁定跟随，见 `06-presentation-test.md`）。

## 常见问题

- **按键无反应**：确认 PlayerBrain 挂在角色上；Console 无报错；`PlayerInputActions` 资源没被误删
- **攻击键切不了状态**：`Light Attack` 槽位是否为空（空配置会安全回退 Idle）；Animator 里攻击状态名是否与 atk1.asset 的 AnimName 一致
- **防御松手不退**：DeflectState 是否收到 IdleCommand（PlayerBrain 的 Defend.canceled 已绑定）
- **跳跃没跳起来**：`Config.JumpSpeed` 是否 > 0
- **连招不生效**：检查 `NextCombo`，并确认 `HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration`
- **长按仍是普攻**：检查 `ThrustAttack` 槽与 `CharacterConfig.AttackHoldDuration`

## 暂停 / 自定义键位

| # | 操作 | 预期 |
|---|------|------|
| P1 | 战斗中按 Esc（或手柄 Start） | 弹出暂停：继续 / 设置 / 退出战斗；角色和 Boss 停住 |
| P2 | 点「继续」或再按 Esc | 菜单关掉，战斗恢复 |
| P3 | 点「设置」 | 进入键位页，默认「键盘鼠标」Tab，六行战斗键 |
| P4 | 点某一行，再按一个新键 | 该行显示新键名，回战斗后立即生效 |
| P5 | 把攻击改成垫步正在用的键 | 两行对调，不会两个动作同一键 |
| P6 | 切到「手柄」Tab 改 RT | 只动手柄绑定，键鼠页的键不变 |
| P7 | 改键等待中按 Esc | 取消本次改键，不关设置页 |
| P8 | 点「恢复默认」 | 当前 Tab 的六键回到资源默认值 |
| P9 | 点「退出战斗」 | 重载当前场景，timeScale 恢复为 1 |
| P10 | 改键后退出 Play 再进 | 上次改的键还在（PlayerPrefs） |

## 暂不验收（后续模块）

- 无（M11 锁定 + M12 锁定相机见 `06-presentation-test.md`）
