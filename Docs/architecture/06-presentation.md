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
OnFinisherTriggered(Vector3 pos)      // 忍杀
OnCameraShake(float intensity)        // 震屏
OnLockOnChanged(bool isLocked)        // 锁定点 UI + 相机 VCam 切换（M11 触发）
```

> 保留：`OnPerilousAttack`（危字提示，M17 保留）。

> 为什么事件带完整数据：M2（CharacterConfig）还没实现时表现层也能独立编译运行，不依赖读取 CharacterBody 内部字段。

## 二、相机（M12，Cinemachine）

### 自由模式

- Cinemachine FreeLook。**Follow 和 Look At 都是 `CameraFollowTarget`**（独立空物体，位置硬贴玩家胸口，旋转 identity），**不要拖玩家根**：根运动步伐晃和角色 yaw 都会进镜头。
- 三个 Rig 的 Aim = **Hard Look At**（不用 Composer + DeadZone 跟步伐拉锯）。
- Body X/Y/Z Damping = **0**。Heading = Position Delta，**Velocity Filter = 0**（Cinemachine 2.10 的 Heading 没有 World 项）。
- Binding Mode = **World Space**。未锁定走位相对相机；朝向只跟鼠标。
- 鼠标环绕由 `CinemachineOrbitInput` 驱动（关掉 `CinemachineInputProvider`；FreeLook Axis Max Speed = 0）。

### 锁定模式（只狼第三人称跟随）

- 第二台 `CinemachineVirtualCamera`（`LockOn Camera`），**不用 FreeLook 继续独立环绕**。
- Follow = 同一个 `CameraFollowTarget`（锁定时脚本 `SetYawTarget(Boss)`，机位架在人-敌轴背后）。**不要 Follow 玩家根。**
- LookAt = Boss；Aim 看胸口高度；Body = Transposer，`LockToTargetWithWorldUp`，阻尼 0。
- 角色仍由 MoveState 面朝 Boss、围着目标 strafe。
- 不用 TargetGroup 中点构图：那会把两人居中，不像只狼「架在角色背后看向敌人」。

### 锁定切换

- `LockOnManager` 触发 `CombatEventBus.OnLockOnChanged` → `CameraController` 改两台 VCam 的 Priority（**不做每帧轮询**）。
  - 解锁：FreeLook priority 高（默认 10），锁定 VCam = 0；恢复鼠标环绕；跟随点取消 yaw
  - 锁定：锁定 VCam priority 高（默认 20），FreeLook = 0；关掉环绕输入
- CinemachineBrain：Update Method = Late Update；Default Blend = **EaseInOut、约 0.6s**（`CameraController.Blend Time`，太短像硬切、太长拖沓）。
- 两台 VCam 都开 **Inherit Position** + Blend Hint **Cylindrical Position**：从当前机位绕角色滑过去，不走直线穿地。
- 锁定 VCam 的 FOV 与 FreeLook 相同（避免过渡时突然变焦）；`Standby Update = Always`，混入前机位已就绪。
- 解锁同样走这套混合，不是瞬间切回。

### 不要用的防抖

- 不要靠调大 Composer DeadZone：人不抖但会离开中央。
- 不要给 `CameraFollowTarget` 做位置平滑：镜头慢半拍，走路发糊（老花眼）。
- 发糊先查 Rig X Damping 和 Heading 速度滤波，不是再加阻尼。

### 震屏

- 订阅 `OnCameraShake` → `CameraShake`（DoTween 偏移，不依赖 Impulse）
- 触发点：弹反成功、崩解、处决、受击

## 三、UI（M13，MVC）

### 布局（用户决策，参考截图）

```
正上方居中：Boss 架势条（中心双向增长，黄/橙，箭头端点）
底部居中：  玩家架势条（中心双向增长，样式同 Boss）
左上角：    忍杀提示灯（2 红点）+ Boss 血条 + 名称"苇名弦一郎"
左下角：    回生节点（1 粉花瓣）+ 玩家血条
右下角：    葫芦槽位（图标 + 数量）
世界空间：  锁定点钉在 Boss Spine1，Overlay 相机画在最前，不被模型挡住
屏幕中央：  "危"字（玩家头顶 World→Canvas 投影）
```

**不显示**：忍义手武器栏、纸人数量。物品区只有葫芦。

### View 层（纯 UI，每个一个类）

```csharp
// 只暴露视觉接口，不持有业务逻辑
BossStatusView     : SetLifeDots(int count) / SetHP(float ratio) / SetName(string)
BossPostureBarView : SetPosture(float ratio) / SetDanger(bool)  // 中心双向 + 快满高亮
PlayerStatusView   : SetReviveDots(int) / SetHP(float) / SetPosture(float)
ItemSlotView       : SetGourdIcon(Sprite) / SetGourdCount(int)
LockOnIndicatorView: SetLocked(bool) / SetFinisherReady(bool)  // 世界空间跟 Spine1，Overlay 相机不被挡
PerilousWarningView  : SetPerilous(PerilousType)        // "危"字（世界空间投影，M17 保留）

// 所有 View 继承 UIView 基类：Show()/Hide()/OnViewInit()
```

### Controller 层

```csharp
public class CombatUIController : MonoBehaviour
{
    [SerializeField] private CharacterBody playerBody;  // 区分事件属于玩家还是 Boss
    [SerializeField] private CharacterBody bossBody;
    [SerializeField] private BossStatusView bossStatusView;  // 以及其余 View 引用

    // 订阅 CombatEventBus 所有战斗事件 → 调对应 View 接口更新
    void OnEnable()  { /* 订阅 */ }
    void OnDisable() { /* 取消订阅 */ }

    // 按 CharacterBody 区分路由，不做每帧轮询
    void HandlePostureChanged(CharacterBody c, float posture, float maxPosture)
    {
        float ratio = posture / maxPosture;
        if (c == playerBody) playerStatusView?.SetPosture(ratio);
        else if (c == bossBody) { bossPostureBarView?.SetPosture(ratio); bossPostureBarView?.SetDanger(ratio > 0.8f); }
    }
    // ...
}
```

### 特殊动画（DoTween，已实现）

- 架势条快满（>80%）：颜色变亮 + 边缘尖刺脉动（`BossPostureBarView.SetDanger`）
- 忍杀图标：崩解时显示 `Finsher`（`LockOnIndicatorView.SetFinisherReady`）
- 葫芦使用：数字闪烁（待接）

> 动画全部通过 `DOKill()` 清理残留 tween，防止事件连续触发时动画叠加。
> PerilousWarningView（"危"字 UI）保留（M17）。

## 四、音效（M15）

跟 FXManager 同模式——订阅 CombatEventBus 播 AudioClip。

```csharp
public class AudioManager : MonoBehaviour
{
    public AudioClip deflectSfx;      // "叮"
    public AudioClip blockSfx;        // "笃"
    public AudioClip hitSfx;          // 受击
    public AudioClip finisherSfx;     // 处决
    public AudioClip deathSfx;        // 死亡
    public AudioClip gourdSfx;        // 喝葫芦

    void OnEnable()  { CombatEventBus.OnWeaponDeflected += HandleWeaponDeflected; /* 等 */ }
    void OnDisable() { /* 取消 */ }
    // 处理函数里判断 DeflectType：Perfect → 叮，Normal → 笃
}
```

> 实现细节：Awake 里自动补 AudioSource（`GetComponent` 失败则 `AddComponent`），`PlayOneShot` 播放不打断其他音效。

## 涉及文件

- 新建：`Assets/Scripts/Camera/CameraController.cs`（订阅 OnLockOnChanged 切 VCam）
- 新建：`Assets/Scripts/Camera/CameraFollowTarget.cs`、`CinemachineOrbitInput.cs`
- 编辑器：`Assets/Editor/LockOnCameraBuilder.cs`（菜单 Tools/战斗/生成锁定相机）
- 新建：`Assets/Scripts/UI/Views/*.cs`
- 新建：`Assets/Scripts/UI/CombatUIController.cs`
- 新建：`Assets/Scripts/Audio/AudioManager.cs`
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
