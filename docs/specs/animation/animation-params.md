# 动画参数与验收

## 参数表

| 参数 | 值 | 说明 |
|------|-----|------|
| crossFadeDuration | 0.1s | 动画过渡时长 |
| blendTreeDampTime | 0.1s | Blend Tree 平滑时间 |
| walkSpeedThreshold | 0.3 | 低于此→Idle |
| runSpeedThreshold | 0.7 | 高于此→Run |
| lockOnRotateSpeed | 10°/s | 锁定旋转速度 |
| attackSpeedMultiplier | 1.0 | 攻击动画速度 |

## 验收标准

1. 移动 Blend Tree 平滑混合，无跳帧
2. 攻击/闪避/识破用 Root Motion，手感自然
3. 受击/跳跃代码控制，击退方向可控
4. EnableHitbox/DisableHitbox 精确控制判定窗口
5. OnAttackHit 在正确帧触发
6. CrossFade 过渡，无硬切

## 验收清单

- [ ] 操作：打开 GameScene，玩家匀速移动
      预期：Blend Tree 平滑混合无跳帧（对应验收标准第1条）
- [ ] 操作：玩家攻击/闪避/识破
      预期：Root Motion 驱动，手感自然（第2条）
- [ ] 操作：玩家受击/跳跃
      预期：代码控制击退/起跳方向，可控（第3条）
- [ ] 操作：观察 Animator 面板状态切换
      预期：CrossFade 过渡，无硬切（第6条）
