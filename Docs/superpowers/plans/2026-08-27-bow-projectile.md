# 射箭投射物实现计划

> **面向 AI 代理的工作者：** 当前会话已按此实现。验收用 `03-hit-detection-test.md` 射箭节 + `07-anim-events-test.md` 第 11–12 条。

**目标：** 所有弓 Clip 在时间轴 `arrowCues` 时刻出箭；伤害读招式表；箭不挂 Hitbox。

**架构：** 招式表 `arrowCues` → baker 拷进 `AttackConfig` → `AttackState` 到点 `SpawnArrow` → `ArrowProjectile` 直线 SphereCast → `CombatManager.ReportProjectileHit`。`BT_ExecuteMove` 写入当前表行/段。

**技术栈：** Unity 2022 LTS、现有 CombatManager / ReceiveHit。出箭与 `sfxCues` 同一套时间轴，不写 Animation Event。
