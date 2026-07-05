# Boss 数值与验收

## Boss 属性

| 属性 | 值 |
|------|-----|
| maxHealth | 1000 |
| maxPosture | 300 |
| attack | 100 |
| postureRecoveryRate | 10%/s |
| moveSpeed | 中等 |

## Boss 弹刀参数

| 参数 | 值 |
|------|-----|
| posturePenaltyMultiplier | 1.5 |
| playerHitStunDuration | 0.3s |
| hitStopDuration | 0.03s |

## 事件输出

- `CombatEvents.OnBossPostureBreak` → 架势崩溃
- `CombatEvents.OnDeathblow` → 被忍杀
- `CombatEvents.OnBossDamaged(float damage)` → 受击

## 验收标准

1. AI 根据距离和权重动态选招，含 9 种招式
2. Boss 弹刀窗口 9帧 (< 玩家 12帧)
3. 弹刀概率 40%→60%→25% (普攻/连招/低血量)
4. 玩家连砍 3 段→Boss 60% 弹刀概率
5. Boss 弹刀成功→玩家架势×1.5 + Boss 反击窗口
6. 架势归零→崩溃→忍杀
