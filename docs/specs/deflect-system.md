# 弹刀系统技术规格

## 设计意图

弹刀是只狼战斗系统的核心，是攻防转换的枢纽。玩家通过精准的时机判定弹开敌人攻击，获得输出窗口。

## 核心机制

### 弹刀判定流程

```
玩家按下右键（鼠标）：

├── 轻点右键（按下后快速松开）：
│    ── 进入 DeflectState，持续约12帧@60fps（0.2秒）
│         ├─ 前0帧：立即进入判定
│         ├─ 第1-12帧：完美弹刀窗口 ← 核心
│         └─ 第13-36帧：普通格挡窗口
│
│    在完美弹刀窗口内受到攻击 → 【完美弹刀】
│    │    ├─ 播放弹刀特效（大型火花 + 屏幕震动）
│    │    ├─ 播放弹刀音效（金属"叮"声）
│    │    ├─ 帧冻结 0.03-0.05秒
│    │    ├─ 敌方架势大幅上升
│    │    ├─ 玩家恢复少量自身架势（约5-10%）
│    │    ├─ 连续弹刀加成计数 +1
│    │    └─ 抖刀惩罚计数归零
│    │
│    在普通格挡窗口内受到攻击 → 【普通格挡】
│    │    ├─ 减免约50-70%伤害
│    │    ├─ 玩家架势小幅上升
│    │    └─ 小幅击退
│    │
│    未受到攻击 → 正常退出DeflectState
│
├── 按住右键不放：
│    └── 持续格挡状态
│         ├─ 受击 → 普通格挡（同上方）
│         ├─ 连续受击 → 架势持续上升 → 最终崩溃
│         └─ 松手 → 退出格挡状态
│
└── 抖刀惩罚（连续快速按右键）：
     ├─ 判定：0.5秒内连续按右键且未成功弹刀
     ├─ 惩罚1：弹刀窗口逐次缩小
     │    第1次：12帧 → 第2次：~10帧 → ... → 最低1帧
     ├─ 惩罚2：弹刀对敌方造成的架势伤害递减
     │    第1次：100% → 后续逐渐降低 → 最低约20%
     ├─ 解除方式：
     │    ├─ 成功弹刀一次 → 惩罚重置
     │    └─ 停止按键超过0.5秒（30帧）→ 惩罚自动消除
     └─ 例外：弹开敌人多段连击时不受惩罚
```

### 连续弹刀加成

```
连续成功弹刀时，对敌方架势条的伤害递增：

  第1次弹刀 → 基础架势伤害 × 1.0
  第2次弹刀 → 基础架势伤害 × 1.2
  第3次弹刀 → 基础架势伤害 × 1.4
  第4次弹刀 → 基础架势伤害 × 1.5
  第5次+   → 封顶 × 1.5

解除：弹刀失败 / 超过1秒未弹刀 → 计数归零
```

### 弹刀窗口分档（S/L Deflect）

```
不同攻击的弹刀窗口不同：

  轻型攻击（普通挥砍）→ 弹刀窗口 12帧（标准）
  重型攻击（重劈/突刺）→ 弹刀窗口 6-7帧（更严格）
  
弹刀重型攻击成功后，对敌方造成的架势伤害更大（约1.5倍）
```

### 弹刀碰撞检测方式

```
不依赖 OnTriggerEnter（有物理刷新延迟）
→ 使用 Physics.OverlapSphere 绑定在敌人武器骨骼上
→ 每帧主动检测是否与主角的弹刀判定框交叉

实现：
  敌人武器末端挂一个小的Sphere Collider（trigger）
  主角弹刀激活时，每帧检测该Sphere是否在主角的弹刀范围内
  同时检测主角朝向与攻击方向的夹角 < 90°（正面弹刀）
```

## 数据结构

```csharp
// 依赖：DeflectConfig（见 data-layer.md）
// 依赖：PostureSystem（见 posture-system.md）
// 依赖：CombatEvents（事件广播）

public class DeflectSystem
{
    // 从 DeflectConfig 读取
    private DeflectConfig _config;
    
    // 运行时状态
    private int _spamCount = 0;
    private float _lastDeflectPressTime = 0f;
    private float _lastDeflectReleaseTime = 0f;
    private bool _isDeflecting = false;
    private float _deflectTimer = 0f;
    private int _deflectChainCount = 0;
    private float _lastDeflectSuccessTime = 0f;
}
```

## 接口定义

```csharp
/// <summary>
/// 弹刀系统，处理弹刀判定、抖刀惩罚、连续加成
/// </summary>
public class DeflectSystem
{
    public DeflectSystem(DeflectConfig config);
    
    /// <summary>
    /// 玩家按下右键时调用
    /// </summary>
    public void OnDeflectPressed();
    
    /// <summary>
    /// 玩家松开右键时调用
    /// </summary>
    public void OnDeflectReleased();
    
    /// <summary>
    /// 每帧调用，更新弹刀状态
    /// </summary>
    public void Update();
    
    /// <summary>
    /// 检测是否受到攻击，返回弹刀结果
    /// </summary>
    public DeflectResult TryDeflect(AttackData incomingAttack, Vector3 attackDirection, Vector3 playerForward);
    
    /// <summary>
    /// 成功弹刀后调用，重置惩罚计数
    /// </summary>
    public void OnSuccessfulDeflect();
    
    /// <summary>
    /// 获取当前弹刀窗口（考虑抖刀惩罚）
    /// </summary>
    public float GetCurrentDeflectWindow();
    
    /// <summary>
    /// 获取当前架势伤害倍率（考虑连续加成和抖刀惩罚）
    /// </summary>
    public float GetPostureDamageMultiplier();
    
    /// <summary>
    /// 是否处于弹刀状态
    /// </summary>
    public bool IsDeflecting { get; }
    
    /// <summary>
    /// 是否处于格挡状态（按住右键）
    /// </summary>
    public bool IsBlocking { get; }
}

public enum DeflectResult
{
    None,           // 未弹刀
    NormalBlock,    // 普通格挡
    PerfectDeflect  // 完美弹刀
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| baseDeflectWindow | 0.2 | 秒 | 12帧@60fps |
| minDeflectWindow | 0.016 | 秒 | 1帧@60fps |
| spamResetTime | 0.5 | 秒 | 停止按键多久后重置惩罚 |
| windowReductionPerSpam | 0.015 | 秒 | 每次抖刀减少的窗口 |
| deflectChainResetTime | 1.0 | 秒 | 多久未弹刀则加成归零 |
| deflectPostureRecovery | 0.08 | 比例 | 弹刀恢复自身架势8% |
| blockDamageReduction | 0.6 | 比例 | 格挡减伤60% |
| blockPostureIncrease | 0.3 | 比例 | 格挡时架势上升30% |

## 与其他系统的交互

### 输入

- `InputReader` → 右键按下/松开/长按事件
- `PostureSystem` → 敌方攻击数据（AttackData）
- `Boss` → 攻击方向和位置

### 输出

- `PostureSystem` → 增加敌方架势、恢复自身架势
- `DamageCalculator` → 传递伤害倍率
- `HitStopManager` → 触发帧冻结
- `CombatEvents` → 广播 `OnPerfectDeflect` / `OnNormalBlock`
- `VFXManager` → 播放弹刀特效
- `AudioManager` → 播放弹刀音效

## 测试要点

### EditMode 单元测试

- [ ] `GetCurrentDeflectWindow_BaseValue_Returns12Frames` — 初始弹刀窗口正确
- [ ] `GetCurrentDeflectWindow_After3Spams_WindowReduced` — 抖刀惩罚使窗口递减
- [ ] `GetCurrentDeflectWindow_AtMinSpam_Returns1Frame` — 窗口不低于最小值
- [ ] `OnSuccessfulDeflect_SpamPenaltyReset` — 成功弹刀重置惩罚
- [ ] `OnDeflectReleased_Wait05Seconds_SpamPenaltyAutoReset` — 超时自动重置
- [ ] `GetPostureDamageMultiplier_Chain2_Returns12` — 连续弹刀加成正确
- [ ] `GetPostureDamageMultiplier_Chain5Plus_Caps15` — 加成封顶
- [ ] `GetPostureDamageMultiplier_Spam3_Reduced` — 抖刀惩罚降低伤害
- [ ] `TryDeflect_InPerfectWindow_ReturnsPerfectDeflect` — 完美弹刀判定
- [ ] `TryDeflect_InBlockWindow_ReturnsNormalBlock` — 普通格挡判定
- [ ] `TryDeflect_OutsideWindow_ReturnsNone` — 未弹刀
- [ ] `TryDeflect_WrongAngle_ReturnsNone` — 角度不对不弹刀

## 验收标准

1. 轻点右键能进入弹刀状态，持续约 0.2 秒
2. 在弹刀窗口内受击触发完美弹刀，播放特效音效，帧冻结
3. 普通格挡减伤并减少架势上升
4. 连续快速按右键触发抖刀惩罚，弹刀窗口递减
5. 成功弹刀重置惩罚，停止按键 0.5 秒后自动重置
6. 连续弹刀时敌方架势伤害递增，封顶 1.5 倍
7. 弹刀成功恢复自身 8% 架势
