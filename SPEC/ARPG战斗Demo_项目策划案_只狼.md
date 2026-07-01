# ARPG战斗Demo项目策划案 — 只狼

> 复刻只狼战斗系统 | 一场经典boss战：苇名弦一郎
> 引擎：Unity 2022 LTS + URP | 开发周期：8周 | 用途：秋招作品集

---

## 目录

1. [项目概述](#一项目概述)
2. [操作设计](#二操作设计)
3. [技术架构](#三技术架构)
4. [弹刀系统](#四弹刀系统)
5. [Boss弹刀系统](#五boss弹刀系统)
6. [架势条系统](#六架势条系统)
7. [危字与识破系统](#七危字与识破系统)
8. [雷电反击系统](#八雷电反击系统)
9. [回血系统](#九回血系统)
10. [主角状态机](#十主角状态机)
11. [弦一郎Boss设计](#十一章一郎boss设计)
12. [打击感与视觉反馈](#十二打击感与视觉反馈)
13. [动画系统设计](#十三动画系统设计)
14. [UI/HUD设计](#十四uihud设计)
15. [数据结构设计](#十五数据结构设计)
16. [场景设计](#十六场景设计)
17. [开发排期](#十七开发排期)
18. [资源清单](#十八资源清单)
19. [面试展示策略](#十九面试展示策略)

---

## 一、项目概述

### 1.1 核心体验

复刻只狼的核心战斗系统，以一场完整的弦一郎boss战作为展示载体。

玩家体验应该是：**弹刀节奏感强、攻防博弈紧张、雷电反击爽快、打赢后有成就感。**

### 1.2 设计哲学

> **少即是多。** 只有5个操作键，但每个操作都有深度。弹刀不只是防御，更是进攻；架势不只是血条的附属，而是独立的核心博弈维度。

### 1.3 核心卖点（面试记忆点）

| 卖点 | 展示能力 |
|------|---------|
| 弹刀判定（帧级窗口 + 抖刀惩罚） | 碰撞判定、帧级输入响应 |
| 架势条双轨博弈 | 数值系统、状态机设计 |
| 抖刀惩罚机制 | 反作弊/反无脑操作设计思维 |
| 连续弹刀加成链 | 战斗节奏设计 |
| 识破（Mikiri Counter） | 输入方向 + 攻击类型判定 |
| 雷电反击 | 多阶段状态管理 |
| 弦一郎双阶段AI | 行为设计、阶段转换 |

---

## 二、操作设计

### 2.1 完整操作表

| 操作 | 按键 | 说明 |
|------|------|------|
| 移动 | WASD | 8方向移动 |
| 跳跃 | 空格 | **一段跳**，不可二段跳 |
| 闪避/识破 | Shift | 普通时=闪避，突刺危字时=识破 |
| 攻击/忍杀 | 鼠标左键 | 3段连斩；敌人架势归零时贴近自动忍杀 |
| 弹刀/格挡 | 鼠标右键 | 轻点=完美弹刀，按住=格挡 |
| 回血 | E | 药葫芦，10次，每次回复约30%血量 |
| 锁定 | 鼠标中键 | 锁定弦一郎，相机跟随 |
| 视角 | 鼠标移动 | 自由转动 |

### 2.2 操作优先级（高→低）

```
忍杀 > 弹刀/格挡 > 攻击 > 识破/闪避 > 跳跃 > 回血 > 移动
```

> 这意味着：架势归零时左键优先触发忍杀而不是攻击；弹刀输入优先于攻击输入。

---

## 三、技术架构

### 3.1 整体架构

```
┌─────────────────────────────────────────────┐
│                  GameManager                 │
│  (游戏状态：菜单/战斗中/转场/结算)            │
└───────────────────────┬─────────────────────┘
                        │
        ───────────────┼───────────────
        ▼               ▼               ▼
──────────────┐ ┌──────────────┐ ┌──────────────┐
│  Player      │ │  Boss        │ │  Camera      │
│  Controller  ││  Controller  │ │  Controller  │
│  (HFSM)      │ │  (HFSM)      │ │  (Cinemachine)│
└─────────────┘ └──────┬───────┘ └──────────────
       │                │
       ▼                ▼
┌──────────────────────────────────────────────
│               Combat System                   │
│  ├ DeflectSystem     (弹刀判定+抖刀惩罚)      │
│  ├ PostureSystem     (架势条管理)             │
│  ├ DamageSystem      (伤害计算)               │
│  ├ DangerSystem      (危字提示+应对判定)      │
│  ├ LightningSystem   (雷电反击)               │
│  └ HitStopSystem     (帧冻结)                 │
└──────────────────────────────────────────────┘
       │
       ▼
┌──────────────────────────────────────────────┐
│              Data Layer (ScriptableObject)    │
│  ├ PlayerStats       (主角属性)               │
│  ├ BossStats         (弦一郎属性)             │
│  ├ AttackData[]      (攻击动作数据)           │
│  └ CombatConfig      (全局战斗参数)           │
└──────────────────────────────────────────────┘
```

### 3.2 核心设计原则

1. **分层状态机（HFSM）**：主角和Boss都使用HFSM，避免switch-case失控
2. **数据驱动**：所有战斗参数用ScriptableObject配置，方便调优
3. **事件解耦**：CombatEvents事件系统，UI/音效/VFX监听事件而非直接耦合
4. **帧级精度**：弹刀判定不依赖OnTriggerEnter，用Physics.OverlapSphere每帧主动检测

---

## 四、弹刀系统（核心中的核心）

### 4.1 弹刀判定逻辑

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

### 4.2 抖刀惩罚实现

```csharp
public class DeflectSystem {
    [SerializeField] private float baseDeflectWindow = 0.2f;  // 12帧@60fps
    [SerializeField] private float minDeflectWindow = 0.016f;  // 1帧@60fps
    [SerializeField] private float spamResetTime = 0.5f;       // 0.5秒重置窗口
    [SerializeField] private float windowReductionPerSpam = 0.015f;
    [SerializeField] private float minPostureDamageMultiplier = 0.2f;
    
    private int spamCount = 0;
    private float lastDeflectPressTime = 0f;
    private float lastDeflectReleaseTime = 0f;
    private bool isDeflecting = false;
    private float deflectTimer = 0f;
    
    public float GetCurrentDeflectWindow() {
        // 每多抖一次，窗口缩小约1帧，最低1帧
        float reduction = spamCount * windowReductionPerSpam;
        return Mathf.Clamp(baseDeflectWindow - reduction, minDeflectWindow, baseDeflectWindow);
    }
    
    public float GetPostureDamageMultiplier() {
        // 每多抖一次，对敌方架势伤害降低，最低20%
        float multiplier = 1.0f - spamCount * 0.1f;
        return Mathf.Max(multiplier, minPostureDamageMultiplier);
    }
    
    public void OnDeflectPressed() {
        float now = Time.time;
        // 0.5秒内连续按 → spam计数+1
        if (now - lastDeflectReleaseTime < spamResetTime) {
            spamCount++;
        } else {
            spamCount = 0;
        }
        lastDeflectPressTime = now;
        isDeflecting = true;
        deflectTimer = GetCurrentDeflectWindow();
    }
    
    public void OnDeflectReleased() {
        lastDeflectReleaseTime = Time.time;
        isDeflecting = false;
    }
    
    public void OnSuccessfulDeflect() {
        spamCount = 0; // 成功弹刀，重置惩罚
    }
}
```

### 4.3 连续弹刀加成

```
连续成功弹刀时，对敌方架势条的伤害递增：

  第1次弹刀 → 基础架势伤害 × 1.0
  第2次弹刀 → 基础架势伤害 × 1.2
  第3次弹刀 → 基础架势伤害 × 1.4
  第4次弹刀 → 基础架势伤害 × 1.5
  第5次+   → 封顶 × 1.5

解除：弹刀失败 / 超过1秒未弹刀 → 计数归零
```

### 4.4 弹刀窗口分档（S/L Deflect）

```
不同攻击的弹刀窗口不同：

  轻型攻击（普通挥砍）→ 弹刀窗口 12帧（标准）
  重型攻击（重劈/突刺）→ 弹刀窗口 6-7帧（更严格）
  
弹刀重型攻击成功后，对敌方造成的架势伤害更大（约1.5倍）
```

### 4.5 弹刀碰撞检测方式

```
不依赖 OnTriggerEnter（有物理刷新延迟）
→ 使用 Physics.OverlapSphere 绑定在敌人武器骨骼上
→ 每帧主动检测是否与主角的弹刀判定框交叉

实现：
  敌人武器末端挂一个小的Sphere Collider（trigger）
  主角弹刀激活时，每帧检测该Sphere是否在主角的弹刀范围内
  同时检测主角朝向与攻击方向的夹角 < 90°（正面弹刀）
```

### 4.6 弹反节奏——攻防转换的核心

只狼战斗中有4种基础操作：攻击、防御（格挡/弹反）、跳跃、垫步。其中**弹反是整个攻防循环的枢纽**。

```
弹反的时机：
  在敌人攻击快要落到自己身上的一瞬间，点按防御键。
  卡准时机防御成功 = 弹反成功。

弹反成功的瞬间效果：
  ── 己方不受到伤害
  ── 敌方架势条大量增长
  ── 刀上爆出明亮的大火花 + 清脆的打铁声
  ── 敌方进入大硬直
```

**弹反是攻守转换的开关。** 弹反成功时，由于敌方受到大硬直，他的下一次攻击一定会比我的攻击慢。如果此时双方都选择攻击，我方的攻击会先一步命中敌方，造成伤害并打断他的攻击。反之，当我方被敌方弹反时，为避免攻击被打断，我方应该立刻切换防御。

```
完整攻防循环：

  我方攻击 ──→ 敌方弹反成功（我方被弹反）
    │                    │
    │               我方攻击被打断
    │               我方应切换防御
    │                    │
    ▼                    ▼
  我方防御 ── 敌方攻击 ──
    │
    ├── 卡准时机 ──→ 我方弹反成功
    │                    │
    │               敌方大硬直
    │               攻守转换！
    │                    │
    ▼                    ▼
  我方攻击 ──→ 命中敌方（敌方攻击被打断）
    │
    └── 循环继续...

核心节奏：攻 → 被弹 → 防 → 弹反 → 攻 → 命中 → 攻 → ...
```

这个循环就是只狼战斗的DNA——**弹反不是防御的终点，而是进攻的起点。** 整个战斗不是"你打我一下我打你一下"的回合制，而是通过弹反不断夺取主动权、压制对手的过程。

---

## 五、Boss弹刀系统

> 只狼的核心博弈是**双向弹刀**——不只是玩家弹Boss，Boss也能弹玩家。
> 狼的弹刀和攻击行为直接影响Boss的出招逻辑，这正是只狼的内核。

### 5.1 Boss弹刀判定逻辑

```
Boss弹刀系统与玩家弹刀对称，但有几个关键差异：

├── Boss弹刀窗口比玩家窄
│    玩家：12帧（标准）
│    Boss：9帧（约0.15秒）→ 给玩家可乘之机
│
├── Boss弹刀由AI控制，不是玩家输入
│    AI在特定时机进入弹刀姿态 → 姿态期间有弹刀窗口
│    玩家攻击落在窗口内 → Boss弹刀成功
│    落在窗口外 → Boss普通格挡或未防御
│
── Boss弹刀成功的效果
     ├─ 弹刀特效（小火花，比玩家弹刀略小）
     ├─ 弹刀音效（金属"叮"声，略沉闷）
     ├─ 帧冻结 0.03秒
     ├─ 玩家架势大幅上升（基础值 × 1.5）
     ├─ 玩家攻击被弹回，短暂硬直（约0.3秒）
     └─ Boss获得反击窗口 → 立即接反击招式
```

### 5.2 Boss弹刀状态机

```csharp
public class BossDeflectSystem {
    [SerializeField] private float bossDeflectWindow = 0.15f;   // 9帧@60fps（比玩家窄）
    [SerializeField] private float blockWindow = 0.3f;          // 普通格挡窗口
    [SerializeField] private float stanceDuration = 0.4f;       // 弹刀姿态总时长
    [SerializeField] private float posturePenaltyMultiplier = 1.5f; // 被弹反后玩家架势惩罚
    [SerializeField] private float deflectChanceOnAttack = 0.4f;    // 玩家攻击时弹刀概率
    [SerializeField] private float deflectChanceInCombo = 0.6f;     // 玩家连招中弹刀概率
    [SerializeField] private float deflectChanceLowHealth = 0.25f;  // Boss低血量时概率降低
    
    private bool isDeflectStance = false;
    private float deflectStanceTimer = 0f;
    private float activeWindowTimer = 0f;
    private int consecutivePlayerAttacks = 0;  // 玩家连续攻击计数
    
    // AI决定进入弹刀姿态
    public void EnterDeflectStance() {
        isDeflectStance = true;
        deflectStanceTimer = stanceDuration;
        activeWindowTimer = bossDeflectWindow;
    }
    
    public void Update() {
        if (!isDeflectStance) return;
        
        deflectStanceTimer -= Time.deltaTime;
        if (activeWindowTimer > 0) activeWindowTimer -= Time.deltaTime;
        
        if (deflectStanceTimer <= 0) {
            ExitDeflectStance();
        }
    }
    
    private void ExitDeflectStance() {
        isDeflectStance = false;
        consecutivePlayerAttacks = 0;  // 姿态结束，重置连击计数
    }
    
    // 检测玩家攻击是否被弹开
    public DeflectResult TryDeflect(AttackData playerAttack, Vector3 attackDirection, Vector3 bossForward) {
        if (!isDeflectStance) return DeflectResult.None;
        
        // 正面检测：攻击方向与Boss朝向夹角 < 90°
        float angle = Vector3.Angle(attackDirection, bossForward);
        if (angle > 90f) return DeflectResult.None;
        
        if (activeWindowTimer > 0) {
            return DeflectResult.PerfectDeflect;
        } else {
            return DeflectResult.NormalBlock;
        }
    }
    
    // AI弹刀概率判定
    public bool ShouldEnterDeflectStance(float bossHealthPercent) {
        float chance = deflectChanceOnAttack;
        
        // 玩家连续攻击 → 提高弹刀概率
        if (consecutivePlayerAttacks >= 2) {
            chance = deflectChanceInCombo;
        }
        
        // Boss低血量 → 降低弹刀概率（给玩家机会）
        if (bossHealthPercent < 0.3f) {
            chance = deflectChanceLowHealth;
        }
        
        consecutivePlayerAttacks++;
        return Random.value < chance;
    }
    
    public void ResetComboCount() {
        consecutivePlayerAttacks = 0;
    }
    
    public enum DeflectResult {
        None,
        NormalBlock,
        PerfectDeflect
    }
}
```

### 5.3 AI出招逻辑与弹刀的交互

```
Boss的AI不是固定循环，而是根据玩家行为动态决策：

玩家行为                → Boss反应
─────────────────────────────────────────
玩家普攻命中            → 40%概率进入弹刀姿态（下次攻击时弹）
玩家连续攻击（3段以上）  → 60%概率进入弹刀姿态
玩家弹刀成功            → 短暂硬直，后退，下一招大概率防御姿态
玩家架势崩溃边缘        → 激进攻击，弹刀概率降低
玩家距离远              → 追击/射箭（一阶段）
玩家喝药                → 大概率突进攻击（惩罚喝药）

阶段转换后（二阶段）：
  弹刀概率整体提升10%
  新增雷电反击后的强制弹刀姿态
  低血量时不再降低弹刀概率（更激进）
```

### 5.4 玩家攻击命中Boss时的判定流程

```csharp
// 玩家攻击命中逻辑
public void OnPlayerAttackHit(AttackData attack, BossController boss) {
    // 先问Boss：你要弹吗？
    BossDeflectSystem.DeflectResult result =
        boss.deflectSystem.TryDeflect(attack, playerAttackDirection, boss.transform.forward);
    
    switch (result) {
        case BossDeflectSystem.DeflectResult.PerfectDeflect:
            // Boss完美弹刀
            VFXManager.Spawn("BossDeflectSpark", contactPoint);
            AudioManager.Play("boss_deflect_ding");
            HitStopManager.Trigger(0.03f);
            
            // 玩家受到惩罚
            float posturePenalty = attack.postureDamage * boss.deflectSystem.posturePenaltyMultiplier;
            playerPostureSystem.AddPosture(posturePenalty);
            playerCombatSystem.EnterHitStun(0.3f);  // 玩家短暂硬直
            
            // Boss获得反击窗口
            bossAI.TransitionToCounterAttackState();
            break;
            
        case BossDeflectSystem.DeflectResult.NormalBlock:
            // Boss普通格挡，减伤
            bossHealthSystem.TakeDamage(attack.damage * 0.3f);
            bossPostureSystem.AddPosture(attack.postureDamage * 0.3f);
            AudioManager.Play("boss_block");
            break;
            
        case BossDeflectSystem.DeflectResult.None:
            // Boss未防御，正常受击
            bossHealthSystem.TakeDamage(attack.damage);
            bossPostureSystem.AddPosture(attack.postureDamage);
            bossAI.TransitionToStaggerState(0.3f);
            break;
    }
}
```

### 5.5 弹刀配置（ScriptableObject）

```csharp
[CreateAssetMenu(fileName = "BossDeflectConfig", menuName = "Combat/BossDeflectConfig")]
public class BossDeflectConfig : ScriptableObject {
    [Header("弹刀窗口")]
    public float deflectWindow = 0.15f;      // 9帧@60fps（比玩家12帧窄）
    public float blockWindow = 0.3f;         // 普通格挡窗口
    public float stanceDuration = 0.4f;      // 弹刀姿态总时长
    
    [Header("AI弹刀概率")]
    public float deflectChanceOnPlayerAttack = 0.4f;   // 玩家攻击时40%弹刀
    public float deflectChanceInCombo = 0.6f;          // 玩家连招中60%弹刀
    public float deflectChanceLowHealth = 0.25f;       // Boss低血量时25%
    public float phase2Bonus = 0.1f;                   // 二阶段概率+10%
    
    [Header("惩罚参数")]
    public float posturePenaltyMultiplier = 1.5f;      // 被弹反后玩家架势升1.5倍
    public float playerHitStunDuration = 0.3f;         // 被弹反后玩家硬直时长
    public float hitStopDuration = 0.03f;              // 帧冻结时长
}
```

### 5.6 设计要点总结

| 要点 | 说明 |
|------|------|
| Boss弹刀窗口比玩家窄 | 玩家12帧，Boss 9帧，否则玩家没法打 |
| AI弹刀要有规律可学 | 不能纯随机，玩家要能通过观察掌握节奏 |
| 弹刀后Boss有反击窗口 | 弹反成功→Boss立刻反击，攻防转换 |
| 玩家连砍会被弹到崩溃 | "不能无脑砍"的核心设计意图 |
| Boss低血量时弹刀减少 | 给玩家翻盘机会，符合boss战节奏 |
| 二阶段弹刀更激进 | 难度递增，检验学习成果 |

---

## 六、架势条系统

### 5.1 双轨博弈

```
玩家架势条 ←→ 弦一郎架势条

  双方各自有一条架势条（独立于血量）
  攻击和弹刀都会影响双方架势
  架势归零 → 崩溃 → 可忍杀（对Boss）/ 长时间硬直（对玩家）
```

### 5.2 架势变化规则

| 行为 | 对敌方架势 | 对己方架势 |
|------|-----------|-----------|
| 普攻命中 | +基础削韧值 | 无 |
| 重击命中 | +大量削韧值 | 无 |
| 完美弹刀 | +大幅削韧（受加成链影响） | -恢复5-10% |
| 普通格挡 | 无 | +小幅上升 |
| 被攻击未弹刀 | 无 | +大幅上升 |
| 抖刀惩罚后弹刀 | +递减（最低20%） | -恢复量也递减 |
| 识破成功 | +大量削韧 | 无 |
| 跳跃踩头 | +中量削韧 | 无 |
| 停止交战2秒后 | 缓慢恢复 | 缓慢恢复 |

### 5.3 架势恢复

```
恢复逻辑：
  脱战2秒后 → 架势以每秒15%的速度恢复
  主动格挡状态 → 恢复速度减半（按住右键格挡时恢复更慢）
  受击后 → 恢复计时器重置
```

### 5.4 崩溃判定

```
当 currentPosture >= maxPosture：

  对玩家：
    → 强制进入 StunState
    → 播放大硬直动画（约1.5秒）
    → 期间无法操作
    → 弦一郎获得自由攻击窗口

  对弦一郎：
    → 播放崩溃动画（约2秒）
    → 头顶出现红色忍杀提示
    → 玩家贴近按左键 → 触发忍杀
```

---

## 七、危字与识破系统

### 7.1 三种危的应对

只狼中有三种危：**下段危、突刺危、擒拿危**。面对危时，玩家需要观察敌方的抬手动作，迅速判断出是哪种类型的危，从而采取正确的应对措施。

```
┌─────────────────┬─────────────────┬─────────────────┐
│    下段危        │    突刺危        │    擒拿危        │
│   红色↓符号      │   红色↓符号      │   红色↑符号      │
─────────────────┼─────────────────┼─────────────────┤
│  空格 跳跃      │  Shift 识破     │  Shift 垫步     │
│  跳过攻击       │  踩住敌人的刀    │  拉开距离        │
│  可踩头输出     │  敌方架势大幅↑  │  躲开投技        │
│                 │  敌人短暂硬直    │  无额外奖励      │
└─────────────────┴─────────────────┴─────────────────┘
```

#### 下段危

> 敌方砍你的脚（低位横扫攻击）。
> 应对方式：**跳跃**。跳过攻击后可以踩头输出。

```
特征：敌人身体下沉，武器从下往上/水平扫过
示例：弦一郎的低位横扫
玩家操作：按空格跳跃 → 跳过攻击 → 在空中可踩头（追加攻击）
```

#### 突刺危

> 敌人使用武器向前戳你（如弦一郎的突刺、小兵用刀/枪戳）。
> 应对方式：**Shift识破**。通过向前踏步（不按方向键）使出识破，踩住敌人的武器。

```
特征：敌人身体前倾，武器/肢体直线向前刺出
示例：弦一郎突刺、枪足突刺、小兵刀刺
玩家操作：按Shift（不按方向键）→ 前踏踩刀 → 敌方架势大幅上升

注意：
  - 识破是默认就会的技能，不需要学习
  - 识破必须正对突刺方向（夹角 < 60°）
  - 距离太远则识破不触发，变成普通闪避
  - 非突刺型攻击按Shift → 普通侧闪/后闪
```

#### 擒拿危

> 敌人用手/武器抓你（类似投技），如法环中的投技攻击。
> 应对方式：**垫步拉开距离**。配合方向键Shift闪避，避免被擒拿。

```
特征：敌人伸手/伸武器准备抓取，动作较慢但一旦抓住伤害极高
示例：部分Boss的投技、擒拿类敌人
玩家操作：按Shift + 方向键（侧向/后退）→ 闪避拉开距离
```

### 7.2 识破详细逻辑

```
敌方攻击为"突刺型"（带红色危字提示）：

玩家按Shift → 【识破】
  1. 播放识破动画（前踏一步，踩住敌人的刀）
  2. "咔嚓"踩断音效
  3. 敌方架势大幅上升（比弹刀更多）
  4. 敌人短暂硬直（约0.5秒）
  5. 玩家可立即接攻击输出

失败情况：
  - 非突刺型攻击按Shift → 普通闪避
  - 突刺型攻击没按Shift → 被刺中，受伤 + 架势上升
  - 距离太远 → 识破不触发，变成普通闪避
```

### 7.3 识破的判定条件

| 条件 | 说明 |
|------|------|
| 攻击类型 | 必须是 `attackType == Thrust` |
| 距离 | 玩家与敌人距离 < 3米 |
| 角度 | 敌人攻击方向与玩家朝向夹角 < 60° |
| 时机 | 在攻击判定期内按下Shift |

---

## 八、雷电反击系统（二阶段核心）

### 7.1 完整流程

```
弦一郎跳起蓄雷（红色危字提示）：

  第一阶段：雷电下落
  └── 玩家被雷击中 → 进入"带电"状态
       ├─ 角色全身带电特效
       ├─ 进入短暂的输入等待窗口（约0.5秒）
       └─ 如果玩家不做任何操作 → 受到雷电伤害 + 架势大幅上升

  第二阶段：接雷（空中按左键）
  └── 角色双手举起，雷电停在手中
       ├─ 短暂的定格感（约0.3秒）
       ├─ 输入等待窗口（约1秒）
       └─ 如果超时未弹 → 受到雷电伤害

  第三阶段：弹雷（再次按左键）
  └── 雷电被掷回弦一郎
       ├─ 弦一郎被电麻（约1.5秒硬直）
       ├─ 弦一郎架势大幅上升
       ├─ 屏幕震动 + 闪电特效
       ── 大量输出窗口

  失败情况：
  └── 未在空中按左键 → 被电，大伤害
       按了但没及时弹 → 被电，大伤害
```

### 7.2 状态实现

```csharp
enum LightningState {
    None,           // 未触发
    Falling,        // 雷电下落中
    Charged,        // 已接雷，等待弹回
    Reflected,      // 已弹回
    Failed          // 失败，被电
}

public class LightningCounterSystem {
    private LightningState state = LightningState.None;
    private float inputWindow = 0f;
    
    public void OnLightningHit() {
        state = LightningState.Charged;
        inputWindow = 1.0f; // 1秒等待窗口
        // 播放接雷特效
    }
    
    public void OnAttackPressed() {
        if (state == LightningState.Charged) {
            state = LightningState.Reflected;
            // 播放弹雷特效
            // 弦一郎进入大硬直
        }
    }
}
```

---

## 九、回血系统

### 8.1 药葫芦

| 参数 | 数值 |
|------|------|
| 按键 | E |
| 总次数 | 10次 |
| 每次回复 | 约30%最大生命值 |
| 回复时间 | 约0.8秒（喝药动画时长） |
| 可打断 | 是（喝药期间受击 → 打断，已消耗的次数不退还） |
| 使用限制 | 不可在受击硬直/崩溃/忍杀演出中使用 |

### 8.2 回血逻辑

```
按E → 检查条件：
  ├─ 剩余次数 > 0？
  ├─ 当前不在硬直/被控状态？
  ├─ 当前不在其他不可打断动画中？
  │
  ├─ 全部满足 → 播放喝药动画（0.8秒）
  │    └── 动画中途受击 → 打断，次数已扣除
  │    └── 动画播完 → 回复生命值
  │
  └─ 不满足 → 不响应
```

---

## 十、主角状态机

### 9.1 分层状态机（HFSM）

```
PlayerStateMachine
│
├── 【基础层 Base Layer】
│   ├── GroundedState（地面状态）
│   │   ├── IdleSubState（待机）
│   │   ├── MoveSubState（移动）
│   │   └── LockOnMoveSubState（锁定移动）
│   │
│   └── AirborneState（空中状态）
│       ├── JumpSubState（跳跃）
│       ── FallSubState（下落）
│
├── 【战斗层 Combat Layer】（高优先级，可打断基础层）
│   ├── AttackState（攻击状态）
│   │   ├── Startup（前摇，可被弹刀打断）
│   │   ├── Active（判定生效，Animation Event触发EnableHitbox）
│   │   └── Recovery（后摇，可被弹刀/闪避取消）
│   │
│   ├── DeflectState（弹刀状态）
│   │   ├── ActiveWindow（完美弹刀窗口，约12帧）
│   │   └── BlockWindow（普通格挡窗口，13-36帧）
│   │
│   ├── DodgeState（闪避状态）
│   │   └── 无敌帧约6-8帧
│   │
│   ├── MikiriState（识破状态）
│   │   └── 前踏动画 + 踩刀判定
│   │
│   ├── HitState（受击状态）
│   │   └── 锁定所有输入
│   │
│   ├── StunState（架势崩溃状态）
│   │   └── 长时间硬直，不可操作
│   │
│   ├── DeathblowState（忍杀状态）
│   │   └── 演出动画，锁定所有输入
│   │
│   ├── HealingState（喝药状态）
│   │   └── 可被打断
│   │
│   └── LightningChargeState（接雷状态）
│       └── 等待弹雷输入
│
└── 【特殊层 Special Layer】（最高优先级）
    ├── ExecuteState（被执行忍杀 - 对玩家）
    └── CutsceneState（转场/演出）
```

### 9.2 状态转换规则

```
任意战斗状态 → DeflectState：按下右键（高优先级打断）
任意战斗状态 → DodgeState：按下Shift（非突刺危字时）
任意战斗状态 → MikiriState：按下Shift（突刺危字时）
AttackState.Recovery → AttackState.Startup：连按左键（连招）
AttackState.* → DeflectState：按右键（弹刀取消后摇）
任意状态 → HitState：受到攻击且未弹刀
StunState → IdleSubState：硬直结束
崩溃敌人贴近 + 左键 → DeathblowState
```

### 9.3 输入缓冲

```csharp
public class InputBuffer {
    private Queue<CombatInput> buffer = new Queue<CombatInput>();
    private const float BUFFER_WINDOW = 0.15f; // 150ms缓冲窗口
    private float lastInputTime;
    
    public void AddInput(CombatInput input) {
        if (Time.time - lastInputTime > BUFFER_WINDOW) {
            buffer.Clear(); // 超时清空
        }
        buffer.Enqueue(input);
        lastInputTime = Time.time;
    }
    
    public CombatInput? GetNextInput() {
        return buffer.Count > 0 ? buffer.Dequeue() : null;
    }
}

public enum CombatInput {
    Attack, Deflect, DeflectRelease, Dodge, Jump, Heal, LockOn
}
```

---

## 十一、弦一郎Boss设计

### 10.1 Boss属性

| 属性 | 一阶段 | 二阶段 |
|------|--------|--------|
| 生命值 | 1000（第一管） | 1200（第二管） |
| 最大架势 | 300 | 400 |
| 攻击力 | 100 | 120 |
| 架势恢复速度 | 每秒10% | 每秒8%（恢复更慢） |
| 移动速度 | 中等 | 较快 |

### 10.2 一阶段攻击表（剑+弓）

| 编号 | 招式名称 | 类型 | 伤害 | 破韧值 | 前摇(帧) | 判定(帧) | 后摇(帧) | 可弹刀 | 危字类型 | 描述 |
|------|---------|------|------|--------|---------|---------|---------|--------|---------|------|
| 1 | 横斩 | 近战 | 100 | 15 | 10 | 4 | 12 | ✅ | 无 | 基础挥砍 |
| 2 | 上挑 | 近战 | 110 | 15 | 12 | 4 | 14 | ✅ | 无 | 从下往上的斩击 |
| 3 | 下劈 | 近战 | 120 | 20 | 14 | 5 | 16 | ✅ | 无 | 重劈，弹刀窗口较小 |
| 4 | 突刺 | 近战 | 150 | 25 | 16 | 6 | 18 | ❌ | 突刺↓ | 需识破，不可弹刀 |
| 5 | 跳跃劈 | 近战 | 130 | 20 | 20 | 6 | 14 | ✅ | 无 | 跳起后下劈 |
| 6 | 扫击 | 近战 | 100 | 10 | 10 | 8 | 10 | ❌ | 扫击→ | 需跳跃躲避，可踩头 |
| 7 | 三连斩 | 近战 | 80+90+100 | 10+10+15 | 8+6+8 | 3+3+4 | 10+8+12 | ✅✅✅ | 无 | 快速三连，节奏：快-快-重 |
| 8 | 射箭×3 | 远程 | 60×3 | 10×3 | - | - | - |  | 无 | 后撤步拉弓，需移动躲避 |
| 9 | 后撤步 | 位移 | 0 | 0 | 10 | - | - | - | 无 | 拉开距离，通常接射箭 |

**一阶段AI行为权重：**

| 行为 | 权重 | 触发条件 |
|------|------|---------|
| 横斩/上挑 | 25% | 距离 < 3米 |
| 三连斩 | 20% | 距离 < 2.5米 |
| 突刺 | 15% | 距离 < 3.5米，随机触发 |
| 下劈 | 10% | 距离 < 2米 |
| 跳跃劈 | 10% | 距离 < 4米 |
| 扫击 | 5% | 距离 < 2米，偶尔触发 |
| 后撤+射箭 | 15% | 距离 > 4米 或 被连续攻击后 |

### 10.3 二阶段攻击表（雷+剑）

二阶段保留一阶段所有招式，并新增：

| 编号 | 招式名称 | 类型 | 伤害 | 破韧值 | 前摇(帧) | 判定(帧) | 后摇(帧) | 可弹刀 | 危字类型 | 描述 |
|------|---------|------|------|--------|---------|---------|---------|--------|---------|------|
| 10 | 雷电下劈 | 雷电 | 200 | 40 | 25 | 8 | 20 | ❌ | 突刺↓(雷) | 跳起蓄雷→下落，需雷电反击 |
| 11 | 冲刺斩 | 近战 | 130 | 20 | 8 | 6 | 10 | ✅ | 无 | 快速冲刺接近+挥砍 |
| 12 | 雷光连斩 | 近战 | 90×4 | 12×4 | 6×4 | 4×4 | 8×4 | ✅✅✅✅ | 无 | 带电快速四连斩，需连续弹刀 |
| 13 | 雷电突刺 | 雷电 | 180 | 35 | 18 | 6 | 16 | ❌ | 突刺↓(雷) | 带电突刺，需识破 |

**二阶段AI行为权重变化：**

| 行为 | 权重变化 | 说明 |
|------|---------|------|
| 所有近战攻击 | 速度提升20% | 前摇缩短15% |
| 雷电下劈 | 新增，20%权重 | 每隔3-4次攻击必出一次 |
| 雷光连斩 | 新增，15%权重 | 压制玩家的连续弹刀能力 |
| 后撤+射箭 | 移除 | 二阶段不再射箭 |
| 架势恢复 | 速度降低 | 不给玩家喘息机会 |

### 10.4 Boss状态机

```
BossStateMachine
│
├── IdleState（待机）
│   └── 随机选择下一个行为
│
── MoveState（移动/追击）
│   └── 靠近玩家到攻击距离
│
├── AttackState（攻击）
│   ├── Startup（前摇）
│   ├── Active（判定生效）
│   └── Recovery（后摇）
│
├── RangedState（远程-射箭，仅一阶段）
│
├── LightningState（雷电攻击，仅二阶段）
│   ├── Leap（跳起蓄雷）
│   ├── Fall（下落）
│   ── Land（落地）
│
├── StaggerState（被弹刀硬直）
│   └── 短暂停顿，约0.3秒
│
── CollapseState（架势崩溃）
│   └── 2秒无法行动，等待忍杀
│
├── ExecutedState（被忍杀）
│   └── 处决演出，战斗结束
│
└── PhaseTransitionState（阶段转换）
    └── 黑屏转场（见下方）
```

### 10.5 阶段转换（黑屏转场）

```
第一管血归零：
  1. 弦一郎播放受击倒地动画（0.5秒）
  2. 屏幕渐黑（0.5秒）
  3. 黑屏显示文字："弦一郎·雷"（1秒）
  4. 屏幕渐亮（0.5秒）
  5. 弦一郎以二阶段姿态重新出现
  6. 玩家回复约30%生命值和架势
  7. 战斗继续
```

### 10.6 处决演出

```
弦一郎架势归零：
  1. 弦一郎播放崩溃动画（单膝跪地，刀拄地）
  2. 头顶出现红色"忍杀"提示
  3. 玩家贴近按左键 → 触发处决
  4. 处决动画：主角跳起 → 一刀斩下
  5. 弦一郎倒下，战斗结束
  6. 结算画面
```

---

## 十二、打击感与视觉反馈

### 11.1 打击感四要素

> **打击感 = 帧冻结 + 屏幕震动 + 特效 + 音效**
> 四者缺一不可，且必须在同一帧触发。

### 11.2 各场景打击感参数

| 场景 | 帧冻结 | 屏幕震动 | 命中特效 | 音效 |
|------|--------|---------|---------|------|
| 普攻命中 | 2帧 | 无 | 小火花 | 轻微金属声 |
| 普攻第3段命中 | 3帧 | 振幅0.03, 0.15秒 | 中火花 | 斩击声 |
| 弹刀命中 | 3帧 | 振幅0.08, 0.15秒 | 大型橙色火花（HDR发光） | "叮"金属清脆音 |
| 识破 | 4帧 | 振幅0.1, 0.2秒 | 踩刀特效 | "咔嚓"踩断声 |
| 跳跃踩头 | 3帧 | 振幅0.05, 0.15秒 | 踩击特效 | 沉闷打击声 |
| 雷电反击 | 6帧 | 振幅0.15, 0.3秒 | 全屏闪电特效 | 雷电爆裂声 |
| 忍杀 | 5帧 | 振幅0.12, 0.4秒 | 处决特效 | 处决音效 |
| 架势崩溃 | 4帧 | 振幅0.1, 0.3秒 | 崩溃粒子 | 崩溃音效 |

### 11.3 帧冻结实现

```csharp
public static class HitStopManager {
    private static float remaining = 0f;
    
    public static void Trigger(float seconds) {
        remaining = seconds;
    }
    
    public static bool IsActive => remaining > 0;
    
    // 在Update最开始调用
    public static void Update() {
        if (remaining > 0) {
            remaining -= Time.unscaledDeltaTime;
            // 跳过所有游戏逻辑更新，但保持渲染
        }
    }
}
```

### 11.4 视觉反馈层次

```
火花特效（两层）：
  VFX_Block（普通格挡）：火花小、颜色暗、存在时间短
  VFX_Deflect（完美弹刀）：火花大、高亮橙黄色（HDR发光）、放射状

屏幕后处理（Post-Processing）：
  架势即将崩溃 → Vignette（暗角）发暗
  被危字攻击命中 → Chromatic Aberration（色差）增强
  雷电反击成功 → 全屏闪白

锁定指示器：
  弦一郎脚下显示锁定标记（三角/圆圈）
```

---

## 十三、动画系统设计

### 12.1 Root Motion 策略

| 动画类型 | Root Motion | 说明 |
|---------|-------------|------|
| 移动（走/跑） | ❌ 代码控制 | Rigidbody.velocity |
| 跳跃 | ❌ 代码控制 | 抛物线运动 |
| 闪避 | ✅ 开启 | 保证位移真实感 |
| 攻击 | ✅ 开启 | 攻击位移跟随动画 |
| 识破 | ✅ 开启 | 前踏动作需要精确位移 |
| 受击后退 |  代码控制 | 需要可控的击退距离 |
| 喝药 | ✅ 开启 | 自然动作 |

### 12.2 动画事件（Animation Events）

在攻击动画的关键帧插入以下事件：

| 事件名 | 插入位置 | 作用 |
|--------|---------|------|
| `EnableHitbox()` | 刀刃挥出的瞬间 | 激活伤害判定 |
| `DisableHitbox()` | 挥刀结束 | 关闭伤害判定 |
| `EnableDeflectCancel()` | 前摇阶段末尾 | 标记此时可被弹刀指令打断 |
| `OnAttackHit()` | 判定帧 | 触发命中逻辑（伤害/架势/特效/音效） |

### 12.3 Blend Tree

```
移动Blend Tree：
  参数：moveX（-1到1）, moveZ（-1到1）
  混合：Idle ← Walk ← Run
  
锁定移动Blend Tree：
  参数：moveDirection（0-360°），speed（0-1）
  混合：锁定待机 ← 锁定移动（侧移/前后）
```

---

## 十四、UI/HUD设计

### 13.1 HUD布局

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
│  ┌──┐                                          │
│  │E │  ← 右下角，药葫芦剩余次数                  │
│  │10│                                          │
│  └──┘                                          │
│                                                │
│              [红色忍杀提示]  ← 架势归零时闪烁     │
│              [红色危字提示]  ← 特殊攻击前显示     │
────────────────────────────────────────────────┘
```

### 13.2 UI元素清单

| UI元素 | 位置 | 说明 |
|--------|------|------|
| 玩家血条 | 左上角 | 红色，带数字 |
| 玩家架势条 | 血条下方 | 白色/灰色 |
| Boss血条 | 屏幕上方居中 | 红色，显示Boss名"苇名弦一郎" |
| Boss架势条 | Boss血条下方 | 黄色 |
| 药葫芦计数器 | 右下角 | 显示剩余次数（E键图标+数字） |
| 锁定标记 | 敌人脚下 | 三角/圆圈指示器 |
| 危字提示 | 屏幕中央/角色上方 | 红色危字+方向符号 |
| 忍杀提示 | 崩溃敌人头顶 | 闪烁的红色"忍"字 |
| 伤害数字 | 命中位置 | 白色=普通，黄色=弹刀，蓝色=识破 |
| 阶段转换文字 | 屏幕中央 | "弦一郎·雷" |
| 结算画面 | 全屏 | 战斗统计数据 |

---

## 十五、数据结构设计

### 14.1 核心ScriptableObject

```csharp
[CreateAssetMenu(fileName = "PlayerStats", menuName = "Combat/PlayerStats")]
public class PlayerStats : ScriptableObject {
    public float maxHealth = 1000f;
    public float maxPosture = 300f;
    public float postureRecoveryRate = 15f;      // 每秒恢复15%
    public float attack = 100f;
    public float defense = 50f;
    public float moveSpeed = 5f;
    public float dodgeSpeed = 10f;
    
    // 弹刀参数
    public float baseDeflectWindow = 0.2f;        // 12帧@60fps
    public float minDeflectWindow = 0.016f;       // 1帧@60fps
    public float spamResetTime = 0.5f;
    public float windowReductionPerSpam = 0.015f;
    public float deflectPostureRecovery = 0.08f;  // 弹刀恢复8%架势
    
    // 连续弹刀加成
    public float[] deflectChainMultipliers = { 1.0f, 1.2f, 1.4f, 1.5f, 1.5f };
    
    // 攻击数据
    public AttackData[] attacks;                  // 3段普攻
    public AttackData heavyAttack;                // 重击
    
    // 回血
    public int maxHealingCharges = 10;
    public float healPercent = 0.3f;
    public float healDuration = 0.8f;
}

[CreateAssetMenu(fileName = "BossStats", menuName = "Combat/BossStats")]
public class BossStats : ScriptableObject {
    public float maxHealth;
    public float maxPosture;
    public float postureRecoveryRate;
    public float attack;
    public float moveSpeed;
    
    // 一阶段
    public AttackData[] phase1Attacks;
    public float[] phase1BehaviorWeights;
    
    // 二阶段
    public AttackData[] phase2Attacks;
    public float[] phase2BehaviorWeights;
    public float attackSpeedMultiplier = 1.2f;    // 二阶段攻速提升20%
    public float startupReduction = 0.85f;        // 前摇缩短15%
    
    // 阶段转换
    public float phaseTransitionHealthPercent = 0.5f;
}

[System.Serializable]
public class AttackData {
    public string animName;           // 动画名称
    public string attackName;         // 招式名称（调试用）
    public float damage;              // 伤害值
    public float postureDamage;       // 对敌方架势伤害
    public AttackType attackType;     // 普通/突刺/扫击/投技/雷电
    public bool canBeDeflected;       // 是否可弹刀
    public float startupFrames;       // 前摇帧数
    public float activeFrames;        // 判定帧数
    public float recoveryFrames;      // 后摇帧数
    public float deflectWindowFrames; // 弹刀窗口帧数（12或6-7）
    public float hitboxRadius;        // 攻击判定范围
    public Vector3 hitboxOffset;      // 攻击判定偏移
    public string vfxName;            // 攻击特效
    public string sfxName;            // 攻击音效
    public bool isRanged;             // 是否远程
}

public enum AttackType {
    Normal,       // 普通（可弹刀）
    Thrust,       // 突刺（需识破）
    Sweep,        // 扫击（需跳跃）
    Grab,         // 投技（需闪避）
    Lightning     // 雷电（需雷电反击）
}
```

### 14.2 事件系统

```csharp
public static class CombatEvents {
    public static event Action<float> OnPlayerDamaged;
    public static event Action<float> OnBossDamaged;
    public static event Action<float> OnPlayerPostureChanged;
    public static event Action<float> OnBossPostureChanged;
    public static event Action OnPerfectDeflect;
    public static event Action OnNormalBlock;
    public static event Action OnMikiriCounter;
    public static event Action OnBossPostureBreak;
    public static event Action OnPlayerPostureBreak;
    public static event Action OnDeathblow;
    public static event Action OnPhaseTransition;
    public static event Action OnLightningCounterSuccess;
    public static event Action OnLightningCounterFail;
    public static event Action<int> OnHealingChargeChanged;
}
```

---

## 十六、场景设计

### 15.1 场景方案：ProBuilder白盒竞技场

```
用ProBuilder快速搭建：
  1. 地面：20×20米 方形平台
  2. 四面矮墙：高2米，防止走出战斗区域
  3. 一个方向光：模拟黄昏暖光
  4. 天空盒：黄昏渐变
  5. （可选）几根柱子作为视觉遮挡

30分钟搞定，不需要美术资源。
```

### 15.2 为什么不花时间在场景上

- 面试看的是战斗系统，不是场景建模
- 白盒 + 写实材质已经足够清晰
- 省下的时间用来打磨打击感和准备面试

---

## 十七、开发排期（8周）

### 第1周：基础框架

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | Unity项目搭建（URP模板）+ 文件夹结构 + 基础场景（ProBuilder白盒） | 可运行空项目 |
| Day 3-4 | 角色控制器（WASD移动 + 一段跳 + 视角） | 角色能在场景移动 |
| Day 5 | 相机系统（跟随 + 锁定，Cinemachine） | 锁定Boss后相机跟随 |
| Day 6-7 | Mixamo角色导入 + 基础动画集成（Idle/Walk/Run/Attack） | 角色有基础动画 |

**里程碑：角色能在场景中移动、锁定、播放动画**

### 第2周：弹刀核心

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | 弹刀系统（完美弹刀判定 + 普通格挡） | 右键弹刀工作 |
| Day 3 | **抖刀惩罚机制** | 连续按右键窗口缩小 |
| Day 4 | 连续弹刀加成链 + 弹刀恢复架势 | 连续弹刀越来越强 |
| Day 5 | 架势条系统（双方） | 架势条显示和工作 |
| Day 6-7 | 打击感（帧冻结 + 屏幕震动 + 火花特效 + 弹刀音效） | 弹刀有感觉了 |

**里程碑：★弹刀手感完成——这是最重要的里程碑**

### 第3周：战斗系统完善

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | 普攻连招（3段） + 输入缓冲 + 连招取消 | 能流畅连招 |
| Day 3 | 识破系统（Shift + 突刺危字判定） | 识破工作 |
| Day 4 | 跳跃 + 跳跃踩头（应对扫击危字） | 跳跃应对危字 |
| Day 5 | 危字提示系统（突刺↓ / 扫击→） | 危字UI显示 |
| Day 6-7 | 回血系统（药葫芦，10次） + 受击/硬直状态 | 完整战斗循环 |

**里程碑：所有玩家操作完成，战斗系统闭环**

### 第4周：Boss AI（一阶段）

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | Boss状态机框架 + 基础AI（巡逻/追击/攻击） | Boss能动 |
| Day 3-4 | 一阶段所有攻击招式（横斩/上挑/下劈/突刺/三连斩等） | 一阶段AI完整 |
| Day 5 | 射箭系统（后撤+三连射） | 远程攻击可用 |
| Day 6-7 | Boss受击反馈 + 弹刀硬直 + 架势崩溃 + 危字攻击标记 | Boss完整战斗循环 |

**里程碑：一阶段可玩，能和弦一郎打起来**

### 第5周：二阶段 + 雷电反击

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | 阶段转换（黑屏转场） + 二阶段AI框架 | 能进入二阶段 |
| Day 3-4 | **雷电反击系统**（跳起蓄雷→接雷→弹雷） | 雷电反击工作 |
| Day 5 | 二阶段新增招式（冲刺斩/雷光连斩/雷电突刺） | 二阶段AI完整 |
| Day 6 | 二阶段加速逻辑（攻速+20%，前摇-15%） | 二阶段难度提升 |
| Day 7 | 忍杀系统 + 处决演出 | 处决完整 |

**里程碑：二阶段可玩，完整boss战跑通**

### 第6周：打磨

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | 全面手感调优（弹刀窗口、架势数值、攻击节奏） | 战斗手感满意 |
| Day 3 | UI/HUD完善（血条、架势条、药葫芦、危字、忍杀提示） | UI完整 |
| Day 4 | 场景美化（灯光/天空盒/简单粒子氛围） | 场景有氛围 |
| Day 5 | 音效完善（所有战斗音效 + BGM） | 音频完整 |
| Day 6-7 | Bug修复 + 边界情况处理 | 稳定版本 |

**里程碑：游戏看起来像个作品了**

### 第7周：录制与文档

| 日 | 任务 | 产出 |
|----|------|------|
| Day 1-2 | 录制战斗演示视频（1-2分钟） | B站视频 |
| Day 3-4 | 编写项目文档（架构图 + 系统设计说明 + GitHub README） | 文档完善 |
| Day 5 | 代码整理、注释、GitHub上传 | 可展示代码仓库 |
| Day 6-7 | 开始准备面试题 + 刷题 | 面试知识库 |

### 第8周：面试准备

| 日 | 任务 |
|----|------|
| 全天 | 面试题准备（Unity基础、渲染管线、数据结构算法） |
| | 简历打磨（项目描述要精炼） |
| | 模拟面试（把项目讲清楚） |

---

## 十八、资源清单

### 必装资源（第1天）

| 资源 | 获取方式 | 说明 |
|------|---------|------|
| **Mixamo角色** | https://www.mixamo.com/ | 备用方案，下载写实风格人形角色 FBX for Unity |
| **Mixamo动画** | https://www.mixamo.com/ | 按动画清单下载（Without Skin） |
| **URP** | Package Manager搜索"Universal RP" | 渲染管线 |
| **写实材质** | FBX内嵌材质提取（Use Embedded Materials） | 使用只狼原版解包资源自带材质 |

### 音效（第1-2周）

| 资源 | 获取方式 |
|------|---------|
| **Pixabay音效** | https://pixabay.com/sound-effects/search/sword/ （600+免费剑音效） |
| **Freesound弹刀音效** | https://freesound.org/people/Christopherderp/sounds/364529/ （金属"叮"声） |

### 特效（第2-3周）

| 资源 | 获取方式 |
|------|---------|
| **Unity Particle Pack** | Asset Store搜索"Particle Pack"，免费 |
| **自制弹刀火花** | Particle System手动调（2-3小时） |

### 场景

ProBuilder白盒，Package Manager搜索"ProBuilder"安装，30分钟搭建。

### 需要的动画清单（Mixamo搜索关键词）

| 用途 | 搜索关键词 |
|------|-----------|
| 待机 | `Idle Combat` `Idle Sword` |
| 走路 | `Walking` |
| 跑步 | `Running` `Jog` |
| 普攻 | `Slash` `Sword Slash` `Sword Attack` |
| 重击 | `Heavy Slash` `Overhead Attack` |
| 弹刀/格挡 | `Block` `Guard Stance` |
| 识破 | `Lunge` `Thrust`（或用攻击动画改） |
| 闪避 | `Dodge Roll` `Backflip` |
| 受击 | `Hit` `Stagger` `Damage` |
| 跳跃 | `Jump` `Jumping` |
| 跳跃攻击 | `Jump Attack` |
| 踩头 | 用跳跃动画替代 |
| 崩溃/硬直 | `Stagger` `Fall Over` |
| 死亡 | `Death` `KO` |
| 喝药 | `Drinking` `Potion` |
| 雷电蓄力 | `Power Up` `Channeling` |
| 雷电攻击 | `Lightning Strike` |

---

## 十九、面试展示策略

### 18.1 简历项目描述

```
【ARPG战斗系统Demo — 只狼】
复刻只狼核心战斗系统 | Unity 2022 + URP + C#

● 实现只狼式弹刀系统，包含帧级弹刀判定窗口（12帧@60fps）、
  抖刀惩罚机制（连续误触导致窗口递减）、连续弹刀加成链
● 设计双轨博弈架势条系统，弹刀/攻击/识破互相影响双方架势
● 实现危字应对系统（突刺→识破、扫击→跳跃踩头）
● 实现雷电反击多阶段状态机（蓄雷→接雷→弹回）
● 设计弦一郎双阶段AI行为树，一阶段剑+弓，二阶段雷+剑，
  含10+攻击招式和阶段转换逻辑
● 全参数ScriptableObject数据驱动，战斗参数可在编辑器热调整
● 使用分层状态机（HFSM）管理主角10+战斗状态，
  输入缓冲系统保证操作响应
```

### 18.2 面试高频问题准备

| 问题 | 回答要点 |
|------|---------|
| 弹刀判定怎么做的？ | Physics.OverlapSphere每帧检测 + 12帧窗口 + 攻击类型标记 |
| 抖刀惩罚的原理？ | 连续按键计数 → 窗口递减公式 → 成功弹刀重置 / 超时自动重置 |
| 连续弹刀为什么越来越强？ | 加成链数组 × 基础架势伤害，鼓励持续进攻 |
| 帧冻结怎么实现的？ | HitStopManager控制逻辑暂停，渲染继续 |
| 雷电反击的状态流转？ | None→Falling→Charged→Reflected/Failed，每阶段有输入窗口 |
| 架势恢复的逻辑？ | 脱战2秒后每秒15%，受击重置计时器 |
| 为什么用HFSM而不是Animator State Machine？ | 代码状态机更灵活，可以嵌套子状态，方便加新机制 |
| 为什么用ScriptableObject？ | 数据驱动，调参不用改代码 |
| 输入缓冲怎么做的？ | Queue + 150ms窗口，超时清空 |

### 18.3 视频录制要点

1. 开头5秒内展示完美弹刀的"叮"声+火花
2. 展示连续弹刀"叮叮叮"的节奏感
3. 展示识破踩刀的特写
4. 展示雷电反击的完整流程（接雷→弹回）
5. 最后放一段完整boss战（不剪辑），展示系统稳定性
6. 分辨率1080p 60fps，时长1-2分钟

---

## 附录A：风险与应对

| 风险 | 概率 | 应对 |
|------|------|------|
| 弹刀手感调不好 | 高 | 参考只狼实际帧数据，逐帧对比调整 |
| Mixamo动画不匹配 | 中 | 先用默认动画，后期替换，不纠结 |
| Boss AI太简单 | 中 | 宁可AI简单但稳定，不要有bug |
| 雷电反击逻辑复杂 | 中 | 拆成3个子状态逐步实现 |
| 时间不够 | 高 | 优先保弹刀+架势+一阶段AI，二阶段可以简化 |
| 材质显示问题 | 低 | 检查FBX材质槽位是否全部正确映射 |

## 附录B：Git提交规范

```
feat: 弹刀系统基础实现
fix: 抖刀惩罚计数未重置bug
refactor: 架势条系统重构为ScriptableObject
art: 添加弹刀火花特效
docs: 更新README
```

## 附录C：每日Checklist

```
□ 今天的任务完成了吗？
□ 有没有遇到阻塞问题？（记录，明天解决）
□ 当前版本能正常运行吗？（每天确保不崩）
□ Git提交了吗？（每天至少一次commit）
□ 离下一个里程碑还有多远？
```
