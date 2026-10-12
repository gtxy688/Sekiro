# 03 命中检测与结算

> 当前近战路径是动画时间驱动的 Hitbox 扫描，不是 BoxCast + Animation Event。手动验收见 03-hit-detection-test.md。

## 近战命中

AttackStateBase 读取 AttackAnimClock 的当前动画秒数，根据 AttackConfig 的 HitStartTime、RecoveryWindowStart 与 hitPulses 开启或关闭判定。进入攻击前摇不会因为贴身而提前挥中；结束或退出时会关闭 Hitbox。

Hitbox 在武器位置变化时执行 SphereCastAll 扫描，也使用 OverlapSphereNonAlloc / OverlapCapsuleNonAlloc 补充区域判定。已命中目标会去重；命中双方 Hitbox 时可以报告拼刀。实际扫描参数来自 Hitbox 和 CharacterBody 配置。

## 命中结算

Hitbox 将目标报告给 CombatManager。CombatManager 转交 CombatResolver；CombatResolver 从攻击者 Hitbox 配置与目标 Hurtbox 获取角色，并调用目标 CharacterBody.ReceiveHit。Hitbox 不直接扣目标生命值。

箭矢使用 ArrowProjectile 独立扫描，不挂近战 Hitbox。AttackStateBase 根据 arrowCues 调用 CharacterBody 发箭；WeaponController 从 BossMoveEntry、BossMoveWindow、ArrowSpawnCue 解析本次箭伤害，然后向 CombatManager 报告投射物命中。

## 数据路径

- 玩家近战伤害：AttackConfig。
- Boss 近战伤害：运行时攻击配置，来源于招式表与段 / 脉冲覆盖。
- 箭矢伤害：Boss 招式行、窗口与出箭点；缺少覆盖时可使用 AttackCombatResolve 的默认值。
- 危字攻击类型与 HitGrade 等枚举属于轻量配置类型。

## 结算边界

CombatResolver 负责近战、投射物和拼刀结算转发；CharacterBody 与状态机决定受击结果。表现事件由相关逻辑发给 CombatEventBus，UI / 音效不负责轮询战斗状态。

## 核心源码

- Combat/Hitbox.cs、Combat/Hurtbox.cs、Combat/ArrowProjectile.cs
- Combat/CombatManager.cs、Combat/CombatResolver.cs
- FrameWork/States/Base/AttackStateBase.cs
- FrameWork/AttackAnimClock.cs、FrameWork/Body/WeaponController.cs
