# 文档体系重构设计

> 日期：2026-08-05
> 背景：项目用 AI 辅助开发，用户负责测试与验收。现有文档（26 份 specs + CLAUDE.md）约束 AI 效果差：规则不可判定、specs 与代码脱节、验收标准缺失。目标是把文档体系重构为"可约束 AI + 可服务人工验收"的结构。

---

## 一、目标

1. **约束 AI**：CLAUDE.md 规则改为可判定表述；强制"按需读文档，不跨模块乱读"；文档与代码冲突时 AI 必须停下报告，不自行选边。
2. **服务人工验收**：每个模块规格末尾带「验收清单」，写**用户的操作步骤 + 预期结果**，用户按模块逐条验证。
3. **消除文档坏味道**：统一大小写、消除 specs 与代码冲突、单一事实来源、分离混合职责。

## 二、目录结构（统一大小写为 `Docs`）

```
CLAUDE.md                        ← L0 规则层：架构红线 + 代码规范 + 文档访问纪律 + 加载指引
Docs/                            ← 统一小写改名为 Docs（git mv）
├── README.md                    ← 总导航：模块 → spec → 验收清单 总表
├── architecture/                ← L1 架构层（技术选型、代码结构、状态机、数据层）
├── specs/                       ← L2 规格层（每模块一份，末尾带验收清单）
│   ├── deflect/  boss/  animation/  danger/  input/  posture/  ui/
├── references/                  ← L3 参考层（原始素材，AI 参考不照抄）
│   └── sekiro-genichiro-ai.md   ← 从 Docs/md/ 移入
├── testing/
│   └── test-plan.md             ← 验收清单总表（用户逐条打勾）
└── superpowers/                 ← L4 计划/记录层（保留）
```

**大小写处理**：当前 git 跟踪 `docs/`（小写），CLAUDE.md 写 `DOCS/`（大写）。Windows 下同目录，但跨平台克隆会崩。统一为 `Docs`。注意 Windows 文件系统不区分大小写，`git mv docs Docs` 会报 "destination already exists"。处理方式：先 `git rm -r --cached docs/` 解除跟踪，再 `mv docs Docs`（物理改名），最后 `git add Docs/`。详见实现计划。

## 三、specs 处理策略（不全部重写）

按扫描结果分三类：

| 类别 | 处理 | 判断依据 |
|---|---|---|
| A. 结构可用 | 保留 + 末尾补「验收清单」 | 参数表/规则清楚（约 21 份） |
| B. 与代码冲突 | 重写为与代码一致 | 见下方 B 类清单 |
| C. 降级/移动 | 移入 references/ | 天守阁弦一郎AI.md（原始 Lua 分析） |

**B 类（需重写）确切清单**（已逐份核对代码）：
- `specs/boss/boss-state-machine.md` — 声称 HFSM，实为带优先级的 FSM（tech-stack.md 已澄清）；声称存在独立 RangedState，代码中射箭走 BossAttackState
- `specs/input/input-priority.md` — 优先级顺序写错：文档写"识破/闪避 < 攻击"，代码中 Mikiri=2 > Attack=1
- 其他 21 份（animation-strategy/params、boss-ai-decision、boss-attacks、boss-deflect、boss-params、danger 3份、deflect 3份、healing、hitstop、input-buffer、posture 2份、ui 3份）保留，仅补验收清单

**统一 spec 模板**：

```
# 模块名 — 一句话职责
## 机制     ← 逻辑规则（怎么判定）
## 参数     ← 数值表（或指向 params 文档）
## 验收清单  ← 操作步骤 + 预期结果（人工验收用）
```

**验收清单格式**（写用户操作，不写 AI 自检命令）：

```
## 验收清单
- [ ] 操作：打开 GameScene，玩家按住右键
      预期：播放格挡动画，架势条不涨
- [ ] 操作：Boss 攻击判定前 12 帧内点右键
      预期：PerfectDeflect，HitStop 生效，Boss 架势条涨
```

## 四、CLAUDE.md 改造

| 现状 | 改造 |
|---|---|
| 架构约束 5 条口号（"禁止硬编码"） | 改为**可判定表述** + "用户会逐条验收" |
| 代码规范（命名/XML/300行） | 保留 |
| 测试要求 | 保留，明确"验收按模块、由用户执行" |
| 文档加载指引表 | 路径改 `Docs/`，保留 |
| 工作流 TDD | 调整为"AI 实现 → 用户验收 → 修正" |

**新增：文档访问纪律（硬规则）**

```
## 文档访问纪律
1. 收到任务 → 在加载指引表定位对应 spec，只读该文件
2. 禁止通读 Docs/ 下所有文档，禁止跨模块引用其他 spec
3. 读取方式：只读精确需要的章节
4. 文档与代码冲突 → 停下报告，不自行选边
5. 每完成一个模块 → 交付说明中附「验收清单」，供用户逐条验证
```

## 五、美术资源版本控制

**结论：美术资源全部不上传云端（用户本地使用）。**

`.gitignore` 已配置（本次已改）：

```
# 美术资源（体积大，通过网盘分享，不上传云端）
Assets/Resources/
# 场景美术源文件（Blender 等，本地使用，不上传云端）
Assets/Scenes/简洁/
Assets/Scenes/简洁.meta
```

**验证结果**（已通过 `git check-ignore`）：
- `Assets/Resources/只狼/c0000-只狼/.../a000_000000.fbx` → 已忽略 ✅
- `Assets/Scenes/简洁/7.blend`（185MB）→ 已忽略 ✅
- `Assets/Scenes/GameScene.unity` → 未被误忽略 ✅

**注意**：GitHub 仓库当前的 `origin` 已有历史，若此前大文件已被提交过，需要 `git filter-branch` 或重建仓库清洗历史（本次操作未涉及，仅预防后续误提交）。

## 六、实现顺序

1. `git mv` 目录大小写统一为 `Docs`
2. CLAUDE.md 重写：规则可判定 + 文档访问纪律 + 加载指引路径修正
3. 新建 `Docs/README.md` 总导航
4. 移动 `Docs/md/天守阁弦一郎AI.md` → `Docs/references/`
5. 按 A/B/C 分类处理 specs（补验收 / 重写 / 移动）
6. 每个模块交付后用户按「验收清单」验证

## 七、验收标准（本设计自身的）

1. `git ls-files` 中美术资源（fbx/blend/png/mat/prefab in Resources/简洁）为 0
2. CLAUDE.md 中所有文档路径为 `Docs/` 开头，且每条规则可判定
3. 每份 spec 末尾有「验收清单」小节（操作步骤 + 预期结果）
4. 文档与代码冲突点已消除（grep 验证 boss-state-machine、input-priority 等重写项）
5. AI 收到任务时只读对应 spec（人工抽查对话记录）
