# 07 动画事件集成（M8）

> 模块：M8
> 前置：M3（Hitbox/CombatManager）
> 验收：`07-anim-events-test.md`

## 一、作用

把攻击动画的特定帧和逻辑代码接通。纯 Unity Editor 操作 + 少量事件接收代码。

```
攻击动画播放
  ├─ 动画事件 "EnableHitbox"（起始帧）→ 调用 Hitbox.Enable()
  ├─ 动画事件 "DisableHitbox"（结束帧）→ 调用 Hitbox.Disable()
  └─ 忍杀动画：
     ├─ 动画事件 "ExecuteFinisher"（命中帧）→ 调 CombatManager.ExecuteFinisher()
     └─ 动画事件 "Revive" / "DrinkGourd"（表现帧）→ 调对应逻辑
```

## 二、动画事件接收（代码侧）

Hitbox 已提供 Enable/Disable（M3）。动画事件直接调这些公开方法即可，无需额外接收层。

需要新增的接收方法：

```csharp
// CombatManager 增加
public void ExecuteFinisher() { /* 清空目标一条命 + 架势归零 + 发事件 */ }

// 葫芦（M16）
public void OnDrinkGourdAnimEvent() { /* 补血动作完成时回调 */ }
```

## 三、Unity Editor 配置步骤（每段攻击动画）

1. 打开 Animator，选中攻击动画 Clip。
2. 菜单 Window → Animation 打开 Animation 窗口。
3. 选中 Clip，按时间轴：
   - 在**武器开始有判定的那一帧**（通常挥出后 1-2 帧）：
     - 点 Events → Add Animation Event
     - Function 填 `EnableHitbox`
   - 在**判定结束帧**（武器收回前）：
     - 再 Add Event，Function 填 `DisableHitbox`
4. 每个攻击招式（轻击/重击/连段每一段/危字招）都配一遍。

## 四、需要配置的动画清单

| 动画 | 事件 |
|------|------|
| 玩家轻击 atk1-atk4 | EnableHitbox / DisableHitbox |
| 玩家连段每段 | 同上 |
| Boss 近战连段 | EnableHitbox / DisableHitbox |
| Boss 突刺 (Thrust) | EnableHitbox / DisableHitbox |
| Boss 横扫 (Sweep) | EnableHitbox / DisableHitbox |
| Boss 射箭 | （箭是弹道，暂用 Hitbox 或单独箭对象） |
| 忍杀 | ExecuteFinisher（命中帧） |
| 喝葫芦 | OnDrinkGourdAnimEvent |
| 玩家受击 | （无事件，纯动画） |

## 五、注意事项

- 动画事件调用的方法必须在挂 Hitbox 的 GameObject 或引用到的对象上。
- 若 Hitbox 不在 Animator 所在对象上，事件里 `GetComponent` 拿不到 → 让 Hitbox 直接持有 Animator 或由 CharacterBody 转发。
- 事件名要和方法名完全一致（区分大小写）。
- 每个 Clip 都要重新配，配一次只对当前 Clip 生效。

## 涉及文件

- 修改：`Assets/Scripts/Combat/CombatManager.cs`（ExecuteFinisher）
- 修改：`Assets/Scripts/FrameWork/Body/CharacterBody.cs`（葫芦动画回调，可选）
- **大量 Unity Editor 操作**（配动画事件）
