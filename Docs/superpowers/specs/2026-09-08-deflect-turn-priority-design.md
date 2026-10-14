# 弹反后先手权缺失：调查与设计对照

**状态：** 调查记录，未实施。代码未改。
**触发：** 用户实测 MeleeOnly 环境后反馈：「玩家弹反 Boss 之后，下一次攻击必定先手」的情况没有实现，怀疑是设计漏了。

## 结论

用户判断正确，这是设计缺口，不是配置问题。

`DeflectState.HandlePerfectParry` 里，玩家弹反 Boss 的完整效果只有三项：涨 Boss 架势、触发打铁表现、累计连续弹开次数。**既不打断 Boss 当前这一招，也不给玩家任何优先窗口。** Boss 招照播，下一招照接，玩家必须自己垫步躲开。

## 当前实现事实

源码位置 `Assets/Scripts/FrameWork/States/Ground/DeflectState.cs`：

```csharp
// 玩家被弹开进 ParriedState。Boss 连段（飞舟等）被弹不打断，才能连续弹反。
if (!brokeAttackerPosture && HitReactionUtil.IsPlayer(hit.attacker))
    hit.attacker.ForceParryStun();
```

这条 `IsPlayer` 守卫是玩家弹反 Boss 之后 Boss 完全不受影响的**唯一原因**，也是唯一的开关点。改动面很小。

同一处 `KengekiArmed` 同样被守卫限死：只有 Boss 弹反玩家才武装交锋，玩家弹反 Boss 不给玩家任何对应机制。

### 已有的可复用概念

`AttackStateBase.IsUninterruptibleAttack`（`Assets/Scripts/FrameWork/States/Base/AttackStateBase.cs:236`）已经在表达「打不断」这件事：

```csharp
// 危字整段、飞舟、JumpThrust / Jump_Danger 全段抓前摇打不断。
```

命中结算侧也用同一个标记拦住受击切换（`CharacterBody.cs:1001`，`if (AttackUninterruptible) return;`）。

**这意味着「哪些招该被弹断、哪些不该」项目里已经有现成的分类，不需要新造概念。**

## 外部对照（只狼）

### 用户实机复核结论（2026-09-08）

以下为用户亲自试玩原版后的确认，**优先级高于公开资料**：

- **普通平砍被弹反有停顿**，不是只有特殊招才有。
- **连段末尾也有停顿。**
- 净效果：**玩家下一次出手一定比 Boss 快。**
- 未测出：Boss 停顿结束后能不能挡住玩家的下一刀 —— 因为原版 Boss 被完美弹反后**很喜欢后撤步 + 射箭**，玩家根本抓不到它的防御动作。

最后这条有直接工程含义：**本项目已经实现了"后撤 + 射箭"，不是只狼专属行为。** 见下节。

### 本项目里"后撤射箭"的对应物

`GenichiroMoveTable.asset` 中：

| 招式 | 动作 | 距离 | 权重 |
|---|---|---|---|
| `Slash_Rush2` | `Slash_Rush2` | 5–99m | **300（全表最高）** |
| `Bow_Shot` | `Bow_Shot`（0.75s 出箭）| **7–99m** | 200 |
| `Slash_RushThenBow` | `Kengeki_Heavy → 3011` | — | 200 |

`Bow_Shot` 的 `minRange` 是 7 米 —— **Boss 被弹反后只要拉开到 7 米外，"射箭"就是权重最高、几乎必出的选择。** 这与用户在原版观察到的行为同源，不需要新做功能。

**测试推论：** 排查"Boss 停顿后能否防御"时，必须排除远程逃逸路径。当前的 `MeleeOnly` 白名单恰好屏蔽了全部弓招与 `Slash_Rush2`，所以 **MeleeOnly 环境才是能测出防御行为的干净环境**；常规环境下 Boss 会跑掉，测不到。

### 公开资料（参考，非规范）

以下来自公开资料，作为设计方向的参考而非规范：

- 完美弹反在只狼里有一个**金色闪光**指示，含义就是「现在轮到你了」。普通杂兵几乎每次弹反都给，Boss 只在**特定招式**上给 —— 也就是「弹到了一个可打断的招」。[r/Sekiro: Lesser known aspect of the Deflect System](https://www.reddit.com/r/Sekiro/comments/b9hlhu/lesser_known_aspect_of_the_deflect_system/)
- 弹反会**打断敌人当前的攻击序列**并给出巨大的反击窗口；没弹准则敌人继续连段。[r/Sekiro: When do you strike back after deflecting?](https://www.reddit.com/r/Sekiro/comments/bg2zk8/when_do_you_strike_back_after_deflecting/)
- 存在**霸体（hyper-armor）招式**，进入该动画后无法被打断，是「有时弹了也没用」的来源。
- 只狼核心循环是「**攻击 → 敌人格挡 → 敌人弹反你 → 你转防 → 你的回合反打 → 继续攻击**」。回合切换的信号就是弹反。[r/Sekiro: When to attack and when to deflect?](https://www.reddit.com/r/Sekiro/comments/16inyas/when_to_attack_and_when_to_deflect/)

与本项目的关键差异：**本项目用「Boss 主动弹反玩家」承担"敌人夺回回合"这一半，但缺了"玩家弹反 Boss 夺回回合"的另一半。** 攻防转换是单向的。

## 已经量好的关键数据

| 角色 | 硬直动画 | 帧数 @30fps | 动画长度 | 播到 95% | 当前 `ParriedDuration` | 实际硬直 |
|---|---|---|---|---|---|---|
| 玩家 | `a050_120104.fbx` → `Deflected` | 0–19 | 0.633s | **0.602s** | 0.35 | **0.60s** |
| Boss | `被弹反.fbx` → `Deflected` | 0–30 | **1.031s** | 0.979s | 0.35 | **0.35s** |

Boss 的动画**比配置长得多**：`ParriedState` 只等 `timer >= duration`（0.35），所以 Boss 的硬直动画会**被切掉三分之二**。这是当前 Boss 被弹反时"看不出停顿"的第二个原因 —— 就算接上了硬直，0.35 秒也会让动画播个开头就被掐掉。

**长度参考：** 玩家完成一次"点攻击（松键）+ 走完前摇"约 **0.43 秒**。所以停顿低于 0.43 秒时，玩家在停顿内连前摇都走不完 —— "必定先手"要靠停顿结束后的那点余量补，而不是靠停顿本身。

## 建议方案（待用户确认）

### 改动点

`DeflectState.HandlePerfectParry`，把现有守卫从"只弹玩家"改成"弹可打断的招"：

```csharp
// 现在
if (!brokeAttackerPosture && HitReactionUtil.IsPlayer(hit.attacker))
    hit.attacker.ForceParryStun();

// 建议
if (!brokeAttackerPosture && !hit.attacker.AttackUninterruptible)
    hit.attacker.ForceParryStun(armKengeki: false);
```

用 `AttackUninterruptible` 直接复用已有的"打不断"分类（危字 / 飞舟 / JumpThrust / Jump_Danger），不需要新概念，也不破坏连续弹反。

### 三个必须显式处理的点

1. **`armKengeki: false` 不能漏。** `ForceParryStun` 签名是 `(string animName = null, bool armKengeki = true)`，默认值会顺手给 Boss 挂 `KengekiArmed` → 玩家一弹反 Boss 立刻接反击，先手权反而更难拿。飞舟互弹那处已经踩过这个坑。

2. **反击计时基准必须与停顿解耦。** 现在 `counterFireTime = enterTime + parriedDurationOnHit + hitDelay - HitStartTime`，其中 `parriedDurationOnHit` 读的就是**被弹方的 `ParriedDuration`**。一旦为提高停顿把它调大，反击窗口会被同比例压缩：

   | Boss 停顿 | 反击命中点 | 玩家（0.60 恢复）剩余 |
   |---|---|---|
   | 0.35（现状）| 0.85 | 0.25s |
   | 0.5 | 1.00 | 0.40s |
   | 0.7 | 1.20 | 0.60s |

   注意 `Kengeki_Slash` 前摇只有 0.361 秒，停顿一旦超过 `0.361 + 0.5 = 0.861` 秒，`counterFireTime` 算出来会**早于停顿结束** → 反击在 Boss 还在硬直时就发起 → 停顿形同虚设。

   **建议新增独立的 `parryCounterBaseDuration`，不要复用 `ParriedDuration`。**

3. **玩家侧的 0.35 下限问题同源**（见文末"顺带记录"）：玩家的实际恢复是 0.60 而不是 0.35，反击基准一直是错的。两处应当一起定。

### 停顿时长候选

| 方案 | Boss 停顿 | 玩家（0.60 恢复）可操作时间 | 效果 |
|---|---|---|---|
| 短 | 0.35 | 0.25s | 几乎吃不到先手，维持现状 |
| **中（推荐）** | **0.5** | **0.40s** | 够走完前摇（0.23）+ 出判定，稳定先手 |
| 长 | 0.7 | 0.60s | 白给窗口，最接近"必定先手"的字面意思 |

`0.5` 同时接近 Boss 硬直动画（1.03s）的一半，视觉上不会像动画被掐断。

### 停顿结束后 Boss 能否防御

由停顿时长自动决定，**不需要额外设计**：`DeflectMemory.TryPassiveDeflect` 的守卫里 `body.IsParried` 期间直接 `return false`，所以停顿期间 Boss 只能白吃；停顿一结束它立刻恢复被动格挡。上述方案下有两个位置会检验到这一点：0.40s 和 0.60s。

用户实机复核已确认**方向**：

1. **普通平砍被弹 → 必须停顿**（不只是特殊招）。
2. **连段末尾被弹 → 也必须停顿**（用户原话："连段的末尾也会有"）。
3. 净效果 **玩家下一次出手一定比 Boss 快**。
4. **危字 / 飞舟 / JumpThrust / Jump_Danger 维持现状**（用户此前明确要保留连续弹反，这两条不冲突）。

仍待拍板的是**数值与实现边界**：

1. **停顿时长填多少？** 关键约束：必须 **≥ 玩家下一刀的前摇 0.23 秒**，否则"玩家一定先手"不成立。
   - 偏短（0.3 左右）：玩家必须够快才能吃到先手，保留技巧空间。
   - 偏长（0.5+）：稳定的白给窗口，更接近"必定先手"的字面意思。
2. **停顿结束后 Boss 能否防御玩家的下一刀？**（用户在原版没测出来。）
   注意本项目有一条**会直接改变答案**的规则：`DeflectMemory.TryPassiveDeflect` 的守卫里有
   `if (body.IsAttacking || body.IsParried || body.IsPostureBroken) return false;`
   —— **停顿期间 Boss 不格挡，等于白吃一刀**；停顿结束、`IsParried` 清了，它才恢复被动格挡。
   也就是说**答案由停顿时长决定**，不需要额外设计。
3. **要不要按"连段末段"细分？** 用户实测"连段末尾也有"，说明不需要区分逐段 —— 逐段给停顿即可。

## 必须一起想清楚的副作用

- **和被动格挡反击的时序冲突。** `DeflectState` 里 `counterFireTime = enterTime + parriedDurationOnHit + hitDelay - HitStartTime` 的基准是「被弹方恢复」。Boss 一侧的 `ParriedDuration` 一旦因为硬直改大，反击发起时刻会跟着变 —— 两个数值耦合在同一个公式里。
- **`ForceParryStun` 的默认参数会武装交锋。** 签名是 `ForceParryStun(string animName = null, bool armKengeki = true)`。直接用它给 Boss 上硬直会顺手给 Boss 挂 `KengekiArmed` → 玩家一弹反，Boss 立刻接反击，反而更吃亏。飞舟互弹那处已经踩过这个坑（显式传了 `armKengeki: false`）。
- **`ConsecutiveTimesParried` 的语义。** 现在累计的是"玩家弹开 Boss"的次数，供主动层 JumpThrust 使用。加入硬直后要确认不会和这条计数打架。

## 顺带记录：同一处的另一个已知问题

`ParriedState` 的硬直是 `max(动画, 配置下限)`，所以 `ParriedDuration` 只是下限：

- `PlayerConfig.ParriedDuration = 0.35`，但 `Deflected` 动画（`a050_120104.fbx`，0–19 帧 @30fps）实际长 0.633 秒，播到 95% 约 **0.60 秒**
- 而 `DeflectState` 的反击计时用的是**配置下限 0.35**，于是 Boss 反击命中落在 `0.35 + 0.5 = 0.85 秒`，玩家 0.60 秒恢复后只剩约 0.25 秒

两个问题在同一段代码里：**「Boss 被弹不受罚」+「玩家恢复时间被算错」。** 修先手权时应当一起处理，否则改完仍是错的基准。
