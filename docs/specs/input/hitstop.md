# 帧冻结 (HitStop)

## 机制

命中瞬间暂停游戏逻辑，渲染继续（画面定格）。

**流程：**
1. 命中事件 → `HitStopManager.Trigger(seconds)`
2. 下一帧起 `remaining > 0` → 跳过所有逻辑更新（物理/状态机/动画）
3. `Time.unscaledDeltaTime` 计时（不受 timeScale 影响）
4. 冻结期间输入缓冲正常接受输入

## 冻结时长

| 触发 | 时长 |
|------|------|
| 普攻命中 | 0.033s (2帧) |
| 弹刀 | 0.05s (3帧) |
| 识破 | 0.067s (4帧) |
| 架势崩溃 | 0.067s (4帧) |
| 忍杀 | 0.083s (5帧) |

## 接口

- `HitStopManager.Trigger(float seconds)`（全局静态）
- `HitStopManager.Update()`（每帧在最开始调用）
- `HitStopManager.IsActive`、`RemainingTime`
