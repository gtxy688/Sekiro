# 动画策略

## Root Motion

| 状态 | 策略 |
|------|------|
| 移动(走/跑) | 代码控制 (Rigidbody.velocity) |
| 跳跃 | 代码控制 |
| 受击后退 | 代码控制 |
| 闪避 | Root Motion |
| 攻击 | Root Motion |
| 识破 | Root Motion |
| 喝药 | Root Motion |

实现：状态切换时动态设置 `Animator.applyRootMotion = true/false`

## Animation Event

| 事件名 | 时机 | 作用 |
|--------|------|------|
| EnableHitbox | Active 起始帧 | hitboxCollider.enabled = true |
| DisableHitbox | Active 结束帧 | hitboxCollider.enabled = false |
| EnableDeflectCancel | Startup 末尾 | 标记可弹刀打断点 |
| OnAttackHit | 命中确认帧 | 触发伤害/架势/特效/音效 |
| OnFootStep | 脚部落地 | 脚步音效 |
| OnAnimationEnd | 最后一帧 | 通知状态机转换 |

## Blend Tree

- **移动：** 2D FreeformDirection，参数 moveX/moveZ (-1~1)
- **锁定移动：** 2D Simple Directional，参数 moveDirection(0-360°)/speed(0-1)
- 更新：每帧 PlayerController.Update 读取输入→转换参数→Animator.SetFloat()，dampTime=0.1s 平滑

## 状态切换

1. `StateMachine.TransitionTo(targetState)`
2. `targetState.Enter()`：设置 applyRootMotion → CrossFade(animName, 0.1s) → 重置参数
3. `targetState.Update()`：更新 Blend Tree / 事件检查
4. `targetState.Exit()`：applyRootMotion=false → 清理监听
