# ScriptableObject 与配置工具链

这一篇回答：战斗数值为什么不直接写在状态脚本里，以及 Boss 招式如何从配置进入运行时。

## 1. 三类核心配置

### CharacterConfig

描述角色级长期参数：

- 最大生命、最大架势与架势恢复。
- 格挡、弹反、闪避和识破窗口。
- 受击时长与动画名。
- 移动、跳跃和转向参数。
- 命数、葫芦和回生次数。
- Boss 被动防御与喝药惩罚参数。

玩家和 Boss 共用 `CharacterBody`，但各自引用独立配置，所以共用框架不等于共用数值。

### AttackConfig

描述一段可以执行的攻击：

- 动画名与过渡时间。
- 生命伤害、架势伤害、击退和受击等级。
- 危字类型与 Hitbox 槽位。
- 攻击脉冲、连招窗口、取消窗口和状态总时长。
- 攻击转向。
- 音效时间点与出箭时间点。
- 下一段连招。

玩家攻击通常直接引用磁盘上的 `AttackConfig` 资产。

### BossMoveTable

描述 Boss 的招式集合。每个 `BossMoveEntry` 同时包含：

- 选招数据：距离、权重、冷却、层级和额外条件。
- 执行数据：动画序列、段窗口和默认战斗数值。

`BossMoveWindow` 再描述每一段的判定脉冲、危字、Hitbox、音效和出箭时间。

## 2. 为什么使用 ScriptableObject

- 数值与逻辑分离，调参无需修改状态代码。
- 多个角色实例可以引用同一份只读配置。
- Inspector 能直接编辑并序列化资源。
- 配置可被自定义 Editor、检索和构建校验统一处理。

但运行时状态不能写回共享 SO，否则多个实例或多次 Play 会互相污染。

## 3. Boss 为什么需要运行时烘焙

Boss 的一条招式可能有多个动画分支和多段窗口。`BT_ExecuteMove` 确认当前段后，`BossAttackBaker` 将 `BossMoveEntry + BossMoveWindow` 转成临时 `AttackConfig`：

```text
行为树选中 BossMoveEntry
→ 选择一个可播放序列
→ 取得当前 BossMoveWindow
→ BossAttackBaker.Bake
→ HideAndDontSave 的运行时 AttackConfig
→ AttackStateBase 按统一接口执行
```

这样 Boss 数据结构可以适合 AI 选招，同时攻击状态只需要理解一种 `AttackConfig`。

## 4. 配置的唯一真相

命中窗口以 `hitPulses` 为准。`HitStartTime`、`RecoveryWindowStart` 和 `ComboWindowEnd` 分别承担前摇取消、后摇开放和连招节奏，不应再被当作另一套独立命中窗口。

磁盘上的玩家 `AttackConfig` 会在 `OnValidate` 中从首尾脉冲同步部分字段；Boss 运行时烘焙保留招式表自己的节奏数据，避免两类配置互相覆盖。

## 5. 攻击时间轴工具

`AttackTimelineWindow` 把一段招式画成时间轴，用于可视化编辑：

- 一条或多条命中脉冲。
- 连招与恢复窗口。
- 音效时间点。
- 出箭时间点。
- 多段招式各自的持续时间。

它解决的是“多个秒数字段在普通 Inspector 中难以比较”的问题。工具提高调参效率，但最终运行时仍读取 SO 数据，不依赖编辑器窗口存在。

## 6. 配置校验

`ArpgValidationRules` 统一检查玩家攻击和 Boss 招式表：

- id 为空或重复。
- `minRange > maxRange`、权重不可用。
- 动画序列为空，或 Animator 中不存在状态名。
- 状态时长非正数。
- 窗口为负或只有极短“假红条”。
- 连招窗口顺序错误或超过状态时长。
- HitPulse 区间非法或越界。
- 音效、出箭时间点越界。
- 音效时间点没有 AudioClip。

错误可在构建前阻止明显坏配置进入 Player，警告则提示可疑但可能有意的设置。

## 7. 代价

- SO 字段多，配置之间仍有一致性风险。
- 字符串动画名缺乏编译期检查，因此必须依赖校验工具。
- Boss 运行时创建临时 SO，会产生对象分配；当前单 Boss 低频创建可接受。
- 配置工具本身也需要维护，字段语义变化时必须同步更新编辑器与校验规则。

Editor API 的具体写法单独见：[Unity Editor 扩展：从零到攻击时间轴](10-UnityEditor扩展从零.md)。

## 面试追问梯度

### 基础概念

1. ScriptableObject 与普通 C# class、MonoBehaviour 有什么区别？
2. SO 资产与运行时 `CreateInstance` 出来的对象有什么区别？

### 项目实现

3. CharacterConfig、AttackConfig、BossMoveTable 为什么不能合成一张表？
4. BossAttackBaker 为什么要生成临时 AttackConfig？
5. `hitPulses` 为什么是命中窗口的唯一真相？

### 方案取舍

6. SO 相比 JSON、CSV 或数据库适合什么规模？
7. 运行时修改共享 SO 会造成什么数据污染？
8. 字符串动画名有什么风险，项目怎样补偿？

### 扩展设计

9. 如果策划需要批量修改上百条招式，现有 SO Inspector 是否够用？
10. 如果做 Addressables 或热更新，配置引用方式要怎样调整？

### 故障排查

11. Inspector 数值正确但运行时不生效，应检查引用资产、烘焙副本还是覆盖优先级？
12. 两个 Boss 实例互相影响配置，最可能错误地写了哪类数据？

回答数据驱动时不要只说“方便调参”。还要讲数据所有权、共享资产风险、运行时副本和校验成本。

## 自测

1. `CharacterConfig`、`AttackConfig` 和 `BossMoveTable` 的边界是什么？
2. 为什么 Boss 不直接把整张招式表交给 `AttackState`？
3. 为什么运行时状态不能写入共享 SO？
4. 时间轴工具与构建校验分别解决什么问题？
