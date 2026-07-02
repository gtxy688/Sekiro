# 动画系统技术规格

## 设计意图

动画系统负责将战斗逻辑与 Unity Animator 桥接，确保攻击、弹刀、闪避等战斗动作在正确的时机播放，并通过 Root Motion 和 Animation Event 精确控制角色位移和伤害判定。动画系统不直接参与战斗逻辑计算，而是作为"视觉表达层"响应状态机的状态切换。

## 核心机制

### Root Motion 策略

```
不同动画类型对 Root Motion 的使用策略不同：

  不使用 Root Motion（代码控制位移）：
  ├─ 移动（走/跑）→ Rigidbody.velocity 驱动，更精确地响应输入
  ├─ 跳跃 → 抛物线运动，需要可控的起跳和落地
  └─ 受击后退 → 需要可控的击退方向和距离

  使用 Root Motion（动画驱动位移）：
  ├─ 闪避 → 保证位移与动画表现一致，真实感强
  ├─ 攻击 → 攻击位移跟随动画（前冲/旋转），手感自然
  ├─ 识破 → 前踏动作需要精确的位移匹配
  └─ 喝药 → 自然动作，无需代码干预

实现方式：
  Animator.applyRootMotion 在状态切换时动态设置：
  - 进入 Root Motion 状态 → applyRootMotion = true
  - 进入代码控制状态 → applyRootMotion = false
```

### Animation Event 清单

```
在攻击动画的关键帧插入以下事件：

  EnableHitbox()
  ├─ 插入位置：刀刃挥出的瞬间（Active 阶段起始帧）
  ├─ 作用：激活伤害判定区域
  └─ 实现：设置 hitboxCollider.enabled = true

  DisableHitbox()
  ├─ 插入位置：挥刀结束（Active 阶段结束帧）
  ├─ 作用：关闭伤害判定区域
  └─ 实现：设置 hitboxCollider.enabled = false

  EnableDeflectCancel()
  ├─ 插入位置：攻击前摇（Startup）阶段末尾
  ├─ 作用：标记此时可被弹刀指令打断
  └─ 实现：设置 canDeflectCancel = true

  OnAttackHit()
  ├─ 插入位置：判定帧（Active 阶段的命中确认帧）
  ├─ 作用：触发命中逻辑（伤害计算、架势变化、特效音效）
  └─ 实现：调用 DamageSystem.OnAttackConfirmed()

  OnFootStep()
  ├─ 插入位置：脚部落地帧
  ├─ 作用：播放脚步音效、触发踩头判定
  └─ 实现：调用 AudioManager.Play("footstep")

  OnAnimationEnd()
  ├─ 插入位置：动画最后一帧
  ├─ 作用：通知状态机当前动画播放完毕
  └─ 实现：触发状态转换（退出当前状态或进入下一个连招段）
```

### Blend Tree 设计

```
移动 Blend Tree（GroundedState）：
  ┌─────────────────────────────────┐
  │  参数：moveX（-1 到 1）         │
  │        moveZ（-1 到 1）         │
  │                                 │
  │  Idle ← Walk_Forward ← Run     │
  │       ← Walk_Backward           │
  │       ← Walk_Left / Walk_Right  │
  │                                 │
  │  混合模式：2D FreeformDirection │
  └─────────────────────────────────┘

锁定移动 Blend Tree（LockOnMoveSubState）：
  ┌─────────────────────────────────┐
  │  参数：moveDirection（0-360°）  │
  │        speed（0-1）             │
  │                                 │
  │  LockOn_Idle                    │
  │  ← LockOn_Move_Forward          │
  │  ← LockOn_Move_Backward         │
  │  ← LockOn_Move_Left             │
  │  ← LockOn_Move_Right            │
  │                                 │
  │  混合模式：2D Simple Directional│
  └─────────────────────────────────┘

参数更新时机：
  每帧在 PlayerController.Update 中：
  1. 读取 InputReader 的移动输入
  2. 转换为 moveX / moveZ 或 moveDirection / speed
  3. Animator.SetFloat() 更新参数（使用 dampTime 平滑过渡）
```

### 动画状态切换流程

```
状态机切换状态时：

  1. PlayerStateMachine.TransitionTo(newState)
  2. newState.OnEnter()
     ├─ 设置 Animator.applyRootMotion（根据策略表）
     ├─ Animator.CrossFade(动画名, transitionDuration)
     └─ 重置动画参数
  3. newState.Update()
     ├─ 更新 Blend Tree 参数（如果是移动状态）
     └─ 检查动画事件回调
  4. newState.OnExit()
     ├─ 恢复 Animator.applyRootMotion = false
     └─ 清理动画事件监听
```

## 数据结构

```csharp
// 依赖：PlayerStateMachine（状态切换）
// 依赖：CombatEvents（事件广播）
// 依赖：DamageSystem（伤害判定）

/// <summary>
/// Animation Event 配置，描述单个动画事件的关键参数
/// </summary>
[System.Serializable]
public class AnimationEventConfig
{
    [Header("事件标识")]
    public string eventName;
    
    [Header("时间参数")]
    public float normalizedTime;   // 归一化时间（0-1）
    public float frameOffset;      // 帧偏移（微调用）
    
    [Header("附加数据")]
    public int intParameter;       // 整型参数（如连招段数）
    public float floatParameter;   // 浮点参数（如伤害倍率）
    public string stringParameter; // 字符串参数（如特效名称）
}

/// <summary>
/// Root Motion 配置表
/// </summary>
[CreateAssetMenu(fileName = "RootMotionConfig", menuName = "Animation/RootMotionConfig")]
public class RootMotionConfig : ScriptableObject
{
    [Header("使用 Root Motion 的状态")]
    public string[] rootMotionStates = {
        "DodgeState",
        "AttackState",
        "MikiriState",
        "HealingState"
    };
    
    [Header("使用代码控制的状态")]
    public string[] codeMotionStates = {
        "MoveSubState",
        "LockOnMoveSubState",
        "JumpSubState",
        "FallSubState",
        "HitState"
    };
}

/// <summary>
/// Blend Tree 参数配置
/// </summary>
[CreateAssetMenu(fileName = "BlendTreeConfig", menuName = "Animation/BlendTreeConfig")]
public class BlendTreeConfig : ScriptableObject
{
    [Header("平滑过渡")]
    public float dampTime = 0.1f;
    
    [Header("速度阈值")]
    public float walkSpeedThreshold = 0.3f;
    public float runSpeedThreshold = 0.7f;
    
    [Header("锁定移动")]
    public float lockOnRotateSpeed = 10f;
}
```

## 接口定义

```csharp
/// <summary>
/// 动画事件处理器，接收 Animation Event 回调并转发给战斗系统
/// </summary>
public class AnimationEventHandler : MonoBehaviour
{
    /// <summary>
    /// 激活伤害判定区域（在攻击动画 Active 阶段起始帧调用）
    /// </summary>
    public void EnableHitbox();
    
    /// <summary>
    /// 关闭伤害判定区域（在攻击动画 Active 阶段结束帧调用）
    /// </summary>
    public void DisableHitbox();
    
    /// <summary>
    /// 标记可被弹刀指令打断（在攻击前摇末尾调用）
    /// </summary>
    public void EnableDeflectCancel();
    
    /// <summary>
    /// 攻击命中确认（在判定帧调用，触发伤害和架势计算）
    /// </summary>
    public void OnAttackHit();
    
    /// <summary>
    /// 脚步落地事件（播放音效、踩头判定）
    /// </summary>
    public void OnFootStep();
    
    /// <summary>
    /// 动画播放完毕（通知状态机进行状态转换）
    /// </summary>
    public void OnAnimationEnd();
    
    /// <summary>
    /// 伤害判定区域是否激活
    /// </summary>
    public bool IsHitboxActive { get; }
    
    /// <summary>
    /// 是否可被弹刀打断
    /// </summary>
    public bool CanDeflectCancel { get; }
}

/// <summary>
/// 动画控制器桥接，管理 Animator 参数和 Root Motion 切换
/// </summary>
public class AnimationController : MonoBehaviour
{
    /// <summary>
    /// 设置移动 Blend Tree 参数
    /// </summary>
    public void SetMoveParameters(float moveX, float moveZ);
    
    /// <summary>
    /// 设置锁定移动 Blend Tree 参数
    /// </summary>
    public void SetLockOnMoveParameters(float direction, float speed);
    
    /// <summary>
    /// 切换到指定动画（带过渡）
    /// </summary>
    public void PlayAnimation(string animName, float crossFadeDuration = 0.1f);
    
    /// <summary>
    /// 设置 Root Motion 开关
    /// </summary>
    public void SetRootMotion(bool enabled);
    
    /// <summary>
    /// 设置动画速度（用于攻速调整）
    /// </summary>
    public void SetAnimationSpeed(float speed);
    
    /// <summary>
    /// 重置所有动画参数到默认值
    /// </summary>
    public void ResetParameters();
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| crossFadeDuration | 0.1 | 秒 | 动画过渡时长 |
| blendTreeDampTime | 0.1 | 秒 | Blend Tree 参数平滑时间 |
| walkSpeedThreshold | 0.3 | 比例 | 低于此值播放 Idle，高于播放 Walk |
| runSpeedThreshold | 0.7 | 比例 | 高于此值播放 Run |
| lockOnRotateSpeed | 10 | °/秒 | 锁定时角色朝向旋转速度 |
| attackSpeedMultiplier | 1.0 | 倍 | 攻击动画播放速度（二阶段可提升） |
| rootMotion_move | false | — | 移动不使用 Root Motion |
| rootMotion_jump | false | — | 跳跃不使用 Root Motion |
| rootMotion_hit | false | — | 受击不使用 Root Motion |
| rootMotion_dodge | true | — | 闪避使用 Root Motion |
| rootMotion_attack | true | — | 攻击使用 Root Motion |
| rootMotion_mikiri | true | — | 识破使用 Root Motion |
| rootMotion_heal | true | — | 喝药使用 Root Motion |

## 与其他系统的交互

### 输入

- `PlayerStateMachine` → 状态切换时触发动画播放
- `InputReader` → 移动输入驱动 Blend Tree 参数
- `AttackData` → 攻击招式决定播放哪个动画
- `RootMotionConfig` → 读取各状态的 Root Motion 配置
- `BlendTreeConfig` → 读取 Blend Tree 平滑参数

### 输出

- `DamageSystem` → OnAttackHit 事件触发伤害计算
- `CombatEvents` → 广播动画相关事件
- `AudioManager` → OnFootStep 触发脚步音效
- `VFXManager` → EnableHitbox 时激活判定特效
- `PlayerStateMachine` → OnAnimationEnd 触发状态转换
- `DeflectSystem` → EnableDeflectCancel 标记可打断点

## 测试要点

### EditMode 单元测试

- [ ] `SetRootMotion_AttackState_Enabled` — 攻击状态启用 Root Motion
- [ ] `SetRootMotion_MoveState_Disabled` — 移动状态关闭 Root Motion
- [ ] `SetMoveParameters_WalkSpeed_BlendsCorrectly` — 移动参数正确混合
- [ ] `EnableHitbox_SetsActive` — 激活判定区域
- [ ] `DisableHitbox_SetsInactive` — 关闭判定区域
- [ ] `OnAttackHit_TriggersDamageSystem` — 命中事件触发伤害系统
- [ ] `OnAnimationEnd_NotifiesStateMachine` — 动画结束通知状态机
- [ ] `EnableDeflectCancel_SetsCanCancel` — 标记可打断点
- [ ] `PlayAnimation_CrossFade_SmoothTransition` — 动画过渡平滑
- [ ] `SetAnimationSpeed_DoubleSpeed_PlaysFaster` — 动画速度加倍正确

## 验收标准

1. 移动动画通过 Blend Tree 平滑混合，无跳帧
2. 攻击动画使用 Root Motion 驱动位移，手感自然
3. 闪避和识破使用 Root Motion，位移与动画表现一致
4. 受击后退和跳跃使用代码控制，击退方向可控
5. EnableHitbox/DisableHitbox 精确控制伤害判定窗口的开关
6. OnAttackHit 在正确帧触发，伤害计算和特效音效同步
7. 动画过渡使用 CrossFade，无硬切现象
8. 锁定移动 Blend Tree 正确响应方向输入
