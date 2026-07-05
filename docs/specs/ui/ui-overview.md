# UI 元素与布局

所有 UI 更新通过 `CombatEvents` 事件驱动，不直接查询战斗模块。

## 布局

```
左上角:  玩家头像 + 血条(红) + 架势条(白/灰)
上方居中: Boss "苇名弦一郎" + 血条(红) + 架势条(黄)
右下角:  药葫芦 (E图标 + 次数)
屏幕中央: 危字提示 / 阶段转换文字 / 结算画面
敌人头顶: 忍杀提示 (架势归零时闪烁)
敌人脚下: 锁定标记 (World Space)
命中位置: 伤害数字 (对象池)
```

## 元素清单

| 元素 | 事件驱动 |
|------|---------|
| 玩家血条 | OnPlayerDamaged |
| 玩家架势条 | OnPlayerPostureChanged |
| Boss 血条 | OnBossDamaged |
| Boss 架势条 | OnBossPostureChanged |
| 药葫芦 | OnHealingChargeChanged |
| 锁定标记 | OnLockOnChanged |
| 危字提示 | DangerSystem.OnDangerWarning |
| 忍杀提示 | OnBossPostureBreak |

## 血条/架势条更新

- 受伤→血条立即缩减，0.3s 后白色残影条跟随
- 架势增加→立即增长；恢复→缓慢缩减
- 架势 > 80% → 变红闪烁预警
- 崩溃→播放破碎动画
