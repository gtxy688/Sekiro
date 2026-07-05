# 回血系统

## 药葫芦

**按键：** E

**条件：** 次数>0 + 不在硬直/被控状态 + 不在不可打断动画中

**流程：**
1. 立即扣除 1 次使用次数
2. 播放喝药动画 0.8s
3. 动画播完 → 回复 30% MaxHP
4. 动画中途受击 → 打断，次数不退还，生命不恢复

## 参数

| 参数 | 值 |
|------|-----|
| maxHealingCharges | 10 |
| healPercent | 30% |
| healDuration | 0.8s |

## 事件

- `CombatEvents.OnHealingChargeChanged(int charges)` → UI 药葫芦计数
