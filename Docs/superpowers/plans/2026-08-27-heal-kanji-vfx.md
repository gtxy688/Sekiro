# 治愈「治」字 Billboard 实现计划

> **面向 AI 代理的工作者：** 本会话用户已要求直接开工。步骤用复选框跟踪。不要擅自 git commit。

**目标：** 玩家喝药成功时，在玩家头顶弹出绿色「治」字 Billboard（绿雾 + 火星，约 0.8 秒淡出）。

**架构：** 沿用 `OnGourdUsed`。`CombatUIController` 把跟随目标绑到玩家。`HealKanjiView` 照 `PerilousWarningView`：世界空间 Quad + 加法 Shader，另加旋转 Aura 与 ParticleSystem。`LateUpdate` 钉头骨、朝向相机。

**技术栈：** Unity 2022 LTS、URP、DoTween、CombatEventBus。无自动化测试；交付验收清单。不要 commit。

**规格：** `Docs/superpowers/specs/2026-08-27-heal-kanji-vfx-design.md`  
**只改架构文档：** `Docs/architecture/06-presentation.md`、`06-presentation-test.md`

---

## 文件结构

| 路径 | 职责 |
| --- | --- |
| 创建 `Assets/Shaders/FX/HealAura.shader` | 程序径向绿雾，加法，`ZTest Always` |
| 创建 `Assets/Scripts/UI/Views/HealKanjiView.cs` | 跟玩家头、Billboard、0.8s、雾旋转、火星 |
| 创建 `Assets/Editor/HealKanjiBuilder.cs` | 菜单生成材质/预制体、挂到 Controller |
| 修改 `Assets/Scripts/UI/CombatUIController.cs` | `HandleGourdUsed` 播治字 |
| 修改 `Assets/Editor/CombatHUDBuilder.cs` | 绑定 `healKanjiView` |
| 修改 `Docs/architecture/06-presentation.md` | 治愈 Billboard 小节 |
| 修改 `Docs/architecture/06-presentation-test.md` | #15 旁加治字项 |
| 用户提供 `Assets/Sekiro/FX/治.png` | Builder 输入；不要手写 PNG |
| Builder 生成 `Assets/Prefabs/FX/Heal/` | 不要手写 YAML Prefab |

---

### 任务 1：HealAura Shader
- [x] 写入 `ARPG/FX/HealAura`

### 任务 2：HealKanjiView
- [x] 创建 View

### 任务 3：Controller
- [x] `HandleGourdUsed` 绑玩家并 `ShowHeal`

### 任务 4：Builder
- [x] `Tools/战斗/生成治愈特效`

### 任务 5：HUD Builder
- [x] 查找并赋值 `healKanjiView`

### 任务 6：文档
- [x] `06-presentation.md` / `06-presentation-test.md`
