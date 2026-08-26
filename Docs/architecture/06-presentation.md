# 06 表现层（M12 相机 + M13 UI + M15 音效）

> 模块：M12, M13, M15
> 前置：依赖战斗层事件（CombatEventBus）
> 验收：`06-presentation-test.md`

## 一、事件总线扩展（CombatEventBus）

表现层全部靠订阅事件驱动，**不做每帧轮询**。

```csharp
// 已有
OnWeaponDeflected(Vector3 hitPoint, DeflectType type)   // 打铁
OnTakeDamage(CharacterBody victim, int dmg, int hp)      // 受伤

// 新增（M2 定义）——事件携带完整数据，表现层不读 CharacterBody 内部字段
OnHPChanged(CharacterBody c, int hp, int maxHp)         // 血条（含最大值算比例）
OnPostureChanged(CharacterBody c, float posture, float maxPosture)  // 架势
OnPostureBroken(CharacterBody c)
OnGourdUsed(CharacterBody c, int remaining)
OnDeath(CharacterBody c)
OnReviveAvailable(CharacterBody c)

// 新增（表现）
OnFinisherTriggered(Vector3 pos)      // 忍杀音效/特效
OnFinisherStarted(Vector3 pos, CharacterBody player, CharacterBody victim) // 忍杀运镜切入
OnFinisherEnded(CharacterBody player, CharacterBody victim)   // 忍杀运镜退回
OnCameraShake(float intensity)        // 震屏
OnLockOnChanged(bool isLocked)        // 锁定点 UI + 相机 VCam 切换（M11 触发）
OnAttackSfx(AudioClip clip, Vector3 worldPos)  // 出招音效（AttackState 按 sfxCues 触发）
```

> 保留：`OnPerilousAttack`（危字提示，M17 保留）。

### 危字 Billboard（M17 表现）

- 事件：`OnPerilousAttack(PerilousType)`（签名不改）。
- 位置：世界空间 Billboard，跟随 **Boss** 的 `Head`（没有则 `Spine1` / `Spine`）+ `headOffset`（默认 0.55m）。**不跟玩家、不放屏幕正中。**
- 绘制：`ARPG/FX/PerilousKanji` 加法 Shader，白字黑底当遮罩；两层 Quad（光晕 0.78m / 字形 0.60m）；`ZTest Always`。笔画粗细调材质 `Stroke Thickness`（越大越粗）；`Dark Crush` 越大笔画越细。
- 时间：弹出后约 0.8 秒淡出。`LateUpdate` 只做跟随/朝向相机，不轮询战斗数值。
- 生成：`Tools/战斗/生成危字特效`。贴图放 `Assets/Art/FX`。

> 为什么事件带完整数据：M2（CharacterConfig）还没实现时表现层也能独立编译运行，不依赖读取 CharacterBody 内部字段。

## 二、相机（M12，Cinemachine 三级相机管理）

相机系统统一由 `CameraController` 调度，基于 `CombatEventBus` 事件驱动，不每帧轮询状态。
**优先级层级**：`FreeLook (10)` < `LockOn Camera (20)` < `Finisher Camera (30)`。

### 1. 自由模式（FreeLook）

- Cinemachine FreeLook。**Follow 和 Look At 都是 `CameraFollowTarget`**（独立空物体，位置硬贴玩家胸口，旋转 identity），**不要拖玩家根**：根运动步伐晃和角色 yaw 都会进镜头。
- 三个 Rig 的 Aim = **Hard Look At**（不用 Composer + DeadZone 跟步伐拉锯）。
- Body X/Y/Z Damping = **0**。Heading = Position Delta，**Velocity Filter = 0**（Cinemachine 2.10 的 Heading 没有 World 项）。
- Binding Mode = **World Space**。未锁定走位相对相机；朝向跟鼠标或手柄右摇杆。
- 环绕由 `CinemachineOrbitInput` 驱动（关掉 `CinemachineInputProvider`；FreeLook Axis Max Speed = 0）。键鼠用鼠标位移，手柄用右摇杆。锁定时关掉该组件，避免和锁定相抢轴。

### 2. 锁定模式（LockOn Camera）

- 第二台 `CinemachineVirtualCamera`（`LockOn Camera`），**不用 FreeLook 继续独立环绕**。
- Follow = 同一个 `CameraFollowTarget`（锁定时脚本 `SetYawTarget(Boss)`，机位架在人-敌轴背后）。**不要 Follow 玩家根。**
- LookAt = Boss；Aim 看胸口高度；Body = Transposer，`LockToTargetWithWorldUp`，阻尼 0。
- 锁定 VCam 挂 `CinemachineCollider`：撞到 Default/Ground 就把镜头往前收，避免穿墙。不要把 Hurtbox 勾进 Obstacle Layers（会吸进人里）。没 MeshCollider 的墙避不了，那种只能改碰撞或换地图。
- 角色仍由 MoveState 面朝 Boss、围着目标 strafe。
- 不用 TargetGroup 中点构图：那会把两人居中，不像只狼「架在角色背后看向敌人」。

### 3. 处决特写模式（Finisher Camera，只狼原版刀刃侧低机位特写）

- 第三台 `CinemachineVirtualCamera`（`Finisher Camera`），优先级最高（30）。
- **触发与退出**：订阅 `OnFinisherStarted(pos, player, victim, kind)` 切入，`OnFinisherEnded` 退出平滑降回锁定或自由相机。
- **差异化机位配置（针对三类忍杀）**：
  - **普通地面直刺 (`Ground`)**：`FollowOffset = (-0.48f, -0.05f, -2.65f)`，偏向狼左后方（刀刃侧），低机位微仰视，清晰收录直刺贯穿与受害者受挫姿势，配合 `0.4m` Dolly In 推进。
  - **弹反借力断喉 (`Deflect`)**：`FollowOffset = (-0.68f, -0.1f, -2.85f)`，更宽的左侧越肩视角与更低机位，完美框定左侧狼蓄力与右侧 Boss 核心红点位置，配合 `0.35m` Dolly In 推进。
  - **识破踩刀贯穿 (`Mikiri`)**：`FollowOffset = (-0.42f, -0.15f, -2.7f)`，极低机位强烈仰角，聚焦狼踩刀与向下狠刺的动作线。
- **长焦与微推（Dolly In）**：FOV 设为 `46°~48°`，处决期间沿刺刀攻击线向前缓推，刺入瞬间配合顿帧（HitStop）与强震屏（CameraShake）。
- **贴墙避障**：通过射线检测自动收短 Z 轴距离，防止卡入墙体。

### 4. 模式平滑切换规则

- `LockOnManager` 触发 `CombatEventBus.OnLockOnChanged` → `CameraController` 改两台 VCam 的 Priority（**不做每帧轮询**）。
  - 解锁：先按当前机位把 FreeLook 钉在角色背后，再切 Priority。跟随点 yaw **等到混合结束**才清掉——混合期间锁定相机仍 live，提前清 yaw 会把混合起点甩到角色侧方。
  - 锁定：锁定 VCam priority 高（默认 20），FreeLook = 0；关掉环绕输入；跟随点 `SetYawTarget(Boss)`
- CinemachineBrain：Update Method = Late Update；Default Blend = **EaseInOut、约 0.6s**（`CameraController.Blend Time`，太短像硬切、太长拖沓）。
- 三台 VCam 都开 **Inherit Position** + Blend Hint **Cylindrical Position**：从当前机位绕角色滑过去，不走直线穿地。
- 锁定 VCam 的 FOV 与 FreeLook 相同（避免过渡时突然变焦）；`Standby Update = Always`，混入前机位已就绪。
- 解锁时 FreeLook 钉在角色背后（X = 角色 yaw），不混回锁定前的环绕角，也不会甩到角色侧方。

### 不要用的防抖

- 不要靠调大 Composer DeadZone：人不抖但会离开中央。
- 不要给 `CameraFollowTarget` 做位置平滑：镜头慢半拍，走路发糊（老花眼）。
- 发糊先查 Rig X Damping 和 Heading 速度滤波，不是再加阻尼。

### 震屏

- 订阅 `OnCameraShake` → `CameraShake`（DoTween 偏移，不依赖 Impulse）
- 触发点：弹反成功、崩解、处决、受击

### 用户音量（暂停菜单）

- `AudioVolumeSettings`：音乐默认 0.8，音效默认 1.0，`PlayerPrefs` 键 `audio.bgm` / `audio.sfx`。
- 音效：实际音量 = 检视器基础 `volume` × 用户音效滑条。`PlayOneShot` 自动跟着变。wav 很大时调 `AudioManager.volume`（默认 0.35），不要改滑条默认 1。
- 音乐：`BGMManager` 实际音量 = 检视器基础 `volume` × 用户音乐音量 × 淡入淡出 `fadeWeight`。不要用 Mixer。曲子本身很大时调检视器 `volume`（当前场景约 0.14），不要靠暂停滑条贴 1%。

### 回生 / 胜利 / 真死提示

- 挂在 `CombatCanvas` 的 View 上，事件驱动，不每帧轮询。
- 外观跟暂停设置页同一套：全屏压暗 + 居中暗金面板 + 金字标题 + 浅字提示 + 细金线。不要粉红大字、不要太空紫。
- `CombatPromptStyle` 在 `OnViewInit` 补齐压暗和面板，旧 HUD 预制体不用手改。

## 三、音效（M15）

- 订阅 `OnWeaponDeflected`：`DeflectType.Normal` 从 `Resources/Sounds/Block` 随机一条；`Perfect` 从 `Resources/Sounds/Deflect` 随机一条。
- 订阅 `OnTakeDamage`：玩家走 `playerHitSfx`，Boss 走 `bossHitSfx`（各一条，Inspector 拖，不装池）。
- `AudioManager.Awake` 用 `Resources.LoadAll<AudioClip>` 各装一次格挡/弹反池；事件里不重载。
- 同一池连打不连抽同一条（池长 ≥ 2）。第一次全池均匀随机。池空则本发不播并 `LogWarning`。
- `PlayOneShot(clip)` 不传第二参数。响度 = 检视器 `volume` × 音效滑条。
- 处决 / 出招等其它 clip 仍 Inspector 拖。
