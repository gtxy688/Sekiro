# 07 动画事件集成（M8）

> 模块：M8
> 前置：M3（Hitbox/CombatManager）
> 验收：`07-anim-events-test.md`

## 一、作用

把攻击动画的特定帧和逻辑代码接通。纯 Unity Editor 操作 + 少量事件接收代码。

```
攻击动画播放
  ├─ AttackState 读本招动画时间 t → 开/关 Hitbox、播 sfxCues
  └─ 一次性动画事件只保留：射箭生成帧
```

## 二、动画事件接收（代码侧）

Hitbox 已提供 Enable/Disable（M3）。动画事件直接调这些公开方法即可，无需额外接收层。

需要新增的接收方法：

```csharp
// CharacterBody：动画事件必须落在 Animator 所在对象
public void ExecuteFinisher()
{
    CombatManager.Instance?.ExecuteFinisher(this);
}

// 葫芦（M16）
public void OnDrinkGourdAnimEvent() { /* 补血动作完成时回调 */ }
```

## 三、Unity Editor 配置步骤（每段攻击动画）

1. 菜单 **ARPG → 攻击时间轴**。
2. 第一次把玩家 Prefab、Boss Prefab 拖进窗口上方。
3. 拖入要编的 `AttackConfig` 或 Boss 招式表，拖进度看刀，拖红条标判定，插 ♪ 标音效，点保存。

## 四、需要配置的动画清单

| 动画 | 事件 |
|------|------|
| 玩家 5 段连招 Attack1-5 | 代码驱动（AttackState 按动画时间开/关判定） |
| 玩家连段每段 | 同上 |
| Boss 近战连段 | 同上（招式表 `hitPulses` / `sfxCues`） |
| Boss 突刺 (Thrust) | 同上 |
| Boss 横扫 (Sweep) | 同上 |
| Boss 射箭 | 动画事件只保留出箭帧 |
| 玩家 `Finsher_Ground` / `Finsher_Deflect` / `Finsher_Mikiri` | 无事件；动画结束由 `FinisherState` 清命 |
| Boss 三组成对忍杀 | 无清命事件，只同步播放 |
| 喝葫芦 | 无事件；进入 HealState 时立即消耗并回血 |
| 玩家受击 | （无事件，纯动画） |

## 五、注意事项

- **攻击 Hitbox 与出招音效由 `AttackState` 开关**：时钟是本招动画已播放时间 `t`（`AttackAnimClock`）。`HitStartTime` / `hitPulses` 开判定，`RecoveryWindowStart` 关判定；`sfxCues` 到点发 `OnAttackSfx`。对着动画标时间用菜单 **ARPG/攻击时间轴**，数据写在 `AttackConfig` / Boss 招式表，不写 Animation Event。`CrossFade` 必须带 `layer: 0`。动画事件只保留一次性回调：射箭生成帧。
- 忍杀清命由 `FinisherState` 在动画结束时代码驱动。Clip 上即使残留 `ExecuteFinisher` 也不会重复扣命（`CombatManager` 幂等）。
- 喝药采用 Base Layer 慢走 + UpperBody `Drink_UpperBody`，不依赖动画事件结算回血。
- 动画事件调用的方法必须在挂 Hitbox 的 GameObject 或引用到的对象上。
- 若 Hitbox 不在 Animator 所在对象上，事件里 `GetComponent` 拿不到 → 让 Hitbox 直接持有 Animator 或由 CharacterBody 转发。
- 事件名要和方法名完全一致（区分大小写）。
- 每个 Clip 都要重新配，配一次只对当前 Clip 生效。

## 涉及文件

- 修改：`Assets/Scripts/Combat/CombatManager.cs`（ExecuteFinisher）
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（葫芦动画回调，可选）
- **大量 Unity Editor 操作**（配动画事件）
