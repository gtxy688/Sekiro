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

// 新增（M3/M17）
OnPerilousAttack(PerilousType type)   // 危字提示

// 新增（表现）
OnFinisherTriggered(Vector3 pos)      // 忍杀
OnCameraShake(float intensity)        // 震屏
```

> 为什么事件带完整数据：M2（CharacterConfig）还没实现时表现层也能独立编译运行，不依赖读取 CharacterBody 内部字段。

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
BossPostureBarView : SetPosture(float ratio) / SetDanger(bool)  // 中心双向 + 快满高亮
PlayerStatusView   : SetReviveDots(int) / SetHP(float) / SetPosture(float)
ItemSlotView       : SetGourdIcon(Sprite) / SetGourdCount(int)
LockOnIndicatorView: SetLocked(bool) / SetFinisherReady(bool)  // 世界空间，挂 Boss
PerilousWarningView: ShowWarning(PerilousType)  // "危"字动画，播完自动隐藏

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
    void HandlePerilous(PerilousType t) { perilousWarningView?.ShowWarning(t); }
    // ...
}
```

### 特殊动画（DoTween，已实现）

- 架势条快满（>80%）：颜色变亮 + 边缘尖刺脉动（`BossPostureBarView.SetDanger`）
- 忍杀红点：放大 + 红色脉动（`LockOnIndicatorView.SetFinisherReady`）
- "危"字：放大淡入 + 红光泛晕，播完自动隐藏（`PerilousWarningView.ShowWarning`，序列代替原 timer/Update）
- 葫芦使用：数字闪烁（待接）

> 动画全部通过 `DOKill()` 清理残留 tween，防止事件连续触发时动画叠加。
> `PerilousType` 枚举在 `Assets/Scripts/Configs/PerilousType.cs`（跨系统共享，不放 PlayerAttacks）。

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
    public AudioClip gourdSfx;        // 喝葫芦

    void OnEnable()  { CombatEventBus.OnWeaponDeflected += HandleWeaponDeflected; /* 等 */ }
    void OnDisable() { /* 取消 */ }
    // 处理函数里判断 DeflectType：Perfect → 叮，Normal → 笃
}
```

> 实现细节：Awake 里自动补 AudioSource（`GetComponent` 失败则 `AddComponent`），`PlayOneShot` 播放不打断其他音效。

## 涉及文件

- 新建：`Assets/Scripts/Camera/CameraController.cs`（或场景里配 Cinemachine）
- 新建：`Assets/Scripts/UI/Views/*.cs`
- 新建：`Assets/Scripts/UI/CombatUIController.cs`
- 新建：`Assets/Scripts/Audio/AudioManager.cs`
- 修改：`Assets/Scripts/Mgr/CombatEventBus.cs`
