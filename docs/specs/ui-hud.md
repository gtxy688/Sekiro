# UI/HUD 技术规格

## 设计意图

UI/HUD 系统负责在战斗中实时显示所有关键信息：双方生命值和架势值、药葫芦剩余次数、危字提示、忍杀提示等。UI 完全通过事件系统驱动，不直接查询战斗模块的状态，确保 UI 层与战斗逻辑层解耦。

## 核心机制

### HUD 布局

```
┌────────────────────────────────────────────────┐
│                                                │
│  ┌──────────┐                                  │
│  │ 玩家头像  │  ← 左上角                        │
│  │ ████████ │  ← 血条（红）                     │
│  │ ░░░░░░░░ │  ← 架势条（白/灰）                │
│  └──────────┘                                  │
│                                                │
│              ┌────────────────                 │
│              │  弦一郎 血条    │  ← 屏幕上方居中  │
│              │  ████████████  │  （一管血时显示） │
│              │  ▓▓▓▓▓░░░░░░  │  ← 架势条（黄）  │
│              └────────────────┘                 │
│                                                │
│                                                │
│                                       ┌──┐     │
│                                       │E │     │  ← 右下角
│                                       │10│     │    药葫芦计数
│                                       └──┘     │
│                                                │
│              [红色忍杀提示]  ← 架势归零时闪烁     │
│              [红色危字提示]  ← 特殊攻击前显示     │
└────────────────────────────────────────────────┘
```

### UI 元素清单

```
┌─────────────┬────────────┬────────────────────────────────────┐
│ UI 元素      │ 位置        │ 说明                              │
├─────────────┼────────────┼────────────────────────────────────┤
│ 玩家血条     │ 左上角      │ 红色，带数字显示                    │
│ 玩家架势条   │ 血条下方    │ 白色/灰色，满时变红闪烁             │
│ Boss 血条    │ 上方居中    │ 红色，显示"苇名弦一郎"              │
│ Boss 架势条  │ Boss血条下  │ 黄色                               │
│ 药葫芦计数器 │ 右下角      │ E 键图标 + 剩余次数数字             │
│ 锁定标记     │ 敌人脚下    │ 三角/圆圈 World Space 指示器        │
│ 危字提示     │ 屏幕中央    │ 红色危字 + 方向符号（↓突刺/→扫击）  │
│ 忍杀提示     │ 敌人头顶    │ 闪烁的红色"忍"字                    │
│ 伤害数字     │ 命中位置    │ 白=普通 黄=弹刀 蓝=识破             │
│ 阶段转换文字 │ 屏幕中央    │ "弦一郎·雷"（黑屏转场时显示）       │
│ 结算画面     │ 全屏        │ 战斗统计数据（弹刀次数、用时等）     │
└─────────────┴────────────┴────────────────────────────────────┘
```

### 事件监听关系

```
UI 组件通过监听 CombatEvents 更新显示，不直接查询战斗模块：

  CombatEvents.OnPlayerDamaged(float damage)
    → PlayerHealthBar：更新血条宽度 + 播放受击闪白
    → DamageNumberPopup：在玩家位置弹出白色伤害数字

  CombatEvents.OnBossDamaged(float damage)
    → BossHealthBar：更新血条宽度
    → DamageNumberPopup：在 Boss 位置弹出白色伤害数字

  CombatEvents.OnPlayerPostureChanged(float current, float max)
    → PlayerPostureBar：更新架势条宽度 + 接近满时变红闪烁

  CombatEvents.OnBossPostureChanged(float current, float max)
    → BossPostureBar：更新架势条宽度

  CombatEvents.OnPerfectDeflect()
    → DamageNumberPopup：在弹刀位置弹出黄色"弹"字
    → ScreenFlash：短暂白色闪光

  CombatEvents.OnNormalBlock()
    → DamageNumberPopup：弹出灰色"挡"字

  CombatEvents.OnMikiriCounter()
    → DamageNumberPopup：在踩刀位置弹出蓝色"识破"
    → ScreenShake：触发屏幕震动

  CombatEvents.OnBossPostureBreak()
    → DeathblowPrompt：在 Boss 头顶显示闪烁的红色"忍"字

  CombatEvents.OnPlayerPostureBreak()
    → StunIndicator：屏幕边缘红色脉冲

  CombatEvents.OnDeathblow()
    → DeathblowPrompt：隐藏忍杀提示
    → ScreenFlash：全屏闪白

  CombatEvents.OnPhaseTransition()
    → PhaseTransitionUI：黑屏 + "弦一郎·雷"文字

  CombatEvents.OnLightningCounterSuccess()
    → ScreenFlash：全屏闪电特效
    → DamageNumberPopup：弹出大型蓝色"雷反"

  CombatEvents.OnLightningCounterFail()
    → StunIndicator：屏幕边缘紫色脉冲

  CombatEvents.OnHealingChargeChanged(int charges)
    → HealingCounter：更新右下角数字显示

  DangerSystem.OnDangerWarning(AttackType type)
    → DangerPrompt：显示红色危字 + 对应方向符号
```

### 伤害数字弹出系统

```
伤害数字在命中位置生成，向上飘动后淡出：

  生成参数：
  ├─ 位置：命中点世界坐标 + 随机偏移（避免重叠）
  ├─ 颜色：白色（普通）/ 黄色（弹刀）/ 蓝色（识破）
  ├─ 字号：普通伤害 24pt / 弹刀 28pt / 识破 32pt
  ├─ 飘动方向：向上 + 轻微随机水平偏移
  ├─ 飘动速度：2 单位/秒
  ├─ 存在时间：1.0 秒
  └─ 淡出：最后 0.3 秒 alpha 从 1 → 0

实现：
  使用 World Space Canvas + TextMeshPro
  对象池管理，避免频繁 Instantiate/Destroy
```

### 血条和架势条更新

```
血条更新策略：
  1. 受伤时血条立即缩减（红色部分）
  2. 延迟 0.3 秒后，白色"残影"条跟随缩减（视觉反馈）
  3. 回复时血条立即增长（绿色闪烁 → 红色）

架势条更新策略：
  1. 架势增加时条立即增长
  2. 架势恢复时条缓慢缩减（动画过渡）
  3. 架势接近满（>80%）时变红闪烁预警
  4. 架势崩溃时播放破碎动画
```

## 数据结构

```csharp
// 依赖：CombatEvents（事件监听）
// 依赖：DamageSystem（伤害数据来源）

/// <summary>
/// UI 全局配置 ScriptableObject
/// </summary>
[CreateAssetMenu(fileName = "UIConfig", menuName = "UI/UIConfig")]
public class UIConfig : ScriptableObject
{
    [Header("血条")]
    public float healthBarLerpSpeed = 10f;          // 血条平滑速度
    public float healthBarDelayTime = 0.3f;          // 残影条延迟时间
    public Color healthBarColor = new Color(0.8f, 0.1f, 0.1f);
    public Color healthBarDamageColor = Color.white;
    
    [Header("架势条")]
    public float postureBarLerpSpeed = 8f;           // 架势条平滑速度
    public Color postureBarNormalColor = new Color(0.8f, 0.8f, 0.8f);
    public Color postureBarWarningColor = new Color(1f, 0.2f, 0.2f);
    public float postureWarningThreshold = 0.8f;     // 80% 以上变红
    public float postureFlashSpeed = 5f;             // 闪烁速度
    
    [Header("伤害数字")]
    public float damageNumberFloatSpeed = 2f;
    public float damageNumberLifetime = 1.0f;
    public float damageNumberFadeTime = 0.3f;
    public Color damageNormalColor = Color.white;
    public Color damageDeflectColor = new Color(1f, 0.9f, 0.2f);
    public Color damageMikiriColor = new Color(0.3f, 0.6f, 1f);
    public int damageNormalFontSize = 24;
    public int damageDeflectFontSize = 28;
    public int damageMikiriFontSize = 32;
    
    [Header("危字提示")]
    public Color dangerColor = new Color(1f, 0.1f, 0.1f);
    public float dangerDisplayDuration = 1.5f;
    public float dangerFadeInTime = 0.1f;
    
    [Header("忍杀提示")]
    public float deathblowBlinkSpeed = 3f;
    public Color deathblowColor = new Color(1f, 0.2f, 0.2f);
    
    [Header("阶段转换")]
    public float phaseTransitionFadeDuration = 0.5f;
    public float phaseTransitionTextDuration = 1.0f;
}

/// <summary>
/// 伤害数字数据
/// </summary>
public struct DamageNumberData
{
    public float value;
    public Vector3 worldPosition;
    public DamageNumberType type;
}

/// <summary>
/// 伤害数字类型
/// </summary>
public enum DamageNumberType
{
    Normal,    // 普通伤害（白色）
    Deflect,   // 弹刀（黄色）
    Mikiri,    // 识破（蓝色）
    Lightning  // 雷电反击（蓝色大型）
}
```

## 接口定义

```csharp
/// <summary>
/// 玩家血条 UI 组件
/// </summary>
public class PlayerHealthBar : MonoBehaviour
{
    /// <summary>
    /// 更新血条显示值（0-1 比例）
    /// </summary>
    public void UpdateHealth(float healthPercent);
    
    /// <summary>
    /// 每帧调用，处理血条平滑过渡和残影条
    /// </summary>
    public void Update();
}

/// <summary>
/// 架势条 UI 组件（玩家和 Boss 共用基类）
/// </summary>
public class PostureBar : MonoBehaviour
{
    /// <summary>
    /// 更新架势条显示值（当前值，最大值）
    /// </summary>
    public void UpdatePosture(float current, float max);
    
    /// <summary>
    /// 触发架势崩溃动画
    /// </summary>
    public void PlayBreakAnimation();
    
    /// <summary>
    /// 每帧调用，处理平滑过渡和闪烁效果
    /// </summary>
    public void Update();
}

/// <summary>
/// 伤害数字弹出管理器
/// </summary>
public class DamageNumberManager : MonoBehaviour
{
    /// <summary>
    /// 在指定位置弹出伤害数字
    /// </summary>
    public void Spawn(DamageNumberData data);
    
    /// <summary>
    /// 对象池回收
    /// </summary>
    public void Recycle(DamageNumberPopup popup);
}

/// <summary>
/// 危字提示 UI 组件
/// </summary>
public class DangerPrompt : MonoBehaviour
{
    /// <summary>
    /// 显示危字提示（突刺/扫击/擒拿）
    /// </summary>
    public void ShowDanger(AttackType attackType);
    
    /// <summary>
    /// 隐藏危字提示
    /// </summary>
    public void HideDanger();
}

/// <summary>
/// 忍杀提示 UI 组件
/// </summary>
public class DeathblowPrompt : MonoBehaviour
{
    /// <summary>
    /// 显示闪烁的忍杀提示
    /// </summary>
    public void ShowDeathblow();
    
    /// <summary>
    /// 隐藏忍杀提示
    /// </summary>
    public void HideDeathblow();
}

/// <summary>
/// 阶段转换 UI（黑屏 + 文字）
/// </summary>
public class PhaseTransitionUI : MonoBehaviour
{
    /// <summary>
    /// 播放阶段转换动画（渐黑 → 显示文字 → 渐亮）
    /// </summary>
    public void PlayTransition(string phaseText);
}

/// <summary>
/// 结算画面 UI
/// </summary>
public class ResultScreen : MonoBehaviour
{
    /// <summary>
    /// 显示战斗结算画面
    /// </summary>
    public void ShowResult(BattleResultData result);
}
```

## 数值参数表

| 参数 | 值 | 单位 | 说明 |
|------|-----|------|------|
| healthBarLerpSpeed | 10 | — | 血条平滑插值速度 |
| healthBarDelayTime | 0.3 | 秒 | 残影条延迟跟随时间 |
| postureBarLerpSpeed | 8 | — | 架势条平滑插值速度 |
| postureWarningThreshold | 80 | % | 架势超过此比例变红闪烁 |
| postureFlashSpeed | 5 | — | 架势条闪烁频率 |
| damageNumberFloatSpeed | 2 | 单位/秒 | 伤害数字向上飘动速度 |
| damageNumberLifetime | 1.0 | 秒 | 伤害数字存在总时长 |
| damageNumberFadeTime | 0.3 | 秒 | 伤害数字淡出时长 |
| damageNormalFontSize | 24 | pt | 普通伤害字号 |
| damageDeflectFontSize | 28 | pt | 弹刀字号 |
| damageMikiriFontSize | 32 | pt | 识破字号 |
| dangerDisplayDuration | 1.5 | 秒 | 危字提示显示时长 |
| dangerFadeInTime | 0.1 | 秒 | 危字淡入时长 |
| deathblowBlinkSpeed | 3 | — | 忍杀提示闪烁速度 |
| phaseTransitionFadeDuration | 0.5 | 秒 | 阶段转换黑屏过渡时长 |
| phaseTransitionTextDuration | 1.0 | 秒 | "弦一郎·雷"文字停留时长 |

## 与其他系统的交互

### 输入

- `CombatEvents.OnPlayerDamaged` → 更新玩家血条 + 弹出伤害数字
- `CombatEvents.OnBossDamaged` → 更新 Boss 血条 + 弹出伤害数字
- `CombatEvents.OnPlayerPostureChanged` → 更新玩家架势条
- `CombatEvents.OnBossPostureChanged` → 更新 Boss 架势条
- `CombatEvents.OnPerfectDeflect` → 弹出黄色弹刀数字 + 屏幕闪光
- `CombatEvents.OnNormalBlock` → 弹出灰色格挡数字
- `CombatEvents.OnMikiriCounter` → 弹出蓝色识破数字
- `CombatEvents.OnBossPostureBreak` → 显示忍杀提示
- `CombatEvents.OnDeathblow` → 隐藏忍杀提示 + 全屏闪白
- `CombatEvents.OnPhaseTransition` → 播放阶段转换 UI
- `CombatEvents.OnLightningCounterSuccess` → 闪电特效 + 雷反数字
- `CombatEvents.OnLightningCounterFail` → 屏幕边缘紫色脉冲
- `CombatEvents.OnHealingChargeChanged` → 更新药葫芦计数
- `DangerSystem.OnDangerWarning` → 显示危字提示
- `UIConfig` → 读取所有 UI 参数

### 输出

- `UI` 系统为纯显示层，不向其他系统输出数据
- 唯一例外：结算画面的"重新开始"按钮 → 通知 `GameManager`

## 测试要点

### EditMode 单元测试

- [ ] `UpdateHealth_At50Percent_BarHalfFull` — 50% 血量时血条半满
- [ ] `UpdateHealth_DelayBarFollows_03Seconds` — 残影条延迟 0.3 秒跟随
- [ ] `UpdatePosture_At80Percent_StartsFlashing` — 架势 80% 以上变红闪烁
- [ ] `UpdatePosture_At100Percent_TriggersBreak` — 架势满时触发崩溃动画
- [ ] `SpawnDamageNumber_Normal_White24pt` — 普通伤害白色 24pt
- [ ] `SpawnDamageNumber_Deflect_Yellow28pt` — 弹刀伤害黄色 28pt
- [ ] `SpawnDamageNumber_Mikiri_Blue32pt` — 识破伤害蓝色 32pt
- [ ] `ShowDanger_ThrustType_ShowsDownArrow` — 突刺危字显示↓符号
- [ ] `ShowDanger_SweepType_ShowsRightArrow` — 扫击危字显示→符号
- [ ] `ShowDeathblow_BlinksAtSpeed3` — 忍杀提示以速度 3 闪烁
- [ ] `HealingChargeChanged_UpdatesCounter` — 回血次数变化更新显示
- [ ] `DamageNumber_Lifetime_1Second_FadesOut` — 伤害数字 1 秒后淡出

## 验收标准

1. 玩家和 Boss 血条实时更新，受伤时有残影条延迟跟随效果
2. 架势条实时更新，超过 80% 时变红闪烁预警
3. 架势崩溃时播放破碎动画
4. 伤害数字在命中位置弹出，颜色区分类型（白/黄/蓝）
5. 危字提示在 Boss 出招前显示，带有正确的方向符号
6. Boss 架势归零时头顶显示闪烁的红色忍杀提示
7. 药葫芦计数器实时更新剩余次数
8. 阶段转换时播放黑屏 + "弦一郎·雷"文字
9. 所有 UI 更新通过 CombatEvents 事件驱动，不直接查询战斗模块
10. 结算画面在战斗结束后正确显示统计数据
