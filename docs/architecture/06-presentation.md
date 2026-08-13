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

// 新增（M2 定义）
OnPostureChanged(CharacterBody)
OnPostureBroken(CharacterBody)
OnGourdUsed(CharacterBody)
OnDeath(CharacterBody)
OnReviveAvailable(CharacterBody)

// 新增（M3/M17）
OnPerilousAttack(PerilousType type)   // 危字提示

// 新增（表现）
OnFinisherTriggered(Vector3 pos)      // 忍杀
OnCameraShake(float intensity)        // 震屏
```

## 二、相机（M12，Cinemachine）

### 自由模式

- Cinemachine FreeLook 相机，跟随玩家，旋转跟随鼠标/摇杆。

### 锁定模式

- Cinemachine TargetGroup：玩家 + 锁定目标
- 相机围绕两者中点旋转，视线保持两者连线

### 锁定切换

- LockOnManager（M11）`IsLockedOn` 变化 → 切换 Cinemachine brain 的 virtual camera 优先级
  - 解锁：FreeLook priority 高
  - 锁定：TargetGroup camera priority 高

### 震屏

- 订阅 `OnCameraShake` → Cinemachine Impulse 触发
- 触发点：弹反成功、崩解、处决、受击

## 三、UI（M13，MVC）

### 布局（用户决策，参考截图）

```
正上方居中：Boss 架势条（中心双向增长，黄/橙，箭头端点）
底部居中：  玩家架势条（中心双向增长，样式同 Boss）
左上角：    忍杀提示灯（2 红点）+ Boss 血条 + 名称"苇名弦一郎"
左下角：    回生节点（1 粉花瓣）+ 玩家血条
右下角：    葫芦槽位（图标 + 数量）
世界空间：  锁定点（白点挂 Boss 身上）→ 崩解变大红点
屏幕中央：  "危"字（玩家头顶 World→Canvas 投影）
```

**不显示**：忍义手武器栏、纸人数量。物品区只有葫芦。

### View 层（纯 UI，每个一个类）

```csharp
// 只暴露视觉接口，不持有业务逻辑
BossStatusView     : SetLifeDots(int count) / SetHP(float ratio) / SetName(string)
BossPostureBarView : SetPosture(float ratio)  // 中心双向
PlayerStatusView   : SetReviveDots(int) / SetHP(float) / SetPosture(float)
ItemSlotView       : SetGourdIcon(Sprite) / SetGourdCount(int)
LockOnIndicatorView: SetLocked(bool) / SetFinisherReady(bool)  // 世界空间，挂 Boss
PerilousWarningView: ShowWarning(PerilousType)  // "危"字动画
```

### Controller 层

```csharp
public class CombatUIController : MonoBehaviour
{
    // 订阅 CombatEventBus 所有战斗事件
    // 事件 → 调对应 View 接口更新
    void OnEnable() { /* 订阅 */ }
    void OnDisable() { /* 取消订阅 */ }

    void HandlePostureBroken(CharacterBody c) { LockOnIndicatorView.SetFinisherReady(true); }
    void HandlePerilous(PerilousType t)       { PerilousWarningView.ShowWarning(t); }
    // ...
}
```

### 特殊动画（DoTween）

- 架势条快满（>80%）：颜色变亮 + 边缘尖刺
- 忍杀红点：脉动（scale + 透明度 loop）
- "危"字：放大淡入 + 红光泛晕
- 葫芦使用：数字闪烁

## 四、音效（M15）

跟 FXManager 同模式——订阅 CombatEventBus 播 AudioClip。

```csharp
public class AudioManager : MonoBehaviour
{
    public AudioClip deflectSfx;      // "叮"
    public AudioClip blockSfx;        // "笃"
    public AudioClip hitSfx;          // 受击
    public AudioClip perilousSfx;     // 危字
    public AudioClip finisherSfx;     // 处决
    public AudioClip deathSfx;        // 死亡

    void OnEnable()  { CombatEventBus.OnWeaponDeflected += PlayDeflect; /* 等 */ }
    void OnDisable() { /* 取消 */ }
}
```

## 涉及文件

- 新建：`Assets/Scripts/Camera/CameraController.cs`（或场景里配 Cinemachine）
- 新建：`Assets/Scripts/UI/Views/*.cs`
- 新建：`Assets/Scripts/UI/CombatUIController.cs`
- 新建：`Assets/Scripts/Audio/AudioManager.cs`
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
