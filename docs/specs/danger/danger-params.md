# 危字参数与验收

## 识破参数

| 参数 | 值 | 说明 |
|------|-----|------|
| mikiriDistance | 3.0m | 最大触发距离 |
| mikiriAngle | 60° | 最大夹角 |
| mikiriPostureDamage | 50 | 架势伤害 |
| mikiriEnemyStunDuration | 0.5s | 敌人硬直 |
| mikiriHitStopDuration | 0.067s (4帧) | 帧冻结 |

## 踩头参数

| 参数 | 值 | 说明 |
|------|-----|------|
| stompPostureDamage | 30 | 架势伤害 |
| stompHorizontalRange | 1.5m | 水平判定范围 |
| stompVerticalOffset | 0.5m | Y偏移容忍 |
| stompHitStopDuration | 0.05s (3帧) | 帧冻结 |

## 闪避参数

| 参数 | 值 | 说明 |
|------|-----|------|
| dodgeInvincibilityFrames | 6-8帧 | 无敌帧 |
| dodgeSpeed | 10m/s | 闪避速度 |
| dodgeCooldown | 0.3s | 冷却 |

## 验收标准

1. 危字攻击→红色危字+方向符号
2. 下段→跳跃规避+踩头架势+30
3. 突刺→距离<3m+角度<60°→Shift识破
4. 识破成功→踩刀+音效+帧冻结+敌方架势+50+硬直0.5s
5. 不满足→普通闪避（无惩罚）
6. 擒拿→闪避规避（无架势奖励）

## 验收清单

- [ ] 操作：触发突刺危后按 Shift 识破
      预期：对应参数生效（距离<3m + 角度<60°，验收标准第3-4条）
- [ ] 操作：下段扫击跳跃踩头
      预期：敌方架势+30（第2条）
- [ ] 操作：非突刺按 Shift
      预期：普通闪避无惩罚（第5条）
