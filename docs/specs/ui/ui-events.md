# UI 事件监听映射

| 事件 | UI 响应 |
|------|--------|
| OnPlayerDamaged(float) | 血条缩减 + 伤害数字(白) |
| OnBossDamaged(float) | 血条缩减 + 伤害数字(白) |
| OnPlayerPostureChanged(cur, max) | 架势条更新 + >80%变红闪烁 |
| OnBossPostureChanged(cur, max) | 架势条更新 |
| OnPerfectDeflect | 伤害数字(黄"弹") + 屏幕闪光 |
| OnNormalBlock | 伤害数字(灰"挡") |
| OnMikiriCounter | 伤害数字(蓝"识破") + 屏幕震动 |
| OnBossPostureBreak | 忍杀提示闪烁(红色) |
| OnPlayerPostureBreak | 屏幕边缘红色脉冲 |
| OnDeathblow | 隐藏忍杀提示 + 全屏闪白 |
| OnHealingChargeChanged(int) | 右下角药葫芦计数更新 |
| OnDangerWarning(AttackType) | 红色危字 + 方向符号 |
