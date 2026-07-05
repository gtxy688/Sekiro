# 架势条参数与验收

## 参数表

| 参数 | 值 | 说明 |
|------|-----|------|
| playerMaxPosture | 300 | 玩家最大架势 |
| bossPhase1MaxPosture | 300 | 弦一郎一阶段 |
| postureRecoveryRate | 15%/s | 恢复速度 |
| postureRecoveryDelay | 2s | 脱战后延迟 |
| blockRecoveryMultiplier | 0.5 | 格挡时恢复系数 |
| playerStunDuration | 1.5s | 玩家崩溃硬直 |
| bossCollapseDuration | 2s | Boss 崩溃时长 |

## 验收标准

1. 普攻→敌方架势增加；完美弹刀→自身恢复 8%、敌方大幅增加
2. 普通格挡→自身稍增
3. 脱战 2s→15%/s 恢复，格挡中减半
4. 受击重置恢复计时器
5. current ≥ max→触发崩溃事件
6. 玩家→StunState(1.5s)，Boss→CollapseState(2s)
