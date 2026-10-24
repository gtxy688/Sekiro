## 1. 行为树与 HFSM 的分工及每帧驱动调用链

### 1. 架构分工：为什么有了 HFSM 还要行为树？
在《只狼》Boss 战中，Boss 与玩家共用同一套底层角色门面 `CharacterBody` 和分层状态机（HFSM），但两者的“大脑（Brain）”完全不同：
- **行为树（`BTBrain`）负责“宏观战术决策与多段连招编排”**：决定当前应该追击绕圈、交锋反击、喝药惩罚还是主动抽招；选中一套多段连招（`BossMoveEntry`）后，负责按段序（`segment = 0, 1, 2...`）一段一段往下推进，并在招式结束后写入冷却与强制走位间隔。
- **分层状态机（`HFSM`）负责“微观单段动作的物理与动画执行”**：行为树不直接操作 `Animator` 或开关 `Hitbox`，而是把当前这一段动作烘焙成一份标准的 `AttackConfig`，通过 `body.StartAttack` 或 `body.TryChangeGroundedSubState` 交给 HFSM 的 `AttackState` 执行。单段动作怎么播动画、哪几帧开 `Hitbox`、挥刀中途被玩家完美弹反怎么切进 `ParriedState`，全部复用 HFSM 现有逻辑。

```text
【Boss 决策层 (BTBrain) 与 执行层 (HFSM) 的交接闭环】
BTBrain (行为树大脑)
  │
  ├─ 1. 选招：BossMovePicker 从 BossMoveTable 筛选出一条多段招式 BossMoveEntry
  ├─ 2. 烘焙：BT_ExecuteMove 取当前段 (segment)，经 BossAttackBaker.Bake 转为临时 AttackConfig
  └─ 3. 派发：调用 body.StartAttack(baked) 或 body.TryChangeGroundedSubState(...)
        │
        ▼
CharacterBody / HFSM (角色状态机)
  │
  ├─ 4. 执行单段：进入 AttackState，由 AttackAnimClock 推进动画、ApplyHitbox 开关武器判定
  ├─ 5. 状态反馈：每帧向行为树暴露 body.IsAttacking / body.IsParried / body.IsPostureBroken
  └─ 6. 单段结束：AttackState 播完退回 IdleState (body.IsAttacking 变回 false)
        │
        ▼
BT_ExecuteMove (行为树执行器感知单段结束)
  │
  ├─ 若还有下一段 (segment + 1 < states.Length) ──► 立即烘焙并派发下一段 AttackConfig
  └─ 若全部段播完 (segment + 1 >= states.Length) ──► 写入 Blackboard 冷却，开启招间走位间隔
```

---

### 2. 行为树节点结构与每帧驱动调用链
`BTBrain` 直接继承自 `MonoBehaviour`（不走玩家 `BrainBase` 的 0.2s 单槽预输入缓冲池）。在 `BTBrain.Start()` 中，`ConstructBehaviorTree()` 构建出一棵以 `Selector` 为根节点的五层优先级行为树，并在 `BTBrain.Update()` 中每帧驱动评估：

```text
Unity Engine 每帧驱动
  └─► BTBrain.Update()
        ├─ 1. 战败拦截：if (body.IsDefeated) ──► ResetExecutors()，清零移动方向并 return
        ├─ 2. 硬直/处决拦截：if (body.IsFinisherLocked || body.IsParried)
        │       └─► ResetExecutors() 并 return（暂停整棵树，清空未播完的多段连招进度）
        ├─ 3. 玩家倒地/回生/开场语音拦截：重置执行器，只调 moveToTarget.Evaluate() 绕圈或后撤
        ├─ 4. 喝药预检测：healPunish?.ArmIfPlayerHealing()
        │       └─► 若玩家正在喝药且 Boss 未在出招，预先记录惩罚时间戳 punishAt
        ├─ 5. 驱动行为树根节点：behaviorTreeRoot.Evaluate()  [根节点为 Selector]
        │       │
        │       ├─► [优先续跑] 若上一帧有实现 ISelectorLock 的子节点处于 Running (runningChildIndex >= 0)
        │       │     └─► 直接调用 children[runningChildIndex].Evaluate() 续跑该节点
        │       │
        │       └─► [按序评估] 从 0 号子节点向后依次调用 Evaluate()，遇 Running 或 Success 即停止：
        │             ├─ 子节点 0: ConditionNode (IsPostureBroken || IsFinisherLocked || IsParried)
        │             │     └─► 若处于失衡/处决/被弹硬直，返回 Success 吃掉本帧，防止落到走位
        │             ├─ 子节点 1: BT_Kengeki (近身交锋反击，实现 ISelectorLock，持有 kengekiExecutor)
        │             │     └─► 消费 body.KengekiArmed 标记，抽 Kengeki 层招式交 kengekiExecutor 执行
        │             ├─ 子节点 2: BT_HealPunish (喝药重箭惩罚，实现 ISelectorLock，持有 interruptExecutor)
        │             │     └─► 消费 punishAt 计时，到点后取 "Bow_Heavy" 交 interruptExecutor 执行
        │             ├─ 子节点 3: BT_PickActive (常规主动选招，实现 ISelectorLock，持有 activeExecutor)
        │             │     └─► 检查 active_gap 走位间隔 ──► BossMovePicker.Pick 抽 Active 层招式交 activeExecutor 执行
        │             └─ 子节点 4: BT_MoveToTarget (兜底追击与绕圈走位，不实现 ISelectorLock)
        │                   └─► 远距离快走追击，近距离绕圈走位，提交 MoveCommand
        │
        └─ 6. 起手等待期补跑走位：if (任意 Executor.IsWaitingForStart)
                └─► moveToTarget.Evaluate()（已选好招但等玩家弹反反击保护窗到期时，边走位边等）
```

---

### 3. `BTBrain` 组装与驱动源码
`BTBrain` 为交锋反击（`BT_Kengeki`）、喝药惩罚（`BT_HealPunish`）和常规主动出招（`BT_PickActive`）分别配备了三个独立的 `BT_ExecuteMove` 实例（`kengekiExecutor`、`interruptExecutor`、`activeExecutor`），互不干扰各自的段进度：

```csharp
// BTBrain.cs
// 优先级：禁止行动 → 交锋 → 喝药重箭 → 主动选招 → 走位；被动防御由受击流程处理。
private Node ConstructBehaviorTree()
{
    activeExecutor = new BT_ExecuteMove(body, moveTable);
    kengekiExecutor = new BT_ExecuteMove(body, moveTable);
    interruptExecutor = new BT_ExecuteMove(body, moveTable);

    List<Node> children = new List<Node>
    {
        // 崩解 / 忍杀锁定 / 弹反·识破硬直：Success 吃掉本帧，不落到走位。
        new ConditionNode(() => body.IsPostureBroken || body.IsFinisherLocked || body.IsParried)
    };

    if (!DisableBossAttacks)
    {
        children.Add(new BT_Kengeki(
            body, moveTable, PlayerTarget, kengekiExecutor, PlayerBody, moveFilter));
        // 喝药惩罚直接查找重箭，不经过白名单，因此纯近战调试时不挂此节点。
        if (!MeleeOnly)
        {
            healPunish = new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor);
            children.Add(healPunish);
        }
        children.Add(new BT_PickActive(
            body, moveTable, PlayerTarget, activeExecutor, PlayerBody, moveFilter,
            roamAfterAttack, roamAfterAttackJitter));
    }

    moveToTarget = new BT_MoveToTarget(body, PlayerTarget, attackRange,
        roamStrafeDuration, roamStrafeStrength, roamApproachStrength);
    children.Add(moveToTarget);

    return new Selector(children);
}
```

在 `BTBrain.Update()` 中，当 Boss 被玩家完美弹反（`body.IsParried`）或进入忍杀锁定时，除了跳过行为树评估，还会立即调用 `ResetExecutors()` 清空三个执行器的多段招式进度，防止硬直结束后接着播放被打断招式的剩余段：

```csharp
// BTBrain.cs
// 特殊战斗阶段优先拦截，再评估行为树；等待起手时允许单独走位。
private void Update()
{
    if (PlayerTarget == null) return;

    if (body.IsDefeated)
    {
        ResetExecutors();
        body.MoveDirection = Vector3.zero;
        body.PreferFastWalk = false;
        return;
    }

    // 忍杀与硬直期间暂停整棵树，避免出招失败后落到走位并转向玩家。
    if (body.IsFinisherLocked || body.IsParried)
    {
        // 树不跑时也要把 Busy 执行器清掉，否则硬直结束会接着播被打断招的下一段。
        ResetExecutors();
        return;
    }

    // 玩家刚倒地时中止当前攻击，之后只走位，直到回生流程允许恢复进攻。
    bool playerIncapacitated = PlayerBody.IsIncapacitatedForBoss;
    if (playerIncapacitated && !circlingIncapacitatedPlayer)
    {
        circlingIncapacitatedPlayer = true;
        ResetExecutors();
        if (body.IsAttacking)
            body.CancelAttackToIdle();
    }
    else if (!playerIncapacitated)
    {
        circlingIncapacitatedPlayer = false;
    }

    if (postRevivePhase != PostRevivePhase.None)
    {
        UpdatePostRevivePhase();
        return;
    }

    if (playerIncapacitated)
    {
        ResetExecutors();
        moveToTarget.Evaluate();
        return;
    }

    // 开场语音：可以走位，但不要出招。
    if (IsOpeningHold())
    {
        ResetExecutors();
        if (body.IsAttacking)
            body.CancelAttackToIdle();
        moveToTarget.Evaluate();
        return;
    }

    healPunish?.ArmIfPlayerHealing();
    behaviorTreeRoot.Evaluate();
    // 已选好招但尚未到允许起手时刻，保留走位；被动格挡仍由 ReceiveHit 独立处理。
    if (activeExecutor.IsWaitingForStart
        || kengekiExecutor.IsWaitingForStart
        || interruptExecutor.IsWaitingForStart)
        moveToTarget.Evaluate();
}
```

---

## 2. 行为树底座：`Node`、`Selector`、`ISelectorLock` 与 `Blackboard`

### 1. 调用链
```text
BTBrain.Update()
  └─► Selector.Evaluate()
        ├─ 若 runningChildIndex >= 0（上一帧有实现 ISelectorLock 的子节点返回 Running）
        │    └─► children[runningChildIndex].Evaluate()
        │          ├─ 仍返回 Running ──► 直接返回 Running，本帧结束
        │          └─ 返回 Success / Failure ──► runningChildIndex = -1，继续向下重跑
        │
        └─ 从 i = 0 到 children.Count - 1 依次调用 children[i].Evaluate()
             ├─ 返回 Failure ──► continue 试下一个子节点
             ├─ 返回 Success ──► runningChildIndex = -1，返回 Success
             └─ 返回 Running ──► runningChildIndex = (children[i] is ISelectorLock ? i : -1)，返回 Running
```

---

### 2. 节点基类 `Node` 与三态枚举 `NodeState`
行为树所有节点均继承自抽象基类 `Node`。每次调用 `Evaluate()` 返回三种状态之一：
- `NodeState.Running`：当前任务正在执行中（例如多段连招还没播完、或正在走位），下一帧需继续；
- `NodeState.Success`：当前任务已完成；
- `NodeState.Failure`：前置条件不满足或任务被打断，交由父节点 `Selector` 尝试下一个低优先级分支。

```csharp
// Node.cs
// 节点评估结果，供复合节点决定续跑、推进或尝试下一分支。
public enum NodeState
{
    Running, // 运行中：任务还没执行完，下一帧还要继续
    Success, // 当前任务完成
    Failure  // 条件不满足或任务失败
}

// 行为树节点基类，统一评估接口、子节点和黑板传递。
public abstract class Node
{
    protected NodeState state;
    public NodeState State => state;

    // 如果是复合节点（比如 Sequence/Selector），它会有子节点
    protected List<Node> children = new List<Node>();

    // 构建树时由根节点向下传递的实例级黑板。
    protected Blackboard blackboard;

    public Node() { }

    public Node(List<Node> children)
    {
        this.children = children;
    }

    // 递归注入子节点；另持有执行器的节点需重写并继续传递黑板。
    public virtual void SetBlackboard(Blackboard bb)
    {
        blackboard = bb;
        foreach (Node child in children)
        {
            child.SetBlackboard(bb);
        }
    }

    public abstract NodeState Evaluate();
}
```

---

### 3. `Selector` 与 `ISelectorLock`：为什么出招要锁选择器，而走位不能锁？
在动作游戏 AI 中，`Selector`（优先选择器）面临一个核心矛盾：
1. **出招节点不能被同级或高优先级节点中途打断**：例如 `BT_PickActive` 抽中了一套三连砍并进入 `Running`，如果下一帧 `Selector` 又从头评估，可能会在连招段间空隙触发别的分支把连招掐断；因此出招节点在 `Running` 期间必须**锁住 `Selector`**，下一帧直接续跑自己。
2. **走位节点绝对不能锁住 `Selector`**：兜底的 `BT_MoveToTarget` 只要在追击或绕圈，每一帧都会返回 `NodeState.Running`。如果它也锁住 `Selector`，那么一旦进入走位，`Selector` 就会永远只续跑走位节点，上方的交锋反击、喝药惩罚和主动选招永远没机会再被评估。

为此，项目定义了空标记接口 `ISelectorLock`：

```csharp
// ISelectorLock.cs
// Selector 只对实现此接口的 Running 子节点续跑。追击节点不要实现。
public interface ISelectorLock { }
```

只有 `BT_Kengeki`、`BT_HealPunish`、`BT_PickActive`（以及 `BT_ExecuteMove`）实现了 `ISelectorLock`，而 `BT_MoveToTarget` 不实现。`Selector.Evaluate()` 在第 38 行通过 `children[i] is ISelectorLock ? i : -1` 决定是否记录 `runningChildIndex`：

```csharp
// Selector.cs
// 按优先级尝试子节点；只有声明锁定的运行中节点会在下一帧优先续跑。
public class Selector : Node
{
    // 走位节点不锁定，保证每帧都能被更高优先级的出招节点抢占。
    private int runningChildIndex = -1;

    public Selector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        if (runningChildIndex >= 0)
        {
            NodeState resume = children[runningChildIndex].Evaluate();
            if (resume == NodeState.Running)
            {
                state = NodeState.Running;
                return state;
            }
            runningChildIndex = -1;
        }

        for (int i = 0; i < children.Count; i++)
        {
            switch (children[i].Evaluate())
            {
                case NodeState.Failure:
                    continue;
                case NodeState.Success:
                    runningChildIndex = -1;
                    state = NodeState.Success;
                    return state;
                case NodeState.Running:
                    runningChildIndex = children[i] is ISelectorLock ? i : -1;
                    state = NodeState.Running;
                    return state;
            }
        }

        runningChildIndex = -1;
        state = NodeState.Failure;
        return state;
    }
}
```

| 子节点类型 | 是否实现 `ISelectorLock` | 返回 `Running` 时 `runningChildIndex` 的值 | 下一帧 `Selector.Evaluate()` 的行为 |
| :--- | :--- | :--- | :--- |
| `BT_Kengeki` / `BT_HealPunish` / `BT_PickActive` | **是** | 记录当前节点下标 `i` | 直接跳到 `children[i].Evaluate()` 续跑，直到整套招式播完或被打断返回非 `Running` |
| `BT_MoveToTarget`（追击/绕圈走位） | **否** | 保持 `-1` | 下一帧仍从 `i = 0` 开始按优先级评估，一旦冷却到期或触发交锋/喝药立即抢占走位 |

---

### 4. `Blackboard`：实例级黑板与静态招式表隔离
所有招式的静态参数（距离、权重、冷却时长、动画序列）存放在 ScriptableObject 资产 `BossMoveTable` 中；而每场战斗运行时的**各招式上次释放时间戳（`cooldowns`）**与**招间强制走位间隔时长（`ActiveGapDuration`）**则保存在 `BTBrain` 创建的 `Blackboard` 实例中：

```csharp
// Blackboard.cs
// 单个 Boss 的行为树共享数据；冷却直接用招式 ID 和 float 时间戳，避免逐帧装箱或拼接键。
public class Blackboard
{
    // 招式冷却：key = 招式 id（来自 SO），value = 上次执行的时间戳
    private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

    // 出招后强制走位间隔的时长（ArmRoamGap 写入，IsRoamGapActive 读取）
    public float ActiveGapDuration { get; set; }

    // 从未执行过的招式应当立即可用，不能把缺失值 0 当成开局时间戳。
    public bool IsOnCooldown(string moveId, float cooldown)
    {
        return cooldowns.TryGetValue(moveId, out float last)
            && UnityEngine.Time.time - last < cooldown;
    }

    public void SetCooldown(string moveId)
    {
        cooldowns[moveId] = UnityEngine.Time.time;
    }

    // 复战时清空现有实例，保留所有节点持有的黑板引用。
    public void Clear()
    {
        cooldowns.Clear();
        ActiveGapDuration = 0f;
    }
}
```

注意 `IsOnCooldown` 的边界处理：如果某招从开局到现在从未放过，`cooldowns.TryGetValue` 返回 `false`，直接判定为**不在冷却中**；如果直接用默认 `0f` 当作上次释放时间戳，开局前几秒（`Time.time < cooldown`）所有长冷却招式都会被误判为冷却中。

---

## 3. 候选招式筛选：`BT_PickActive` 与 `BossMovePicker`

### 1. 调用链
```text
Selector.Evaluate()
  └─► BT_PickActive.Evaluate()
        ├─ 1. 目标失能或自身失衡/被弹硬直 ──► executor.ResetMove()，返回 Failure
        ├─ 2. 招式正在执行中 (executor.IsBusy == true)
        │      └─► busy = executor.Evaluate()
        │            ├─ 若 busy != Running（招式本帧刚播完或失败）──► ArmRoamGap() 写入 "active_gap" 冷却
        │            └─ 返回 busy
        ├─ 3. 检查招间强制走位间隔：if (IsRoamGapActive()) ──► 返回 Failure（交由下层 BT_MoveToTarget 走位）
        ├─ 4. 筛选招式：BossMovePicker.Pick(table, BossMoveLayer.Active, body, Animator, blackboard, dist, ...)
        │      ├─ Pass 0: 遍历 table.moves，调用 Weight(...) 计算有效权重并累加 total
        │      └─ Pass 1: roll = Random.Range(0f, total)，再次遍历累加 cursor，返回首个 roll <= cursor 的招式
        └─ 5. 派发执行：executor.Begin(move)
```

---

### 2. `BT_PickActive`：主动选招与招间强制走位间隔（`active_gap`）
如果 Boss 每打完一套连招的下一帧立刻又进 `BossMovePicker.Pick` 抽下一招，Boss 就会毫无停顿地無限连招。因此 `BT_PickActive` 在 `executor.Evaluate()` 刚结束（`busy != NodeState.Running`）时，立即调用 `ArmRoamGap()` 在黑板里注册一个名为 `"active_gap"` 的冷却（时长为 `roamAfterAttack + Random(0, roamAfterAttackJitter)`，默认 `2.5s ~ 3.7s`）。在此期间 `IsRoamGapActive()` 返回 `true`，`BT_PickActive` 直接返回 `Failure`，让行为树落入最底层的 `BT_MoveToTarget` 绕着玩家走位对峙：

```csharp
// BT_PickActive.cs
// 主动层：按距离加权抽招并播完。Busy 时续跑，不因距离微调取消。
public class BT_PickActive : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;
    private readonly Transform target;
    private readonly BT_ExecuteMove executor;
    private readonly CharacterBody opponent;
    private readonly HashSet<string> whitelist;
    private readonly float roamAfterAttack;
    private readonly float roamAfterAttackJitter;

    ...

    public override NodeState Evaluate()
    {
        if (BTUtil.IsTargetIncapacitated(target))
        {
            executor.ResetMove();
            return NodeState.Failure;
        }

        // 先清除硬直中被打断的执行器，避免沿用未完成招式的进度。
        if (body.IsPostureBroken || body.IsParried)
        {
            executor.ResetMove();
            return NodeState.Failure;
        }
        if (executor.IsBusy)
        {
            NodeState busy = executor.Evaluate();
            if (busy != NodeState.Running)
                ArmRoamGap();
            return busy;
        }

        bool isRoamGapActive = IsRoamGapActive();
        if (isRoamGapActive)
            return NodeState.Failure;

        float dist = Vector3.Distance(body.transform.position, target.position);
        // 距离范围由招式表过滤；没有可用招式时返回失败，让低优先级走位接管。
        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Active, body, body.Animator, blackboard, dist, opponent, whitelist);
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }

    // 主动招执行结束后留下随机走位间隔，避免每次选招都紧接上一招。
    private void ArmRoamGap()
    {
        float gap = roamAfterAttack + UnityEngine.Random.Range(0f, Mathf.Max(0f, roamAfterAttackJitter));
        blackboard.ActiveGapDuration = gap;
        blackboard.SetCooldown("active_gap");
    }

    private bool IsRoamGapActive()
    {
        float gap = blackboard.ActiveGapDuration;
        if (gap <= 0.01f) gap = roamAfterAttack;
        return blackboard.IsOnCooldown("active_gap", gap);
    }
}
```

---

### 3. `BossMoveEntry` 与 `BossMovePicker`：五道过滤与两遍加权抽样
招式表 `BossMoveTable` 中的每一行是一个 `BossMoveEntry`，包含选招条件（层级、距离区间、权重、冷却、额外条件）与出招数据（动画序列 `sequences` 与段窗口 `windows`）：

```csharp
// BossMoveEntry.cs
// 招式表的一行：选招用距离/权重/冷却/层，出招用动画名和窗口。
[Serializable]
public class BossMoveEntry
{
    public string id;
    public BossMoveLayer layer;          // Active(主动) / Kengeki(交锋) / Interrupt(打断)
    public BossAnimSequence[] sequences; // 可选动画序列（如同一招有多个变体分支）
    public BossMoveWindow[] windows;     // 各段动画的判定窗口与时长配置
    public int baseDamage = 10;
    public float postureDamage = 15f;
    public float knockback = 0f;
    public HitGrade hitGrade = HitGrade.Light;
    public PerilousType perilous = PerilousType.None;
    public float minRange = 0f;          // 最小释放距离（仅 Active 层生效）
    public float maxRange = 99f;         // 最大释放距离（仅 Active 层生效）
    public float weight = 10f;           // 抽样权重
    public float cooldown = 4f;          // 招式冷却秒数
    public BossMoveExtra extra = BossMoveExtra.None; // 额外触发条件
}
```

`BossMovePicker.Weight` 对每条招式依次执行 **5 道过滤**，任一条件不满足即返回权重 `0f`：
1. **层级与基础权重**：`e.layer == layer && e.weight > 0f`；
2. **距离区间（仅 `Active` 主动层）**：`distance >= e.minRange && distance <= e.maxRange`（交锋层由 `BT_Kengeki` 统一用 `table.kengekiMaxRange` 把关）；
3. **黑板冷却**：`!blackboard.IsOnCooldown(e.id, e.cooldown)`；
4. **动画完整性**：`AnySequencePlayable(e, animator)`（检查 `Animator` 中是否存在该招式序列的所有动画状态名）；
5. **额外状态条件（`BossMoveExtra`）**：
   - `HpBelow75`：Boss 当前血量 `< 75%` 最大生命值；
   - `PostureLow`：Boss 当前累计架势值达到阈值（默认半管架势）；
   - `ConsecutiveParry2`：Boss 连续被玩家完美弹开次数 `self.ConsecutiveTimesParried >= 2` 且玩家未倒地（如触发跳劈突刺 `JumpThrust`）；
   - `PlayerKnockedDown`：玩家正处于倒地状态（`opponent.IsKnockedDown`）。

通过过滤后，`BossMovePicker.Pick` 用 `for (int pass = 0; pass < 2; pass++)` 两遍循环完成加权随机抽样（无需分配临时 `List` 存储候选招式，0 GC）：
- **第 0 遍（`pass == 0`）**：累加所有有效候选招式的权重得到总和 `total`；若 `total <= 0f` 说明当前没有任何可用招式，直接返回 `null`；
- **第 1 遍（`pass == 1`）**：在 `[0, total)` 之间生成随机数 `roll`，按完全相同的顺序再次累加有效权重到游标 `cursor`，当首次满足 `roll <= cursor` 时即命中该招式并返回。

```csharp
// BossMovePicker.cs
// 按层加权抽招。缺 Animator 状态、冷却中、距离/额外条件不满足 → 权重 0。
public static class BossMovePicker
{
    public static BossMoveEntry Pick(
        BossMoveTable table,
        BossMoveLayer layer,
        CharacterBody self,
        Animator animator,
        Blackboard blackboard,
        float distance,
        CharacterBody opponent = null,
        HashSet<string> whitelist = null)
    {
        if (table.moves == null) return null;

        float postureLow = table.postureLowThreshold > 0f
            ? table.postureLowThreshold
            : self.Config.MaxPosture * 0.5f;

        // 第一遍累计有效权重，第二遍沿同一候选顺序定位随机落点。
        float total = 0f;
        for (int pass = 0; pass < 2; pass++)
        {
            float cursor = 0f;
            float roll = pass == 1 ? Random.Range(0f, total) : 0f;
            for (int i = 0; i < table.moves.Length; i++)
            {
                BossMoveEntry e = table.moves[i];
                if (whitelist != null && whitelist.Count > 0 && !whitelist.Contains(e.id)) continue;
                float w = Weight(e, layer, self, opponent, animator, blackboard, distance, postureLow);
                if (w <= 0f) continue;
                if (pass == 0)
                {
                    total += w;
                }
                else
                {
                    cursor += w;
                    if (roll <= cursor) return table.moves[i];
                }
            }
            if (pass == 0 && total <= 0f) return null;
        }

        return null;
    }

    // 不满足层、冷却、动画或额外条件时返回零；只有主动层按条目距离过滤。
    public static float Weight(
        BossMoveEntry e,
        BossMoveLayer layer,
        CharacterBody self,
        CharacterBody opponent,
        Animator animator,
        Blackboard blackboard,
        float distance,
        float postureLow)
    {
        if (e == null || e.layer != layer || e.weight <= 0f) return 0f;
        if (layer == BossMoveLayer.Active)
        {
            if (distance < e.minRange || distance > e.maxRange) return 0f;
        }

        if (blackboard.IsOnCooldown(e.id, e.cooldown)) return 0f;
        if (!AnySequencePlayable(e, animator)) return 0f;

        switch (e.extra)
        {
            case BossMoveExtra.HpBelow75:
                if (self.CurrentHP >= self.Config.MaxHP * 0.75f)
                    return 0f;
                break;
            case BossMoveExtra.PostureLow:
                // PostureLow 是历史命名；实际要求累计架势达到阈值才放行。
                if (self.CurrentPosture < postureLow)
                    return 0f;
                break;
            case BossMoveExtra.ConsecutiveParry2:
            {
                // 贴身交锋过多才跳：连续被对手完美弹开未满 2 次，权重为 0。
                if (opponent != null && opponent.IsKnockedDown)
                    return 0f;
                if (self.ConsecutiveTimesParried < 2)
                    return 0f;
                break;
            }
            case BossMoveExtra.PlayerKnockedDown:
            {
                if (opponent == null || !opponent.IsKnockedDown)
                    return 0f;
                break;
            }
        }

        return e.weight;
    }
    ...
}
```

---

## 4. 多段招式推进与烘焙：`BT_ExecuteMove` 与 `BossAttackBaker`

### 1. 调用链
```text
【阶段 A：抽中招式首帧起手】
BT_PickActive / BT_Kengeki / BT_HealPunish
  └─► BT_ExecuteMove.Begin(move, interruptCurrent)
        ├─ ResetMove() 清空旧状态
        ├─ sequence = BossMovePicker.ChooseSequence(move, ...) 选出一条可用动画序列 states[]
        ├─ started = true, segment = 0
        └─► FireCurrentSegment()
              ├─ 1. 首段起手保护检查 (segment == 0 && body.AttackHitNotBeforeTime > 0f)
              │      └─► 若当前时间早于允许起手时刻 earliestStart，设 waitingForStart = true，返回 Running
              ├─ 2. 烘焙当前段配置：baked = BossAttackBaker.Bake(entry, anim, w)
              ├─ 3. 写入角色当前攻击槽：body.ActiveAttack = baked
              ├─ 4. 派发给 HFSM 状态机：
              │      ├─ 若 segment > 0（第二段起）：直接调 body.TryChangeGroundedSubState(g => new AttackState(body, g, baked))
              │      └─ 若 segment == 0（首段）：调 body.StartAttack(baked, allowInterrupt)
              │            └─► TryExecuteCommand(new AttackCommand()) ──► IdleState / MoveState 切入 AttackState
              └─ 5. 标记 waitingAttack = true，返回 Running

【阶段 B：后续帧轮询与段间推进】
BT_PickActive / BT_Kengeki / BT_HealPunish (因 executor.IsBusy == true)
  └─► BT_ExecuteMove.Evaluate()
        ├─ 1. 若 body.IsParried || body.IsPostureBroken || body.IsFinisherLocked
        │      └─► ResetMove()，返回 Failure（招式被弹反或打崩中断）
        ├─ 2. 若 waitingAttack == true
        │      ├─ if (body.IsAttacking || body.IsParryCounterPending) ──► 仍在播当前段，返回 Running
        │      └─ 当前段已播完（HFSM 已退回 IdleState，body.IsAttacking == false）：
        │            ├─ waitingAttack = false, segment++
        │            ├─ TryInterruptAir5()（五连箭后撤段结束后按概率变招重箭）
        │            ├─ 若 segment >= sequence.states.Length（整套连招全部播完）：
        │            │     └─► blackboard.SetCooldown(entry.id) ──► ResetMove() ──► 返回 Success
        │            └─ 若还有下一段（segment < sequence.states.Length）：
        │                  └─► FireCurrentSegment() 立即烘焙并切入下一段 AttackState，返回 Running
```

---

### 2. `BT_ExecuteMove` 核心源码与三个关键机制
在阅读 `BT_ExecuteMove` 源码时，重点关注以下三处控制逻辑：
1. **玩家弹反后的反击保护窗（`AttackHitNotBeforeTime`）**：当玩家主动完美弹反打断 Boss 连招末刀时，`DeflectState` 会调用 `boss.ArmPlayerParryAttackWindow(player.LightAttack)`，在 Boss 身上设定 `AttackHitNotBeforeTime`（预留出玩家回砍一刀的命中时刻）。当 Boss 在硬直结束后立刻抽出下一招准备起手时，`FireCurrentSegment`（第 139～147 行）会用 `AttackHitNotBeforeTime - firstCue` 倒推出允许起手的最早时刻 `earliestStart`。在此之前，`waitingForStart = true` 并返回 `Running`（由 `BTBrain.Update` 驱动 Boss 绕圈走位等待），保证玩家完美弹反后必有一刀安全的反击窗口，不会被 Boss 瞬发快刀抢先打断。
2. **首段与后续段的双通道派发（第 168～174 行）**：
   - **首段（`segment == 0`）**：调用 `body.StartAttack(baked, allowInterrupt)`，内部把 `baked` 存入 `body.ActiveAttack` 后发送 `new AttackCommand()`，走 HFSM 标准命令路由从 `IdleState` 或 `MoveState` 切入 `AttackState`。
   - **第二段起（`segment > 0`）**：当前一段 `AttackState` 播完退出到 `IdleState` 的同一帧，`BT_ExecuteMove.Evaluate()` 检测到 `body.IsAttacking == false`，立即 `segment++` 并调用 `FireCurrentSegment()`。此时直接调用 `body.TryChangeGroundedSubState(g => new AttackState(body, g, baked))` 强切进下一段 `AttackState`，省去中间在 `IdleState` 停留及多余的动画过渡。
3. **五连箭中途变招（`TryInterruptAir5`）**：当弦一郎释放 `Bow_Air5` 或 `Kengeki_Air5` 的第 0 段（后撤步）刚结束进入 `segment = 1` 时，按 `table.air5HeavyInterruptChance`（50% 概率）检查重箭 `"Bow_Heavy"` 是否不在冷却中；若命中，直接把当前执行的 `entry` 和 `sequence` 替换为 `"Bow_Heavy"` 并重置 `segment = 0`，实现后撤步接蓄力重箭的派生变招。

```csharp
// BT_ExecuteMove.cs
// 按表行顺序播完各段动画。五连垫步结束后有概率改打重箭。
public class BT_ExecuteMove : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly BossMoveTable table;

    private BossMoveEntry entry;
    private BossAnimSequence sequence;
    private int segment;
    private bool started;
    private bool waitingAttack;
    private bool allowInterrupt;
    private bool jumpThrustCameraActive;
    private float jumpThrustBossBaseY;
    private bool waitingForStart;
    // 首段已计算过起手约束时，还需等普通格挡退出；此标记参与派发条件。
    private bool scheduleLogged;

    public bool IsBusy => started;
    public bool IsWaitingForStart => started && waitingForStart;

    ...

    // 选定一条可播放的动画分支，再尝试派发首段；等待起手也算执行中。
    public NodeState Begin(BossMoveEntry move, bool interruptCurrent = false)
    {
        ResetMove();
        if (move == null) return NodeState.Failure;
        sequence = BossMovePicker.ChooseSequence(move, body.Animator, body, table);
        if (sequence == null) return NodeState.Failure;
        entry = move;
        started = true;
        segment = 0;
        allowInterrupt = interruptCurrent;
        return FireCurrentSegment();
    }

    public override NodeState Evaluate()
    {
        if (!started) return NodeState.Failure;

        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            ResetMove();
            return NodeState.Failure;
        }

        // 等待期间 Boss 弹回玩家时，交还交锋层，不继续上次预选的主动招。
        if (waitingForStart && body.KengekiArmed)
        {
            ResetMove();
            return NodeState.Failure;
        }

        if (waitingAttack)
        {
            // 反击已接收但尚在弹反态等起手，也算本段未完成。
            if (body.IsAttacking || body.IsParryCounterPending) return NodeState.Running;
            waitingAttack = false;
            segment++;
            TryInterruptAir5();
            // 起跳段结束时关闭跟随镜头，落地突刺沿用普通战斗镜头。
            if (jumpThrustCameraActive && segment > 0)
                ReleaseJumpThrustCamera();
            if (segment >= sequence.states.Length)
            {
                blackboard.SetCooldown(entry.id);
                ResetMove();
                return NodeState.Success;
            }
            return FireCurrentSegment();
        }

        return FireCurrentSegment();
    }

    // 先等待首段起手条件，再烘焙配置交给角色攻击状态执行。
    private NodeState FireCurrentSegment()
    {
        string anim = sequence.states[segment];
        BossMoveWindow w = BossMovePicker.WindowFor(entry, segment, sequence);
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            ResetMove();
            return NodeState.Failure;
        }

        if (segment == 0 && body.AttackHitNotBeforeTime > 0f)
        {
            // 从首个真实近战判定/出箭点倒推起手，不使用 NoHit 段的占位 HitStartTime。
            float firstCue = FirstOffensiveCueTime(w);
            float earliestStart = body.AttackHitNotBeforeTime - firstCue;
            waitingForStart = Time.time < earliestStart;
            scheduleLogged = true;
            if (waitingForStart) return NodeState.Running;
        }
        // 普通格挡中 StartAttack 会吞指令却不出招；等防御态退出再派发。
        if (segment == 0 && scheduleLogged && body.IsGuarding)
        {
            waitingForStart = true;
            return NodeState.Running;
        }
        waitingForStart = false;

        body.CurrentMoveEntry = entry;
        body.CurrentMoveWindow = w;
        AttackConfig baked = BossAttackBaker.Bake(entry, anim, w);
        baked.RotationSpeed = body.Config.RotationSpeed;
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            ResetMove();
            return NodeState.Failure;
        }
        body.ActiveAttack = baked;
        // 第二段起直接切 AttackState，避免先进 Idle 再 CrossFade 导致跨子状态机读不到动画时间。
        // 首段（segment == 0）与非地面态一律走 StartAttack 常规通道。
        bool startedMove = segment > 0
            && body.TryChangeGroundedSubState(g => new AttackState(body, g, baked));
        if (!startedMove && !body.StartAttack(baked, allowInterrupt))
        {
            ResetMove();
            return NodeState.Failure;
        }
        if (segment == 0)
        {
            body.ClearPlayerParryAttackWindow();
            // 延后起手时不能提前开启起跳镜头或清掉连弹计数。
            if (entry.id == "JumpThrust")
            {
                jumpThrustCameraActive = true;
                jumpThrustBossBaseY = body.GetJumpFollowWorldY();
                CombatEventBus.TriggerJumpThrustCamera(true, body, jumpThrustBossBaseY);
            }
            if (entry.extra == BossMoveExtra.ConsecutiveParry2)
                body.ResetConsecutiveTimesParried();
        }
        waitingAttack = true;
        return NodeState.Running;
    }
}
```

---

### 3. `BossAttackBaker.Bake`：将招式段烘焙为运行时 `AttackConfig`
为了不给 Boss 的几十个动画段手动创建几十份独立的 `AttackConfig.asset` 文件，也不为 Boss 单独写一套攻击状态类，`BossAttackBaker.Bake` 在内存中动态创建一份临时的 `AttackConfig`（标记 `HideFlags.HideAndDontSave`），将 `BossMoveEntry` 和当前段 `BossMoveWindow` 的动画名、时长、转向窗口、危字类型、命中槽位（刀刃 `Weapon` / 肘部 `Elbow`）、近战脉冲 `hitPulses`、音效点 `sfxCues` 和出箭点 `arrowCues` 深拷贝进去，直接交由 `AttackState` 消费：

```csharp
// BossAttackBaker.cs
// 将招式表中的一段转换为临时 AttackConfig，复用角色攻击状态；不写入项目资产。
public static class BossAttackBaker
{
    public static AttackConfig Bake(BossMoveEntry entry, string animName, BossMoveWindow w)
    {
        AttackConfig cfg = ScriptableObject.CreateInstance<AttackConfig>();
        cfg.hideFlags = HideFlags.HideAndDontSave;
        cfg.name = string.IsNullOrEmpty(animName) ? entry.id : entry.id + "_" + animName;
        cfg.AnimName = animName;
        cfg.TransitionDuration = w.transitionDuration;
        AttackCombatResolve.ApplyWindow(cfg, entry, w);
        cfg.StateDuration = w.stateDuration;
        cfg.WaitAnimEnd = w.waitAnimEnd;
        cfg.AllowRotation = true;
        cfg.RotationSpeed = 1080f;
        // 弓段跟玩家转到出箭结束，不要用短垫步那套 0.15s 转向窗。
        bool bowTrack = (w.arrowCues != null && w.arrowCues.Length > 0)
            || (!AttackWindowSync.CanMeleeHit(w.hitPulses)
                && w.stateDuration > 0.8f);
        cfg.RotationWindowEnd = bowTrack ? w.stateDuration : w.rotateEnd;
        cfg.NextCombo = null;

        // 只有有效脉冲才保留近战和危字配置；位移或弓段按无近战窗口处理。
        bool canHit = AttackWindowSync.CanMeleeHit(w.hitPulses);
        PerilousType perilous = w.perilous != PerilousType.None ? w.perilous : entry.perilous;
        cfg.Perilous = canHit ? perilous : PerilousType.None;
        cfg.HitboxSlot = canHit ? w.hitboxSlot : AttackHitboxSlot.Weapon;
        if (canHit)
        {
            cfg.HitStartTime = w.hitStartTime;
            cfg.RecoveryWindowStart = w.recoverStart;
            cfg.ComboWindowEnd = w.comboWindowEnd;
            if (w.hitPulses != null && w.hitPulses.Length > 0)
            {
                cfg.hitPulses = new HitPulse[w.hitPulses.Length];
                for (int i = 0; i < w.hitPulses.Length; i++)
                {
                    HitPulse src = w.hitPulses[i];
                    if (src == null) continue;
                    cfg.hitPulses[i] = src.Clone();
                }
            }
        }
        else
        {
            AttackWindowSync.ApplyNoHit(cfg);
        }
        ...
        return cfg;
    }
}
```

---

## 5. 专项分支对比：交锋反击（`BT_Kengeki`）、喝药惩罚（`BT_HealPunish`）与走位（`BT_MoveToTarget`）

### 1. 四个行为子节点的职责与优先级对比
在 `Selector` 根节点下，除了 0 号拦截节点 `ConditionNode`，共有四个实际驱动行为的子节点，按优先级从高到低排列：

```text
Selector 子节点优先级排序：
  ├─ [优先级 1] BT_Kengeki (近身交锋反击)
  │     └─ 触发条件：Boss 刚完美弹反玩家 (body.KengekiArmed == true) 且距离 <= kengekiMaxRange (2.5m)
  ├─ [优先级 2] BT_HealPunish (喝药重箭惩罚)
  │     └─ 触发条件：玩家喝药 (player.IsHealing) 时 Boss 未在出招，等待 HealPunishDelay 后射出 "Bow_Heavy"
  ├─ [优先级 3] BT_PickActive (常规主动选招)
  │     └─ 触发条件：不在招间走位间隔 (!IsRoamGapActive)，按距离/冷却/权重抽 Active 层招式
  └─ [优先级 4] BT_MoveToTarget (追击与绕圈走位)
        └─ 触发条件：上方三个出招节点全部返回 Failure 时兜底执行（远距离快走追击，近距离绕圈对峙）
```

---

### 2. 近身交锋反击：`BT_Kengeki`
当玩家进攻 Boss 时，Boss 通过 `DeflectMemory.TryPassiveDeflect` 连续格挡达到阈值（`PassiveDeflectThreshold`）后会升级为完美弹反，进入 `DeflectState.HandlePerfectParry`：
- 此时 Boss 不仅将玩家弹入 `ParriedState` 硬直，还会将自身的 `body.KengekiArmed = true` 置位（获得回合制反击权）。
- 下一帧 `BTBrain.Update()` 评估行为树时，排在第 1 顺位的 `BT_Kengeki` 检测到 `body.KengekiArmed == true`，立即消费该标记（置回 `false`），在 `dist <= table.kengekiMaxRange`（`2.5m`）内从 `BossMoveLayer.Kengeki` 层抽取近身快招交由 `kengekiExecutor.Begin(move)` 派发。
- 注意此時 Boss 自身可能还停留在 `DeflectState`（完美弹反动作的 0.25s 恢复期内）。当 `kengekiExecutor` 调用 `body.StartAttack` 发送 `AttackCommand` 时，`DeflectState.OnHandleCommand` 会捕获该 `AttackCommand`，将 `body.ActiveAttack` 缓存进 `pendingCounter`（此时 `body.IsParryCounterPending == true`），按玩家弹刀硬直恢复时间倒推起手时刻，实现弹刀后无缝衔接反砍：

```csharp
// BT_Kengeki.cs
// 消费 KengekiArmed 交锋标记，在近身范围选择还击招；硬直中保持等待。
public class BT_Kengeki : Node, ISelectorLock
{
    ...
    public override NodeState Evaluate()
    {
        if (BTUtil.IsTargetIncapacitated(target))
        {
            executor.ResetMove();
            return NodeState.Failure;
        }

        if (body.IsPostureBroken) return NodeState.Failure;
        // 硬直中先占住 Selector，避免 Busy 的交锋招 Evaluate 失败后落到走位。
        if (body.IsParried)
        {
            executor.ResetMove();
            return NodeState.Running;
        }
        if (executor.IsBusy)
            return executor.Evaluate();

        if (!body.KengekiArmed) return NodeState.Failure;

        float dist = Vector3.Distance(body.transform.position, target.position);
        if (dist > table.kengekiMaxRange)
        {
            body.KengekiArmed = false;
            return NodeState.Failure;
        }

        BossMoveEntry move = BossMovePicker.Pick(
            table, BossMoveLayer.Kengeki, body, body.Animator, blackboard, dist, opponent, whitelist);
        body.KengekiArmed = false;
        if (move == null) return NodeState.Failure;
        return executor.Begin(move);
    }
}
```

---

### 3. 喝药重箭惩罚：`BT_HealPunish`
《只狼》中弦一郎有一个经典机制：当玩家拉开距离喝伤药葫芦时，如果弦一郎正处于空闲走位状态，他会立刻弯弓搭箭射出一记蓄力重箭（`"Bow_Heavy"`）；但如果玩家趁弦一郎正在挥刀出招的后摇里喝药，弦一郎无法中断当前挥刀，也不会等挥完刀再补射重箭。

`BT_HealPunish` 用极简的状态机实现了这一公平博弈规则：
1. **喝药瞬间正在出招则直接作废**：在 `ArmIfPlayerHealing()` 和 `Evaluate()` 中，只要检测到 `body.IsAttacking == true`，立刻将 `punishAt = -1f` 并返回 `Failure`——不记录、不排队；
2. **空闲时喝药则启动反应延迟**：若玩家处于 `player.IsHealing` 且 Boss 未在攻击，记录 `punishAt = Time.time + body.Config.HealPunishDelay`；
3. **等待延迟期间锁住选择器**：在 `Time.time < punishAt` 期间返回 `NodeState.Running`（因实现了 `ISelectorLock`，期间阻止低优先级的 `BT_PickActive` 抽出别的普通招式，且 `BTBrain.Update` 看到 `punishAt` 等待阶段未锁定走位或保持当前姿态）；一旦到达 `punishAt` 时刻，直接从招式表按 ID 查找 `"Bow_Heavy"` 交给 `interruptExecutor.Begin(heavy)` 射出重箭：

```csharp
// BT_HealPunish.cs
// 玩家喝药时打重箭。Boss 空闲且未在出招时才记惩罚；正在打别的招则直接放弃，不存储。
public class BT_HealPunish : Node, ISelectorLock
{
    private readonly CharacterBody body;
    private readonly CharacterBody player;
    private readonly BossMoveTable table;
    private readonly BT_ExecuteMove executor;

    private float punishAt = -1f;

    ...

    public override NodeState Evaluate()
    {
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
        {
            punishAt = -1f;
            executor.ResetMove();
            return NodeState.Failure;
        }
        if (executor.IsBusy) return executor.Evaluate();

        if (player == null || player.IsIncapacitatedForBoss)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }

        // 喝药期间 Boss 若在出招：不记、不存，这次惩罚作废。
        if (player.IsHealing)
        {
            if (body.IsAttacking)
            {
                punishAt = -1f;
                return NodeState.Failure;
            }
            if (punishAt < 0f)
                ArmIfPlayerHealing();
        }

        if (punishAt < 0f) return NodeState.Failure;

        if (body.IsAttacking)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }

        if (Time.time < punishAt)
            return NodeState.Running;

        BossMoveEntry heavy = table.FindById("Bow_Heavy");
        if (heavy == null)
        {
            punishAt = -1f;
            return NodeState.Failure;
        }
        punishAt = -1f;
        return executor.Begin(heavy, interruptCurrent: false);
    }

    // 决策前预先记录喝药时机；已有惩罚或 Boss 正在攻击时不重复排队。
    public void ArmIfPlayerHealing()
    {
        if (punishAt >= 0f) return;
        if (player == null || !player.IsHealing || player.IsIncapacitatedForBoss) return;
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked) return;
        // 喝药瞬间 Boss 已在出招 → 不射重箭，也不排队。
        if (body.IsAttacking) return;
        float delay = Mathf.Max(0f, body.Config.HealPunishDelay);
        punishAt = Time.time + delay;
    }
}
```

---

### 4. 兜底追击与绕圈走位：`BT_MoveToTarget`
当上方三个出招节点都返回 `Failure`（例如刚打完一套连招正处于 `active_gap` 强制走位期）时，执行流落入最后一个子节点 `BT_MoveToTarget`：
- **远距离（`distance > stopDistance`，默认 `> 3.0m`）**：设置 `body.PreferFastWalk = true`（让 `MoveState` 播放快步追击动画 `"Walk"` 而非慢速横移），朝玩家方向发送 `new MoveCommand(moveDir)`；
- **近距离（`distance <= stopDistance`）**：设置 `body.PreferFastWalk = false`（播放架刀侧移 `"Walk_Strafe"`），调用 `RoamAroundTarget`：
  1. 身体朝向始终通过 `body.RotateYaw` 对准玩家；
  2. 移动向量由**侧向切线分量（`right * roamSign * roamStrafeStrength`）**与**微弱向内逼近分量（`toTarget.normalized * approach`）**合成，每隔 `roamStrafeDuration`（`1.2s`）翻转一次 `roamSign` 换边绕圈，营造出近身架刀游走对峙的压迫感。

```csharp
// BT_MoveToTarget.cs
// 远处追击、近处绕行；不锁定 Selector，使高优先级出招节点每帧都能抢占。
public class BT_MoveToTarget : Node
{
    ...
    public override NodeState Evaluate()
    {
        // 硬直或朝向冻结时不发移动命令，避免重新切入走位状态并转向。
        if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked || body.IsCombatYawFrozen)
        {
            return NodeState.Running;
        }

        float distance = Vector3.Distance(body.transform.position, target.position);
        bool playerIncapacitated = BTUtil.IsTargetIncapacitated(target);

        if (distance <= stopDistance)
        {
            body.PreferFastWalk = false;
            return RoamAroundTarget(playerIncapacitated ? 0f : roamApproachStrength, playerIncapacitated);
        }

        // 过远用 Walk 快跑拉近，不要用 Walk_Strafe 慢挪。
        body.PreferFastWalk = true;
        Vector3 dir3D = (target.position - body.transform.position).normalized;
        Vector2 moveDir = new Vector2(dir3D.x, dir3D.z);
        body.MoveUsesWorldDir = true;
        body.TryExecuteCommand(new MoveCommand(moveDir));

        return NodeState.Running;
    }
    ...
}
```

