# 删除 CharacterBody 编辑器热重载恢复

## 目标

删除仅为 Play 模式修改脚本、Domain Reload 后继续运行而存在的自恢复逻辑，保持 Player Build 的正常生命周期路径清晰。

## 修改范围

- 删除 `CharacterBody.EnsureRuntimeReady()` 及全部调用。
- 删除 `spawnPoseRecorded` 和复战时针对 Domain Reload 的出生点兜底。
- 清理 `CharacterBody`、`CombatStats` 中对应注释。

## 保留范围

- 保留场景装配检查，如地面检测、UI 引用与战斗作用域创建。
- 保留静态状态重置和正常懒初始化逻辑。
- 不改变状态机、战斗结算和复战流程的业务规则。

## 生命周期前提

`CharacterBody.Awake()` 初始化组件引用、状态机和出生点；`Start()` 进入初始地面状态。运行时入口只在该生命周期完成后使用这些数据，不再静默重建丢失的状态机。

## 验收

- 工程中不再存在 `EnsureRuntimeReady`、`spawnPoseRecorded` 或 `runtime recovery`。
- 正常进入 Play、移动、攻击、受击、架势崩解和复战行为保持不变。
