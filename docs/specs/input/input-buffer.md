# 输入缓冲 (InputBuffer)

## 机制

- 窗口：150ms
- 动画播放期间按键不丢失，进入缓冲队列
- 超过 150ms 的旧输入→清空队列后重新入队
- 动画可取消点→`GetNextInput()` 取出最高优先级输入→丢弃剩余

## 接口

- `AddInput(CombatInput)`：入队
- `GetNextInput() → CombatInput?`：取出最高优先级并移除
- `PeekNextInput() → CombatInput?`：预览不移除
- `Clear()`：清空
- `HasInput / Count`：状态查询

## CombatInput 枚举（按优先级排列）

```
Deathblow > Deflect > DeflectRelease > Attack > Dodge > Mikiri > Jump > Heal > LockOn > Move
```
