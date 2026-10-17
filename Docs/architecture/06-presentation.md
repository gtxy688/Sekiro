# 06 UI、音效与相机表现

> 现状依据：UI、Audio、Camera 与事件总线源码。

## 事件驱动边界

CombatEventBus 提供战斗相关通知。CombatUIController 订阅血量、架势、忍杀机会、喝药、复活、死亡、胜利、危字与锁定等事件，再把变化交给对应 View。AudioManager 订阅打击、受击、处决、喝药、复活、胜利和攻击音效等事件。相机相关组件订阅需要的震动、锁定或特殊演出事件。

订阅方在启用时订阅、禁用时取消订阅。表现系统通过事件更新战斗状态，避免 UI 或音频管理器逐帧查询全部战斗数据。

## UI 组织

UI 包含 CombatUIController 与多个 UIView / 具体 View。Controller 负责把事件路由到对应 View；具体 View 仍可能持有场景引用、Animator 或自己的更新逻辑。当前代码不能概括为标准 MVC，也不能假设所有表现都完全经由统一 Controller。

已绑定的必需角色、数值视图和组件直接使用，缺少引用不再静默跳过刷新。当前场景实际使用的动态 UI、台词组件与 AudioSource 创建，以及可选提示和音效资源的处理仍然保留。

## 相机与音效

CameraController 与 Cinemachine 组件负责跟随、锁定构图和相机表现；部分动画效果使用 DOTween。AudioManager 根据事件选择播放音效；音量设置由配置 / 设置模块提供。

CameraController 在 Awake 中初始化一次场景绑定的自由、锁定、忍杀相机及跟随、输入组件，后续事件不再反复尝试初始化。遮挡淡化组件仍按当前场景的运行时装配方式创建；未锁定、目标销毁与演出结束的恢复仍按正常生命周期处理。

## 核心源码

- Mgr/CombatEventBus.cs
- UI/CombatUIController.cs、UI/Views/
- Audio/AudioManager.cs
- Camera/CameraController.cs
- Mgr/TimeScaleController.cs
