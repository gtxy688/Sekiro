# 攻击前摇取消 / 格挡连按 设计

日期：2026-08-18  
状态：已确认，按此实现

## 目标

玩家（及共用 `AttackState` 的 Boss）在攻击前摇内可用格挡或垫步取消；格挡全程可被垫步取消，也可再按格挡刷新窗口。连按格挡会触发已有抖刀惩罚。

## 规则

1. **判定**：进入 `AttackState` 即开 Hitbox，退出即关。
2. **前摇取消**：`stateTimer < HitStartTime` 时 `DeflectCommand` / `DodgeCommand` 立刻切对应状态；`OnExit` 关 Hitbox。
3. **过了取消窗口**：本刀锁死，格挡/垫步 `return false`，走 0.2s 输入缓冲；挥砍在缓冲内结束则落地后接上。
4. **`HitStartTime = 0`**：进招不可取消（判定仍然一进攻击就开）。
4. **格挡全程**（抬刀 / 举刀 / `Deflect_Slash` / `Deflect_Cancel`）：
   - `DeflectCommand` → 新的 `DeflectState`（重播抬刀、重开窗口、`RegisterDeflectPress`）
   - `DodgeCommand` → `DodgeState`
5. **抖刀**：沿用 `CharacterBody.RegisterDeflectPress`（0.5s 内连点 ≥3 次，窗口 ×0.75，下限 0.1s）。不做新动画。
6. **不改**：连招窗口、跳跃/葫芦父状态强切、垫步不可被打断、危字放行。

## 实现落点

- `AttackConfig.cs`：新增 `HitStartTime`
- `AttackState.cs`：延迟开判定；前摇内取消
- `DeflectState.cs`：不再吞掉格挡/垫步命令
- `Docs/architecture/01-states.md`、`Docs/验收清单.md`：同步规则
