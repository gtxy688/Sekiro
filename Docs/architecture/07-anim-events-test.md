# 07 攻击动画时间轴 - 手动验收清单

> 配合 07-anim-events.md 使用。攻击 Hitbox、攻击音效与箭矢由运行时攻击状态读取时间数据推进，不需要在攻击 Clip 上配置 EnableWeaponHit、DisableWeaponHit 或 SpawnArrow 事件。

## 前置准备

1. 打开 Unity 工程并等待资源导入、脚本编译。
2. 确认攻击时间轴工具可打开，测试场景中的角色、Animator、AttackConfig、BossMoveTable 和 Hitbox 引用有效。
3. 修改配置前保存场景与资源副本，避免覆盖已调好的招式表。

## 手动验收

| # | 操作 | 预期 |
|---|---|---|
| 1 | 打开攻击时间轴，预览配置的动画并拖动时间指针 | 动画预览与时间指针同步 |
| 2 | 调整一个近战判定窗口并保存，进入 Play 模式复测 | 只有配置的攻击窗口能命中，后摇关闭判定 |
| 3 | 在配置中添加音效 cue 并播放对应招式 | 到达 cue 时间后播放一次音效 |
| 4 | 在 Boss 招式窗口添加出箭 cue 并播放招式 | 到达 cue 时间后生成箭矢，箭命中由投射物路径结算 |
| 5 | 连续播放带多段 hitPulses 的攻击 | 各段按配置时间分别命中，同一段不会对同一目标重复结算 |
| 6 | 退出攻击、打断攻击或播放无近战判定的弓段 | Hitbox 关闭；不因前摇或弓段持续造成近战伤害 |

## 检查位置

- AttackConfig 的 hitPulses、sfxCues 与 arrowCues。
- BossMoveTable 的 BossMoveWindow、hitPulses、sfxCues 与 arrowCues。
- AttackStateBase、AttackAnimClock、WeaponController。
