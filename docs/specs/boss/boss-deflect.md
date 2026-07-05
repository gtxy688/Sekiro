# Boss 弹刀 AI

| 参数 | 值 | 说明 |
|------|-----|------|
| deflectWindow | 0.15s (9帧) | 窄于玩家 12帧 |
| blockWindow | 0.3s | 普通格挡窗口 |
| stanceDuration | 0.4s | 弹刀姿态时长 |

| AI 概率 | 值 | 条件 |
|---------|-----|------|
| deflectChanceOnAttack | 40% | 玩家普攻命中时 |
| deflectChanceInCombo | 60% | 玩家连招中 (2段+) |
| deflectChanceLowHealth | 25% | Boss HP<30% |

## 弹刀成功效果

1. 小火花特效 + 帧冻结 0.03s
2. 玩家架势×1.5 (posturePenaltyMultiplier)
3. 玩家硬直 0.3s (playerHitStunDuration)
4. Boss 获得反击窗口 → 立即接反击招式

AI 接口：`ShouldEnterDeflectStance(float bossHealthPercent) → bool`、`TryDeflect(AttackData, Vector3 attackDir, Vector3 bossForward) → DeflectResult`
