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

Selector 同理，遇到 Success 记住索引；Running 记住索引。

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
| 变招/防御 (Goal.Interrupt + Parry) | 玩家攻击时防御/招架 | 玩家攻击命中前 |

### 距离分段（主动计划）

```
距离 > 7m  → 快速接近 + 砍两刀 / 射一箭
距离 5-7m  → 接近 + 横砍 + 射箭 / 飞渡符舟
距离 3-5m  → 近战连段（横二连砍/横重砍/旋转横砍/肘击）
距离 ≤ 3m  → 贴身战（横砍+转身/踢一脚）+ 跳跃下刺
```

> 当前实现是**简单版**（追击 + 隔一会儿砍一刀 + 你挥刀时格挡），用来看手感。完整弦一郎三层（连段/射箭/飞舟/交锋变招）尚未接上。

### 简单树

```
Selector:
├─ 玩家正在攻击 且 距离 ≤ deflectRange 且 格挡冷却好了 → BT_Deflect
├─ 距离 ≤ attackRange 且 攻击冷却好了 → BT_HitOnce（一刀，等 AttackState 结束）
└─ BT_MoveToTarget（世界方向追玩家，到距离后停下并转向）
```

Inspector：`attackRange` 默认 3、`attackCooldown` 默认 2.5、`deflectRange` 默认 2.5。场景里必须拖 `PlayerTarget` / `PlayerBody`。

> 保留：Boss 有危字招式 `BT_Thrust`（突刺，识破反制）/`BT_Sweep`（横扫，跳踩反制），M7 实现时补进树（招式细节暂缓）。

### 黑板数据

```
blackboard["target"]             = Player Transform
blackboard["attackRange"]        = 攻击距离
blackboard["playerPostureRatio"] = 玩家架势比 (0-1)
blackboard["consecutiveGuard"]   = 连续防御次数
blackboard["lastComboIndex"]     = 连段当前第几刀
```

### 新增 BT 节点（Boss 专用）

- `BT_Combo`：近战连段，内部维护第几刀，每刀间隔 0.3-0.5s
- `BT_BowShot`：后跳射箭
- `BT_Deflect`：玩家连续攻击时招架
- `BT_MoveToTarget`（已有）

> 保留：`BT_Thrust`（突刺危字）/`BT_Sweep`（横扫危字）。

### 冷却机制

参考文档里 `SetCoolTime`。每个招式有冷却时间，防止连续放同一招。用黑板存 `Dictionary<string, float> cooldowns`，每次 Evaluate 检查。

### 阶段（本需求只做 1 阶段）

- Boss 2 条命（忍杀 2 次）
- 无脱衣/巴流阶段
- 招式表：近战连段、突刺（危字/识破）、横扫（危字/跳踩）、射箭、飞渡符舟（完整动画）

## 涉及文件

- 新建：`Assets/Scripts/Boss/BehaviourTree/Blackboard.cs`
- 修改：`Assets/Scripts/Boss/BehaviourTree/Node.cs`（SetBlackboard）
- 修改：`Assets/Scripts/Boss/BehaviourTree/Sequence.cs`（Running 记忆）
- 修改：`Assets/Scripts/Boss/BehaviourTree/Selector.cs`（Running 记忆）
- 修改：`Assets/Scripts/Boss/BehaviourTree/BT/BT_MoveToTarget.cs`（黑板）
- 修改：`Assets/Scripts/Boss/BehaviourTree/BT/BT_Attack.cs`（黑板）
- 新建：`Assets/Scripts/Boss/BehaviourTree/BT/BT_HitOnce.cs`
- 新建：`Assets/Scripts/Boss/BehaviourTree/BT/BT_BowShot.cs`
- 新建：`Assets/Scripts/Boss/BehaviourTree/BT/BT_Deflect.cs`
- 修改：`Assets/Scripts/Boss/BTBrain.cs`（黑板注入 + 完整树）
