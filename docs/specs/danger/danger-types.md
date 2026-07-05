# 三种危

## 危类型

| 类型 | 攻击特征 | UI 符号 | 正确应对 | 错误后果 |
|------|---------|---------|---------|---------|
| 下段 (Sweep) | 低位横扫 | 红色→ | 跳跃→踩头 (空格) | 受伤+架势上升 |
| 突刺 (Thrust) | 直线前刺 | 红色↓ | 识破 (Shift无方向) | 受伤+架势上升 |
| 擒拿 (Grab) | 抓取 | 红色↑ | 闪避拉开 (Shift+方向) | 高伤害 |

## 危字提示

- 触发时机：Boss 进入攻击 Startup 时
- AttackData.attackType 为 Thrust/Sweep/Grab → 触发
- 提示持续：整个前摇 + 判定帧
- 隐藏：后摇开始时
- UI 提前量 `dangerUILeadTime=0.1s`
- 事件：`DangerSystem.OnDangerWarning(AttackType)`
