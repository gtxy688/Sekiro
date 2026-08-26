# 格挡 / 弹反多音效随机池 设计

日期：2026-08-26  
状态：已确认，按此实现

关系：M15 音效表现。不改战斗判定、不改 `CombatEventBus` 签名。事件仍走 `OnWeaponDeflected`。音量仍走 `AudioSource.volume`（与暂停菜单音效滑条兼容）。

## 目标

格挡和弹反各有一组 wav，每次触发从对应池里随机播一条。连打时不连播同一条。受击是玩家 / Boss 各一条 Inspector clip，不装池。

## 不做

- 不改 `OnWeaponDeflected` 签名和触发点
- 不加音高随机、不加 AudioMixer
- 不把格挡/弹反 clip 做成 ScriptableObject，不在 Inspector 拖数组
- 不受击装池、不从 `Sounds/Hited` 随机
- 不改危字 / 处决 / 出招等其它音效字段（受击拆成玩家/Boss 两条除外）
- 不把 `PlayOneShot` 写成带 volume 的重载（会盖掉音效滑条）

## 规则

1. **来源**：`AudioManager.Awake` 调用  
   `Resources.LoadAll<AudioClip>("Sounds/Block")`  
   `Resources.LoadAll<AudioClip>("Sounds/Deflect")`  
   对应磁盘目录：`Assets/Resources/Sounds/Block`（Block1–14）、`Assets/Resources/Sounds/Deflect`（Deflect1–4）。去掉 null。只加载一次，事件里不重载。
2. **路由**：`DeflectType.Normal` → 格挡池；`DeflectType.Perfect` → 弹反池。两池各自记上次下标，互不影响。
3. **抽取**：池为空 → 本发不播，`Debug.LogWarning`。池长 1 → 播那条。池长 ≥ 2 → 均匀随机，且结果 ≠ 上次下标。第一次（尚无上次）在全池均匀随机。
4. **字段**：删除 `AudioManager` 上单个 `blockSfx` / `deflectSfx`。受击改为 `playerHitSfx` / `bossHitSfx`（有 `PlayerBrain` 播前者，否则播后者）。场景里旧引用随脚本字段消失即可，不必手清 YAML。
5. **播放**：`audioSource.PlayOneShot(clip)`，不传第二参数。

## 实现落点

- 改 `Assets/Scripts/Audio/AudioManager.cs`：Awake 加载两池；`HandleWeaponDeflected` 按类型抽 clip 再 `PlayOneShot`；`HandleTakeDamage` 按受害者选玩家/Boss clip
- 改 `Docs/architecture/06-presentation.md`：补 M15——格挡/弹反从 Resources 池随机，不连抽同一条；受击两条 Inspector
- 改 `Docs/architecture/06-presentation-test.md`：#17/#18 改为多 clip + 连打不重复；#19/#19b 玩家/Boss 受击；「没声音」补文件夹空则 Warning

抽取写成小函数（两池共用），避免 `HandleWeaponDeflected` 里复制两套随机逻辑。

## 验收

| 操作 | 预期 |
|------|------|
| 普通格挡 | 从 Block 池出声，不是弹反那组 |
| 完美弹反 | 从 Deflect 池出声，不是格挡那组 |
| 连续格挡 ≥ 3 次 | 相邻两次不是同一条（池 ≥ 2 时） |
| 连续弹反 ≥ 3 次 | 同上 |
| 玩家受击 | 播 `playerHitSfx` |
| Boss 受击 | 播 `bossHitSfx` |
| 音效滑条拉到 0 后格挡 | 无声（仍走 Source.volume） |
| 对应文件夹空 | 无声 + Console Warning |

## 风险

- 路径必须是 `Sounds/Block`，不是 `Block`。用户口述少了 `Sounds/`，以磁盘为准。
- `Resources.LoadAll` 顺序不确定，不依赖文件名排序。
- 若以后 `PlayOneShot(clip, 1f)`，音效滑条会失效；本方案不写第二参数。
