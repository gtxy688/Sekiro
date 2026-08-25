# 04 智能（M5 行为树 + M7 弦一郎 Boss AI）

> 模块：M5, M7
> 前置：M5 无依赖，M7 依赖 M5, M3
> 验收：`04-behavior-tree-ai-test.md`

## 一、行为树框架（M5）

### 黑 板（Blackboard）

节点之间共享数据的键值字典，挂在树根，所有节点共享。

```csharp
public class Blackboard
{
    private Dictionary<string, object> data = new Dictionary<string, object>();

    public void Set(string key, object value);
    public T Get<T>(string key);
    public bool Has(string key);
}
```

### Node 基类改造

```csharp
public abstract class Node
{
    protected Blackboard blackboard;
    public void SetBlackboard(Blackboard bb) { this.blackboard = bb; }

    public abstract NodeState Evaluate();
}
```

BTBrain 构建树时创建黑板，注入根节点，向下传。

### Running 记忆（Sequence/Selector）

**现状**：每次从第 0 个子节点重头遍历。
**问题**：子节点返回 Running 后，下一帧又从开头跑，浪费 + 不严谨。
**改法**：记录 `currentChildIndex`。

```csharp
public class Sequence : Node
{
    private int currentChildIndex = 0;

    public override NodeState Evaluate()
    {
        while (currentChildIndex < children.Count)
        {
            NodeState result = children[currentChildIndex].Evaluate();
            switch (result)
            {
                case NodeState.Failure:
                    currentChildIndex = 0;      // 重置，下次重跑
                    state = NodeState.Failure;
                    return state;
                case NodeState.Success:
                    currentChildIndex++;        // 下一个
                    break;
                case NodeState.Running:
                    return NodeState.Running;   // 记住 currentChildIndex，下帧继续
            }
        }
        currentChildIndex = 0;
        state = NodeState.Success;
        return state;
    }
}
```

Selector **只**对实现 `ISelectorLock` 的 Running 子节点记住索引。`BT_MoveToTarget` 不实现该接口，追击时下一帧仍从树顶重评，招架才能插进去。

### 新增 BT 节点（通用）

- `BT_MoveToTarget`（已有，改用黑板）
- `BT_Attack`（已有，改用黑板）
- `BT_UseBlackboard`：读写黑板（可选）

## 二、弦一郎 Boss AI（M7）

**参考**：`../references/sekiro-genichiro-ai.md`（只狼弦一郎 AI 逆向）。

> ⚠️ **注意**：参考文档 `sekiro-genichiro-ai.md` **不完整，有缺失部分**（如某些 Act 函数、冷却逻辑、招式参数被省略）。
> 开发 M7 时若发现文档缺失无法确定的逻辑（招式行为、触发条件、数值），**停下询问用户**，不要臆造补全。

### 核心三层结构（对照参考文档）

| 参考层 | 对应行为树 | 触发 |
|--------|-----------|------|
| 主动计划 (Goal.Activate) | 按距离选招式 | 每帧 |
| 交锋计划 (Goal.Kengeki_Activate) | 被弹开后变招 | 玩家弹反成功时 |
| 变招/防御 (Goal.Interrupt + Parry) | **被动防御判定**（命中瞬间强制格挡/计数升级弹反） | 玩家攻击命中前 |

> 招架层已从"AI 短按防御键"（旧 `BT_DeflectIf`/`BT_Deflect`，1.5s 格挡 CD）改为 **`CharacterBody.TryPassiveDeflect` 被动防御**（M7 攻防转换）：
> - Boss 非攻击/非硬直/非崩解时被玩家命中 → 命中瞬间强制转格挡判定（无 CD、无概率）；
> - 普通格挡：`Hurt_Guard`/GuardLoop 姿态 + 架势上涨（`GuardPostureFactor` 削弱架势伤害），Boss 留在防御姿态（连续防御分支）；
> - 连续格挡达 `passiveDeflectThreshold`（默认 2）次 → 下一次命中升级**强制完美弹反**：弹开玩家（`ForceParryStun`）+ 打铁火花/顿帧，抢回主动权；
> - 危字攻击（`isPerilous`）与 Boss **攻击中**（可被抓前摇）不进入被动防御，走常规受击/识破/跳踩。

### 距离分段（主动计划）

```
距离 > 7m  → 快速接近 + 砍两刀 / 射一箭
距离 5-7m  → 接近 + 横砍 + 射箭 / 飞渡符舟
距离 3-5m  → 近战连段（横二连砍/横重砍/旋转横砍/肘击）
距离 ≤ 3m  → 贴身战（横砍+转身/踢一脚）+ 跳跃下刺
```

> 当前实现已换成**完整薄树 + 招式表**。受击/崩解/忍杀结算不改。`BT_Combo` 的 AttackSet 下标连招不再作为弦一郎正路。

### 完整薄树

```
Selector:
├─ 崩解中 → Success（不选招）
├─ BT_Kengeki（KengekiArmed 且硬直结束、距离≤2.5 → 交锋表）
├─ 玩家 IsHealing → Bow_Heavy
├─ BT_PickActive（距离档加权）
└─ BT_MoveToTarget
```

Selector 只对实现 `ISelectorLock` 的 Running 子节点续跑，**不**记住 `BT_MoveToTarget`，否则追击时招架抢不到。

数据：`BossMoveTable`（`Assets/SO/Boss/GenichiroMoveTable.asset`）。出招前 `BossAttackBaker` 烤成运行时 `AttackConfig`，写入 `ActiveAttack` 再发 `AttackCommand`。

一条 Clip 要砍多刀：在对应 `windows[i].hitPulses` 填多段 `[start,end)`（见 `03-hit-detection.md`）。`Boat` 的 `Boat1` 已按 5 段占位；其它招空数组 = 仍一刀。

约定：`Slash_Spin`+`Elbow` 一行 `Slash_SpinElbow`；`Boat` ≠ `Boat_Full`；`Kengeki_Slash` 五片随机；`Kengeki_Bow` 先 3031 再二选一。缺 Animator 状态的招权重为 0。

Inspector：必须拖 `PlayerTarget` / `PlayerBody` / `moveTable`。

### 黑板数据

```
blackboard["target"]             = Player Transform
blackboard["attackRange"]        = 攻击距离
blackboard["playerPostureRatio"] = 玩家架势比 (0-1)
blackboard["consecutiveGuard"]   = 连续防御次数
blackboard["lastComboIndex"]     = 连段当前第几刀
```

### 新增 BT 节点（Boss 专用）

- `BT_ExecuteMove`：按表行多段出招
- `BT_Kengeki`：被弹后还击
- `BT_HealPunish`：喝药重箭
- `BT_PickActive`：主动层抽招
- `BT_MoveToTarget`（已有）

`BT_Combo` / `BT_HitOnce` / `BT_BowShot` / `BT_DeflectIf` / `BT_Deflect` 文件可留，树里不挂（招架职责由 `CharacterBody.TryPassiveDeflect` 承担）。

### 冷却机制

参考文档里 `SetCoolTime`。每个招式有冷却时间，防止连续放同一招。用黑板存 `Dictionary<string, float> cooldowns`，每次 Evaluate 检查。

### 阶段（本需求只做 1 阶段）

- Boss 2 条命（忍杀 2 次）
- 无脱衣/巴流阶段
- 招式表：近战连段、突刺（危字/识破）、横扫（危字/跳踩）、射箭、飞渡符舟（完整动画）

## 涉及文件

- 招式表：`Assets/Scripts/Boss/BossMoveTable.cs`、`BossMoveEntry.cs`、`BossMoveWindow.cs`、`BossAnimSequence.cs`、`BossMoveLayer.cs`、`BossMoveExtra.cs`
- 抽招 / 烘焙：`BossMovePicker.cs`、`BossAttackBaker.cs`、`GenichiroMoveCatalog.cs`
- 行为树：`BTBrain.cs`、`Selector.cs`（`ISelectorLock`）、`BT_ExecuteMove.cs`、`BT_Kengeki.cs`、`BT_DeflectIf.cs`、`BT_HealPunish.cs`、`BT_PickActive.cs`
- 武装：`CharacterBody.KengekiArmed` / `IsParried`，`ParriedState`
- 数据：`Assets/SO/Boss/GenichiroMoveTable.asset`
- Editor：`Assets/Editor/BossMoveTableEditor.cs`
