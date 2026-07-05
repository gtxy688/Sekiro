# 架势条规则

## 变化规则

| 行为 | 对敌方架势 | 对己方架势 |
|------|-----------|-----------|
| 普攻命中 | +postureDamage | — |
| 完美弹刀 | +postureDamage × chainMultiplier | -8% max |
| 普通格挡 | — | +postureDamage × 0.3 |
| 未防御受击 | — | +postureDamage × 1.0 |
| 识破/踩头 | +postureDamage (大幅) | — |
| 脱战 >2s | 恢复 15%/s | 恢复 15%/s |

## 恢复逻辑

- 脱战 2s 后开始恢复，速度 15%/s
- 格挡状态→恢复速度 ×0.5
- 受击→重置恢复计时器

## 崩溃

- `currentPosture ≥ maxPosture` → IsBroken = true
- 玩家崩溃→ StunState (硬直 1.5s，无法操作)
- Boss 崩溃→ CollapseState (等待忍杀 2s)
- 事件：OnPostureBroken, OnPostureChanged(float current, float max)
