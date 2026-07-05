# 弹刀参数与验收

## 参数表

| 参数 | 值 | 说明 |
|------|-----|------|
| baseDeflectWindow | 0.2s (12帧) | 完美弹刀窗口 |
| minDeflectWindow | 0.016s (1帧) | 抖刀后最小窗口 |
| spamResetTime | 0.5s | 抖刀自动重置时间 |
| windowReductionPerSpam | 0.015s | 每次抖刀窗口缩减 |
| deflectChainResetTime | 1.0s | 连续加成重置时间 |
| deflectPostureRecovery | 8% | 完美弹刀恢复自身架势 |
| blockDamageReduction | 60% | 格挡减伤比 |
| blockPostureIncrease | 30% | 格挡自身架势上升 |

## 验收标准

1. 右键轻点进入弹刀状态 0.2s，窗口内受击→完美弹刀
2. 完美弹刀→0伤害 + 自身架势-8% + 敌方架势+(×倍率) + OnPerfectDeflect
3. 普通格挡→HP×0.4 + 架势×0.3 + OnNormalBlock
4. 快速连点→窗口递减至最低 1帧
5. 成功弹刀或停按 >0.5s→惩罚重置
6. 连续弹刀加成：1.0→1.2→1.4→1.5 封顶
7. 攻击夹角 >90°→None
