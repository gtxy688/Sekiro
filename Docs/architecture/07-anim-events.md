# 07 攻击动画时间轴与动画事件边界

> 文件名为兼容既有链接保留；攻击判定、音效和出箭主要由 AttackStateBase 读取动画时间驱动。

## 攻击时间轴

AttackStateBase 更新时读取 AttackAnimClock 的动画秒数，并据此推进攻击配置：

- HitStartTime 与 hitPulses 控制近战 Hitbox 判定。
- RecoveryWindowStart、ComboWindowEnd 与 StateDuration 控制取消、连招与退出。
- sfxCues 到点时发送攻击音效事件。
- arrowCues 到点时调用发箭路径；箭伤害从 Boss 招式数据解析。

玩家招式时间通常存在 AttackConfig。Boss 攻击使用 BossMoveWindow 等招式表数据，并由 BT_ExecuteMove 烘焙为运行时攻击配置。

## 与 Unity Animation Event 的边界

不要把 EnableWeaponHit、DisableWeaponHit、SpawnArrow 当作攻击 Clip 必须配置的 Animation Event。上述攻击时序由运行时攻击状态和配置时间推进。忍杀、抓取等其它流程的表现入口以相应状态 / 组件源码为准，不由本文件推断为攻击时间轴的一部分。

## 编辑器工具

AttackTimelineWindow 和 BossMoveDamageWindow 用于编辑攻击时间与招式窗口。编辑工具写入配置资源；运行时仍由 AttackStateBase、AttackAnimClock 和 WeaponController 读取并执行。

## 核心源码

- FrameWork/AttackAnimClock.cs
- FrameWork/States/Base/AttackStateBase.cs
- FrameWork/Body/WeaponController.cs
- Editor/AttackTimelineWindow.cs
- Editor/BossMoveDamageWindow.cs
