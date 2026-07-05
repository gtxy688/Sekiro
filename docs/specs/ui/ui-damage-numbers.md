# 伤害数字弹出

## 规格

| 属性 | 值 |
|------|-----|
| 位置 | 命中点世界坐标 + 随机偏移(避免重叠) |
| 飘动 | 向上 2 单位/s + 轻微随机水平偏移 |
| 存在时间 | 1.0s，最后 0.3s 淡出 |
| 实现 | World Space Canvas + TextMeshPro，对象池 |

## 类型

| 类型 | 颜色 | 字号 |
|------|------|------|
| 普通 | 白色 | 24pt |
| 弹刀 | 黄色 | 28pt |
| 识破 | 蓝色 | 32pt |

## 接口

- `DamageNumberManager.Spawn(DamageNumberData data)` ⇒ 在位置弹出数字
- `DamageNumberManager.Recycle(DamageNumberPopup popup)` ⇒ 回收

## 验收

1. 伤害数字命中位置弹出，白/黄/蓝区分类型
2. 按规格飘动 + 淡出
