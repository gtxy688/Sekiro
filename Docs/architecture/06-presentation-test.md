# 06 表现层 - 验收清单

> 配合 `06-presentation.md` 使用。

## 前置准备

1. `GameScene`：Main Camera 挂 CinemachineBrain（**Late Update**）+ `CameraController`（**Blend Time ≈ 0.6**）。
2. Hierarchy 根上有 `CameraFollowTarget`：Source = 玩家根，Height≈1.4。**不要挂成玩家子物体。**
3. FreeLook：Follow / Look At 都拖 `CameraFollowTarget`（不要拖玩家根或骨骼）。挂 `CinemachineOrbitInput`，**关掉** `CinemachineInputProvider`。三个 Rig 的 Aim = **Hard Look At**；Body X/Y/Z Damping = **0**；Binding = **World Space**；Heading = Position Delta 且 **Velocity Filter = 0**。
4. Play 时若没有 `LockOn Camera`，`CameraController` 会运行时补一台；要调机位：菜单 **Tools/战斗/生成锁定相机** 后 Ctrl+S。锁定 VCam 的 Follow 也是 `CameraFollowTarget`（锁定时脚本给它朝 Boss 的 yaw），Look At = Boss。
5. Canvas / AudioManager / 战斗逻辑已接好。

## M12：相机

| # | 操作 | 预期 |
|---|------|------|
| 1 | 不锁定，WASD + 鼠标 | FreeLook：W 朝镜头前方走，角色转向移动方向；鼠标左右转镜头，镜头不跟人 yaw 拧 |
| 1d | 不锁定，手柄右摇杆 | 镜头环绕旋转；按下右摇杆才索敌，轻推摇杆不索敌 |
| 1b | 不锁定，侧向走 / 斜走 | 画面清晰、不发糊（不像老花眼）；人尽量在画面中央。可有走路身体起伏，那是根运动，不是镜头拖影 |
| 1c | 不锁定，只转鼠标、人站死 | 不抖、不糊 |
| 2 | 中键锁定 Boss | 约 0.6s 从当前机位滑到锁定相机：镜头到角色背后（略偏肩），看向 Boss；角色面朝 Boss。不应硬切、穿地或突然变焦 |
| 3 | 锁定后 WASD | 围着 Boss strafe，不是「相对自由镜头」平移；镜头跟着人-敌轴向转、始终盯着 Boss；走位同样清晰不糊 |
| 3b | 锁定后贴墙 / 墙角走 | 镜头被墙挡住时应往前收到墙外，不穿进场景。墙没有碰撞体则避不了 |
| 4 | 锁定时动鼠标 / 右摇杆 | 不再 FreeLook 独立环绕（环绕输入已关） |
| 5 | 再按中键解锁 | 镜头留在**角色背后**接着当 FreeLook，不甩到侧方、也不回到锁定前的环绕角；WASD 重新相对相机 |
| 6 | Boss 死亡时若仍锁定 | 自动解锁并切回 FreeLook |
| 7 | 弹反成功 | 屏幕轻微震动（`CameraShake`） |
| 8 | Boss 崩解/处决 | 震屏（强度更大） |

> 不要靠调大 Composer DeadZone 防抖：那会让人离开画面中央。发糊先查 Rig **X Damping** 是否 > 0、Heading 速度滤波是否还开着。不要给 `CameraFollowTarget` 加位置平滑。

## M13：UI

| # | 操作 | 预期 |
|---|------|------|
| 6 | 进入战斗 | 左上角显示 2 个红点 + Boss 血条 + "苇名弦一郎" |
| 7 | Boss 受伤 | Boss 血条减少（左→右） |
| 8 | 玩家受伤 | 玩家血条减少 |
| 9 | 玩家架势增长 | 玩家架势条从中心向两边增长 |
| 10 | Boss 架势增长 | Boss 架势条从中心向两边增长 |
| 11 | Boss 架势 > 80% | 架势条变亮 + 边缘尖刺（DoTween） |
| 12 | Boss 架势崩解 | 锁定点切到 Finsher，钉在 Spine1 上，不被模型挡住 |
| 13 | 处决完成 | 左上角 Dot1 换成 DeadDot；再处决一次 Dot0 也换成 DeadDot |
| 14 | Boss 放突刺/投技危字 | 红色「危」出现在 **玩家头顶**（不在 Boss 头上、不在屏幕正中）；能看清笔画（不是红方块）；始终朝向相机；约 0.8 秒淡出 |
| 14b | 绕到玩家侧面后再放危字 | 字仍正对相机 |
| 14c | 连续两次危字 | 第一次被打断，重新弹出 |
| 14d | 玩家普通攻击 | 不出现危字 |
| 15 | 使用葫芦 | 右下角葫芦数量减少，数字闪烁 |
| 15b | 受伤后按 E 喝药 | 绿色「治」出现在 **玩家头顶**；能看清笔画（不是绿/白方块）；始终朝向相机；约 0.8 秒淡出 |
| 15c | 绕到玩家侧面再喝 | 字仍正对相机 |
| 15d | 没药时按 E | 不出现治字 |
| 15e | Boss 放危字 | 红「危」在 **玩家头顶**；玩家头上同时不要出「治」 |
| 15f | 喝药中被打断 | 治字继续播完淡出，不跟受击一起立刻消失 |
| 16 | 玩家死亡一次（还能回生） | 左下角**左边**那颗换成 EndDot，右边仍是活点；图标不消失 |
| 16b | 有复活次数时刚倒地 | 画面逐渐变暗；此时没有「死」和底栏选项；按攻击/防御无效 |
| 16q | 倒地过程结束（约 1.2 秒） | 才出现正中「死」+ DEATH + 底栏选项；无暗金方框；此后才能选 |
| 16t | 键鼠倒地后看底栏 | 左边 `LMB    起死回生`，右边 `RMB    就此死去`；不是「防御」「攻击」 |
| 16u | 暂停里改过攻击/防御后再倒地 | 底栏跟新键走 |
| 16v | 用手柄倒地（或倒地后拿起手柄） | 左边 `RT` 起死回生，右边 `LT` 就此死去 |
| 16c | 点「起死回生」或按当前攻击键 | 提示淡出，爬起能继续打 |
| 16p | 点「就此死去」或按当前防御键 | 进真死面板，不再回生 |
| 16d | 处决 Boss 最后一命 | 暗金面板：「胜利」+「击败 苇名弦一郎」+「再来一局」「退出游戏」+ 右上角叉号（默认选中再来一局） |
| 16e | 胜利后推摇杆 / WASD | 角色不走，停在待机 |
| 16f | 点「再来一局」或手柄确认 | 重载当前场景，能重新打 |
| 16n | 胜利页点「退出游戏」或右上角叉号 | 编辑器里不停 Play，只关掉胜利面板；打包后退出程序 |
| 16o | Esc 打开暂停 / 设置 / 键位 | 每个子窗口右上角都有叉号；暂停叉号继续战斗 |
| 16g | 进入战斗 | 底栏正中架势条上方出台词「我上了」，**没有深色背景框**；同时播 `Voices/160000` |
| 16h | 玩家倒地（还能回生） | 台词「我，一定会守护苇名」+ `160400`；倒地过程中画面在变暗 |
| 16i | 点「起死回生」或按攻击键复活 | 先「是龙胤的力量吗。」（160200），播完再接「那么,无论多少次杀死你为止。」；玩家头顶弹出「回生」字（与治字同位置），约 0.8 秒淡出 |
| 16j | 处决 Boss 第一条命 | 「还没完,神子的忍者!」+ `160300`；不是胜利语音 |
| 16k | 处决 Boss 最后一命 | 「苇名。。」+ `160500`；不要再出 160300 |
| 16l | Hierarchy `CombatCanvas` | 直接子物体有 `GamePanel`、`SettingPanel`、`EndPanel`、`RespawnPanel`（后两块不藏在 HUD 里） |
| 16r | 勾选 `RespawnPanel`（不 Play） | 左边「起死回生」、右边「就此死去」；暗红压暗 +「死」；没有暗金方框 |
| 16s | Play 回生倒地 | 不新建、不删掉 `Vignette` / `DeathContent` 子物体 |
| 16m | 选中面板改 TMP Font Asset 后 Play | 字体保持 Inspector 里改的，不会被代码盖成仿宋 |

## M15：音效

| # | 操作 | 预期 |
|---|------|------|
| 17 | 弹反成功 | 从 Deflect 池出声（清脆打铁），不是格挡那组 |
| 17b | 连续弹反 ≥ 3 次 | 相邻两次不是同一条（池 ≥ 2） |
| 18 | 普通防御 | 从 Block 池出声（沉闷格挡），不是弹反那组 |
| 18b | 连续格挡 ≥ 3 次 | 相邻两次不是同一条（池 ≥ 2） |
| 19 | 玩家受击 | 播 `playerHitSfx` |
| 19b | Boss 受击 | 播 `bossHitSfx`，不是玩家那条 |
| 21 | 处决 | 播处决音效 |
| 22 | 玩家死亡 | 播死亡音效 |
| 23 | 暂停设置页把音乐拉到 0 | 战斗 BGM 静音 |
| 24 | 音效拉到 0 后弹反 / 受击 | 无音效 |
| 25 | 两条都不是 0 | 弹反叮、BGM 都在，响度跟滑条走 |
| 23 | 招式 `sfxCues` 填了 clip | 动画播到该时刻出声 |

> #20（危字警示音）已移除：M17 不在本项目范围。

## 常见问题

- **切镜硬切 / 穿地 / 突然变焦**：Brain 混合不是 EaseInOut 0.6s；或没开 Inherit Position / Cylindrical Position；或锁定 VCam 的 FOV 和 FreeLook 不一致。调 `CameraController.Blend Time`（更快 0.45，更软 0.8）。
- **锁定镜头穿墙**：`LockOn Camera` 要有 `CinemachineCollider`；墙需 Default/Ground 且带碰撞体。没碰撞体只能换地图或给墙加 MeshCollider。不要把 Hurtbox 勾进 Obstacle Layers。
- **相机锁定切换失灵**：Main Camera 没挂 `CameraController`，或没订阅 `OnLockOnChanged`；Priority 没切。
- **锁定后仍是 FreeLook 绕圈**：锁定 VCam 没生成 / Priority 没高于 FreeLook。菜单 **Tools/战斗/生成锁定相机** 后再 Play。
- **锁定后镜头不架在人背后**：`CameraFollowTarget` 锁定时没朝 Boss（`SetYawTarget`）；或锁定 VCam 的 Follow 拖成了玩家根（会吃动画 yaw，镜头拧）。Follow 应是 `CameraFollowTarget`，Look At 才是 Boss。
- **走路发糊 / 老花眼**：FreeLook Rig 的 **X Damping > 0**，或 Heading **Velocity Filter > 0**，或给跟随点加了位置平滑。应阻尼全 0、滤波 0、跟随点硬贴位置。
- **侧向走镜头狂抖、调大 DeadZone 人又不居中**：Aim 还在用 Composer 跟步伐拉锯。三个 Rig 改成 **Hard Look At**，Follow/Look At 用 `CameraFollowTarget`。
- **解锁后镜头绕到角色侧方 / 绕回锁定前角度**：混合期间锁定相机仍 live。跟随点 yaw 必须等 `Brain.IsBlending` 结束后再清。提前清会让混合起点甩到世界 -Z。
- **UI 不更新**：CombatUIController 没订阅事件，或订阅了没取消（OnDisable）。
- **架势条不双向**：Fill 方式用的单边，改成 Center 镜像填充。
- **没声音**：AudioManager 没订阅；或 `Resources/Sounds/Block`、`Deflect` 为空（应有 Warning）；或路径少写了 `Sounds/`。受击看 Inspector 有没有拖 `playerHitSfx` / `bossHitSfx`。其它音效仍看 Inspector。
- **喝药不出「治」字**：场景里没有 `HealKanji`，或 CombatUI 的 `healKanjiView` 是空的。菜单 **Tools/战斗/生成治愈特效**；贴图要在 `Assets/Sekrio/FX/治.png`。
