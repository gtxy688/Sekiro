# 项目上下文（苇名之刃 ARPG）

> **单一事实源 = 项目根 `CLAUDE.md`。**
> 架构红线、代码规范、文档访问纪律、文档加载指引表、测试要求，一律以 `CLAUDE.md` 为准。
> **本文件不复制那些内容**，只记录 WorkBuddy 环境下的差异化信息。

## 开工前

先读项目根 `CLAUDE.md`，再动手。

## 协作方式

- **AI 实现 → 用户按模块验收**。交付时必须附「验收清单」（操作步骤 + 预期结果），**不得自己宣称"完成"**
- 沟通用中文，简洁直接

## 可用工具

- **MCP `unityMCP`**（MCP for Unity v3.4.7）：可直接操作 Unity 编辑器
  - 场景/资源/脚本管理、控制 Play 模式、`read_console` 查编译错误
  - `unity_reflect` / `unity_docs` 验证 Unity API 真实性——**写 Cinemachine、Input System、URP 等接口前先验，别凭训练数据**
  - 改完脚本建议 `read_console` 确认编译通过再交付

## Skill

- `arpg-constraints`（项目级 `.codebuddy/skills/`）：引导入口，指向 `CLAUDE.md`，不含规范正文
- `unity-doc-driven-dev`（用户级 `~/.codebuddy/skills/`）：通用 Unity 文档驱动开发方法论 + 文档模板
