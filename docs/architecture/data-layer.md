# ScriptableObject 数据层

所有战斗参数通过 ScriptableObject 配置，禁止代码硬编码。

## 资产类型

| 资产 | 字段 | 说明 |
|------|------|------|
| PlayerStats | maxHealth/posture/recovery/attack/defense/moveSpeed/dodgeSpeed | 玩家基础属性 |
| PlayerStats | baseDeflectWindow/minWindow/spamReset/windowReduction/chainMultipliers | 弹刀参数 |
| PlayerStats | maxHealingCharges/healPercent/healDuration | 回血参数 |
| BossStats | phase1/phase2 MaxHealth,MaxPosture,Attack,Attacks[] | Boss 双阶段 |
| BossStats | phaseTransitionHealthPercent/attackSpeedMultiplier | 阶段转换 |
| AttackData | attackName,animName,damage,postureDamage,attackType,canBeDeflected,帧数据,hitbox | 攻击动作 |
| DeflectConfig | 玩家弹刀 + Boss 弹刀 + AI 概率 | 弹刀全局配置 |
| CombatConfig | normalHitStop/deflectHitStop/mikiriHitStop | 帧冻结 |
| CombatConfig | postureRecoveryDelay/inputBufferWindow | 全局参数 |

## 使用

- 创建：Unity Editor → 右键 → Create → Combat → [类型]
- 存放：`Assets/ScriptableObjects/{Player,Boss,Combat}/`
- 引用：`[SerializeField]` 注入 → 运行时只读
- **禁止**：在代码中写 `float damage = 100f;`，必须从 SO 读取
- **禁止**：运行时修改 SO 本身（使用运行时数据类如 PostureSystem）

## 数据资产清单

| 资产 | 路径 |
|------|------|
| PlayerStats_Default | ScriptableObjects/Player/ |
| BossStats_Genichiro | ScriptableObjects/Boss/ |
| DeflectConfig_Default | ScriptableObjects/Combat/ |
| CombatConfig_Default | ScriptableObjects/Combat/ |
| AttackData_* | ScriptableObjects/Boss/Attacks/ |
