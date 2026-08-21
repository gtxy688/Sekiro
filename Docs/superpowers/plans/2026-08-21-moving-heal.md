# 上半身喝药与慢走实现计划

> **面向 AI 代理的工作者：** 必需子技能：使用 superpowers:subagent-driven-development（推荐）或 superpowers:executing-plans 逐任务实现此计划。步骤使用复选框（`- [ ]`）语法来跟踪进度。

**目标：** 喝药期间允许玩家使用慢走四向移动，Base Layer 保持根运动，UpperBody Layer 独立播放喝药动画。

**架构：** `HealState` 同时驱动 Base Layer locomotion 和 UpperBody Override Layer；仅放行连续移动意图，其他命令全部锁定。所有退出路径在 `OnExit` 清理 Layer Weight，避免受击打断后残留上身姿势。

**技术栈：** Unity Animator Layer、Avatar Mask、Blend Tree、C# HFSM、Root Motion。

**提交约束：** 用户未要求 Git commit，执行期间不得创建 commit。

---

## 文件职责

- 修改 `Assets/Scripts/FrameWork/States/Ground/HealState.cs`：双 Layer 播放、移动和清理。
- 修改 `Assets/Scripts/FrameWork/States/Ground/GroundedState.cs`：喝药期间禁止父层跳跃/重复喝药。
- 修改 `Assets/Scripts/FrameWork/Body/CharacterBody.cs`：暴露喝药运行时标志。
- 修改 `Assets/Anim/player.controller`：使用已配置的 `UpperBody` 和 `Walk_Slow_Strafe`。
- 修改 `Docs/architecture/02-combat-data.md`：覆盖旧“喝药不可移动”规则。
- 修改 `Docs/architecture/07-anim-events.md`：记录双 Layer 行为。

### 任务 1：核对 Animator 结构

- [ ] **步骤 1：核对 Base Layer**

必须存在：

- `Idle`
- `Walk_Slow_Strafe`

`Walk_Slow_Strafe` 使用 `MoveX` / `MoveZ`，四个 Motion 分别为：

- `Walk_Slow_Forward`
- `Walk_Slow_Backward`
- `Walk_Slow_Left`
- `Walk_Slow_Right`

混合树中心不放慢走待机，零输入由代码切 `Idle`。

- [ ] **步骤 2：核对 UpperBody Layer**

必须满足：

- Layer 名：`UpperBody`
- Blending：Override
- 默认 Weight：0
- Mask：`Player_UpperBody`
- 默认状态：`UpperBody_Empty`
- 喝药状态：`Drink_UpperBody`
- 无 Animator Transition，由代码 CrossFade

- [ ] **步骤 3：核对 Avatar Mask 和 Drink Clip**

Mask：

- 包含脊柱、胸、头、双臂、双手。
- 排除 Root、Hips、双腿。

`Drink` Clip：

- Loop Time 关闭。
- Root Transform Rotation / Position Y / Position XZ 全部 Bake Into Pose。

- [ ] **步骤 4：Play Mode 前检查 Animator State**

通过 `AnimUtil.HasState` 检查 Base Layer 状态；UpperBody 使用 `Animator.HasState(layerIndex, Animator.StringToHash("Drink_UpperBody"))`。缺项时停止进入喝药状态并输出角色名、Layer 名、State 名。

### 任务 2：重写 HealState 双层播放

- [ ] **步骤 1：增加字段**

```csharp
private const string UpperLayerName = "UpperBody";
private const string DrinkState = "Drink_UpperBody";
private const string SlowWalkState = "Walk_Slow_Strafe";

private int upperLayerIndex;
private bool drinkAnimationSeen;
private string currentBaseState;
private float rotationSpeed;
```

- [ ] **步骤 2：修改 `OnEnter`**

顺序：

1. `body.UseGourd()`，失败则回待机。
2. 取得并校验 `UpperBody` 索引和 `Drink_UpperBody`。
3. 设置 `body.IsHealing = true`。
4. `SetLayerWeight(upperLayerIndex, 1f)`。
5. `CrossFadeInFixedTime(DrinkState, 0.1f, upperLayerIndex)`。
6. 按当前输入选择 Base Layer 的 `Idle` 或 `Walk_Slow_Strafe`。

```csharp
body.Animator.SetLayerWeight(upperLayerIndex, 1f);
body.Animator.CrossFadeInFixedTime(
    DrinkState, 0.1f, upperLayerIndex);
PlayBaseLocomotion(force: true);
```

- [ ] **步骤 3：修改 `OnUpdate`**

每帧：

1. 更新锁定/未锁定朝向。
2. 更新 `MoveX` / `MoveZ`。
3. 在零输入和有输入之间切 Base Layer。
4. 确认 UpperBody 已进入 `Drink_UpperBody`。
5. 当该状态 `normalizedTime >= 0.95f` 时回 `IdleState` 或 `MoveState`。

结束选择：

```csharp
if (body.MoveDirection.sqrMagnitude >= 0.01f)
    parent.SubStateMachine.ChangeState(
        new MoveState(body, parent, null));
else
    parent.SubStateMachine.ChangeState(
        new IdleState(body, parent));
```

- [ ] **步骤 4：实现 `HandleCommand` 白名单**

```csharp
public override bool HandleCommand(ICommand cmd)
{
    if (cmd is MoveCommand move)
    {
        body.MoveDirection = move.Direction;
        return true;
    }
    return true;
}
```

攻击、格挡、闪避、跳跃、喝药均被消耗且不切状态。

- [ ] **步骤 5：实现 Base Layer 切换**

```csharp
private void PlayBaseLocomotion(bool force)
{
    string wanted = body.MoveDirection.sqrMagnitude >= 0.01f
        ? SlowWalkState
        : "Idle";
    if (!force && wanted == currentBaseState) return;

    currentBaseState = wanted;
    body.Animator.CrossFadeInFixedTime(wanted, 0.08f, 0);
}
```

- [ ] **步骤 6：实现锁定四向参数**

锁定时：

```csharp
Vector3 world = body.InputToWorldDir(body.MoveDirection);
Vector3 toBoss = LockOnManager.Instance.Target.position -
                 body.transform.position;
toBoss.y = 0f;
toBoss.Normalize();
Vector3 right = Vector3.Cross(Vector3.up, toBoss);

float x = Vector3.Dot(world, right);
float z = Vector3.Dot(world, toBoss);
body.Animator.SetFloat("MoveX", x, 0.1f, Time.deltaTime);
body.Animator.SetFloat("MoveZ", z, 0.1f, Time.deltaTime);
body.RotateYaw(toBoss, rotationSpeed);
```

未锁定时：

- `MoveX = 0`
- `MoveZ = body.MoveDirection.magnitude`
- `body.RotateYaw(body.InputToWorldDir(body.MoveDirection), rotationSpeed)`

- [ ] **步骤 7：实现 `OnExit` 强制清理**

```csharp
public override void OnExit()
{
    body.IsHealing = false;
    if (upperLayerIndex >= 0)
        body.Animator.SetLayerWeight(upperLayerIndex, 0f);
}
```

受击通过状态机切换时会先调用 `OnExit`，因此 Layer 不残留。

### 任务 3：阻止父状态绕过 HealState

- [ ] **步骤 1：在 `CharacterBody` 增加标志**

```csharp
public bool IsHealing { get; set; }
```

- [ ] **步骤 2：修改 `GroundedState.OnParentHandleCommand`**

在 Jump/Heal 父层处理之前：

```csharp
if (body.IsHealing &&
    (cmd is JumpCommand || cmd is HealCommand))
{
    return true;
}
```

攻击、格挡和闪避会下钻到 `HealState` 后被吞掉；移动下钻并更新方向。

- [ ] **步骤 3：刷新 Unity 并检查 Console**

预期：喝药期间连续按跳跃、喝药、攻击、格挡、闪避均不切状态；移动正常更新。

### 任务 4：同步文档并手动验收

- [ ] **步骤 1：更新 `02-combat-data.md`**

将“喝药期间不可移动/攻击”改为：

- 允许慢走四向移动。
- 禁止攻击、格挡、闪避、跳跃和重复喝药。
- 受击仍会打断，药在进入状态时已经消耗。

- [ ] **步骤 2：更新 `07-anim-events.md`**

记录：

- Base Layer 负责 `Idle` / `Walk_Slow_Strafe` 根运动。
- UpperBody Layer 负责 `Drink_UpperBody`。
- 当前回血仍在进入状态时结算，不依赖 Drink 动画事件。

- [ ] **步骤 3：执行手动验收**

1. 原地喝药：下半身 Idle，上半身完整播放 Drink。
2. 未锁定移动喝药：慢走朝输入方向移动。
3. 锁定移动喝药：身体朝 Boss，四向慢走正确。
4. 移动中停下：Base Layer 回 Idle，上半身 Drink 不重播。
5. 再次移动：Base Layer 回慢走，上半身 Drink 进度不中断。
6. 喝药期间其他动作无响应。
7. 喝药中受击：进入受击动画，UpperBody Weight 立即归零。
8. 喝药结束：有输入回 MoveState，无输入回 IdleState。

预期：角色不滑步、不出现上下身扭曲；Console 无 Animator Layer/State Error。
