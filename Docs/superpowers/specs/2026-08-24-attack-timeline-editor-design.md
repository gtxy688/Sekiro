# 攻击时间轴编辑器设计规格

日期：2026-08-24  
状态：已批准（用户确认后进入实现计划）  
依据：`Docs/architecture/03-hit-detection.md`、`Docs/architecture/07-anim-events.md`、`Docs/architecture/06-presentation.md`、`Docs/architecture/01-states.md`  
关系：给玩家 `AttackConfig` 与 Boss `BossMoveWindow` 提供同一套「对着动画标判定 / 音效」的编辑器；运行时出招时钟改为跟这一招动画走。不改 HFSM 结构、不改 Hit 结算、不上 Unity Timeline、不把判定写回 Animation Event。

文档冲突（实现时改文档，不以旧 07 为准）：`07-anim-events.md` 前半仍写 Animation Event `EnableHitbox` / `DisableHitbox`，后半与代码已改为 `AttackState` 按配置时间开关。本规格锁定后者，并进一步把「进招秒表」换成「这一招动画已播放时间」。

## 1. 目标与红线

在 Unity 里打开一个窗口：上面看角色挥刀，下面拖红条（刀能打到人的区间）和音符（出声时刻）。进 Play 后，开刀和出声落在预览里看到的那一帧。

红线：

- 数据只写招式配置：玩家写工程里的 `AttackConfig`；Boss 写招式表的 `BossMoveWindow`，由现有 `BossAttackBaker` 拷到运行时临时 `AttackConfig`（`HideAndDontSave`，不存盘）。
- 判定结构统一为 `hitPulses[]`。玩家大多数招式只有 **1 段**（一刀），不是一招连砍；Boss 一条 Clip 可多段（如飞舟 Boat1）。
- 音效是独立列表 `sfxCues[]`，不跟判定窗绑死。一条动画可多个 clip、可早于出伤。
- 不写 Animation Event 开关 Hitbox；不上 Timeline / PlayableDirector 驱动战斗。
- 连招窗、转向窗仍在 Inspector 填数字，不进时间轴。
- 射箭生成帧仍用现有动画事件，不进本工具。
- 忍杀、弹刀、垫步、受击不是本工具的编辑对象。
- 表现层出声只走 `CombatEventBus` → `AudioManager`，`AttackState` 不直接 `PlayOneShot`。

成功标准：拖进度姿势跟上；红条对到刀挥平，进游戏也是那时才有判定；插了音符能在那一帧听到；玩家一条红、Boss 可多段；没重存的旧招式不会突然没判定。验收为 Unity 手测，无自动化测试。

## 2. 数据

### 2.1 新增类型

```csharp
[Serializable]
public class AttackSfxCue
{
    public float time;      // 秒，相对本招动画 0 点
    public AudioClip clip;
}
```

### 2.2 `AttackConfig`

- 保留现有窗口字段：`HitStartTime`、`RecoveryWindowStart`、`ComboWindowEnd`、`StateDuration`、`RotationWindowEnd`、`hitPulses`。
- 新增 `AttackSfxCue[] sfxCues`。
- 编辑器保存判定时：**始终写入 `hitPulses`**（玩家 1 段也写成长度为 1 的数组），并回填：
  - `HitStartTime` = 第一段 `start`
  - `RecoveryWindowStart` = 最后一段 `end`
- `ComboWindowEnd` 若小于回填后的 `RecoveryWindowStart`，编辑器把它抬到 `RecoveryWindowStart`（保持现有 `AttackState.ValidateWindows` 不等式成立）。不自动改 `StateDuration`，除非用户点「用动画长度填写」。

### 2.3 `BossMoveWindow`

- 同样有 `hitPulses`（已有）和新增 `sfxCues`。
- `BossAttackBaker.Bake` 把 `sfxCues` 拷进临时 `AttackConfig`（逐条复制 `time` 与 `clip` 引用）。
- 回填 `hitStartTime` / `recoverStart` 规则与玩家相同，烘焙时现有拷贝逻辑继续有效。

### 2.4 时间含义

所有本规格涉及的秒数（pulse、sfx、以及回填后的开/关、取消、连招、`StateDuration`）都是：**这一招动画从 0 开始已经播放了多久**，不是「按下攻击后的墙钟秒表」。

## 3. 运行时（`AttackState`）

每帧取本招动画时间 `t`（秒）：

1. Animator 第 0 层若处于 Transition，且 **下一状态名** 等于 `config.AnimName` → 用 `GetNextAnimatorStateInfo(0)`。
2. 否则若 **当前状态名** 等于 `config.AnimName` → 用 `GetCurrentAnimatorStateInfo(0)`。
3. `t = stateInfo.normalizedTime * stateInfo.length`。
4. 对不上本招状态时 `t = 0`（仍在融合进这一招之前）。
5. 攻击状态资源不应勾 Loop；若 `normalizedTime >= 1` 视为本招播完。

用 `t` 替换现有 `stateTimer` 来做：

- 判定：`hitPulses` 非空则按各段 `[start, end)` 开关刀（现有 `ApplyHitbox` / 清 `hitTargets` 行为不变）；为空则回退 `HitStartTime <= t < RecoveryWindowStart`（旧资产）。
- 取消：`t < HitStartTime` 或 `t >= RecoveryWindowStart` 可弹刀/垫步。
- 连招：`RecoveryWindowStart <= t <= ComboWindowEnd` 且存在 `NextCombo`。
- 转向：`AllowRotation && t <= RotationWindowEnd`。
- 结束：`t >= StateDuration` → 回 Idle。
- 音效：进入时清空已触发标记；当 `t` 首次 ≥ 某条 `sfxCues[i].time` 且 `clip != null` → 发总线事件一次。

新事件（实现时写入 `06-presentation.md`）：

```csharp
OnAttackSfx(AudioClip clip, Vector3 worldPos)
```

`AudioManager` 订阅后 `PlayOneShot(clip)`。位置带上是为以后 3D 声，第一版仍可 2D 播。

不改：`CrossFade(..., layer: 0)`、危字提示、`EnableWeaponHit` / `DisableWeaponHit`、退出时关刀。射箭动画事件保留。

## 4. 编辑器

### 4.1 窗口

独立 `EditorWindow`，菜单 `ARPG/攻击时间轴`。`AttackConfig` 与 `BossMoveTable` 的 Inspector 各一个按钮：打开窗口并选中当前对象。

布局：上为 3D 预览，下为时间轴。角色只画在窗口内（隔离预览），不实例化进打开的场景。

### 4.2 预览用谁

编辑器设置里各填一次玩家 Prefab、Boss Prefab 路径（第一次空则提示去填，不改场景）。

- 正在编 `AttackConfig` → 玩家 Prefab
- 正在编 Boss 招式某一段 → Boss Prefab

用该 Prefab 的 Animator，按招式 `AnimName` 找到对应 `AnimationClip` 来摆姿势、量总时长。找不到状态或没填 Prefab：预览区空，Console 一句中文说明，不抛未处理异常。

### 4.3 时间轴

- **判定轨**：可拖动的区间（红条）。玩家默认 1 段；提供「加段 / 删段」供 Boss 多刀。拖到 `start >= end` 时钳制为合法（`start < end`，两端落在 `[0, clipLength]`），不写入非法数据。
- **音效轨**：独立标记点，每点一个 `AudioClip` + 时间。可增删。
- 拖进度：预览角色停在该帧姿势。
- 不展示连招/转向轨。

Boss：必须先选表中哪一行招、哪一段窗口（与 `sequences` 里对应 state 对齐，例如 Boat1 / Boat2）。写入该行的 `windows[segment]`。

### 4.4 与「填入默认招式」

`BossMoveTableEditor` 的「填入弦一郎默认招式表」会整表覆盖，包括已对过的 `hitPulses` / `sfxCues`。规格不改这个按钮；验收说明写清：对完刀后不要按，除非要恢复目录默认值。

## 5. 错误与兼容

| 情况 | 行为 |
| --- | --- |
| 未填预览 Prefab | 窗口提示；不修改场景 |
| `AnimName` 在预览 Animator 上不存在 | 预览空；与现网一样可 `LogError` |
| pulse 非法 | 编辑器不写入非法数据；运行时现有 `ValidateWindows` 仍拦截 |
| `sfxCues` 某条 `clip` 为空 | 跳过该条，其它照播 |
| 旧 SO `hitPulses` 为空 | 运行时用 `HitStartTime` / `RecoveryWindowStart` 一对开关 |
| Clip 长度与 `StateDuration` 不一致 | 不自动改；可选手动「用动画长度填写」 |

## 6. 验收清单（手测）

1. 打开窗口，拖进度，刀停在对应姿势。
2. 把红条拖到刀刃挥平附近，进 Play 砍人：判定开始时刻与预览一致（目视可接受一帧误差）。
3. 插一个音符并指定 clip，进 Play 在该帧附近听到。
4. 玩家轻击：时间轴上一条红；保存后 `hitPulses.Length == 1`，且 `HitStartTime` / `RecoveryWindowStart` 已回填。
5. Boss 飞舟 Boat1：可加多段红，进 Play 同一段动画能多段出伤。
6. 未改过的旧 `AttackConfig`（空 `hitPulses`）进 Play 仍有判定。
7. 不按「填入默认招式」时，重开窗口仍看到上次拖的红条和音符。

## 7. 涉及文件（实现时）

- 改：`AttackConfig.cs`、`BossMoveWindow.cs`、`BossAttackBaker.cs`、`AttackState.cs`、`CombatEventBus.cs`、`AudioManager.cs`
- 增：`Assets/Editor/AttackTimelineWindow.cs`（及预览/设置所需的同目录 Editor 脚本）
- 改文档：`07-anim-events.md`（时钟与事件范围）、`03-hit-detection.md`（玩家也用 `hitPulses`，长度为 1）、`06-presentation.md`（`OnAttackSfx`）、`01-states.md`（窗口时钟改为动画时间）、对应 `*-test.md` 增本工具条目

不改：CombatManager 结算、Hitbox 扫描方式、行为树选招。
