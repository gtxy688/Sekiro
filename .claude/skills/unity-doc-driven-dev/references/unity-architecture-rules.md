# Unity 通用架构原则

> 适用于**所有游戏类型**（动作、卡牌、Roguelike、平台跳跃、模拟经营、解谜…）。
> 项目有特殊约定时以项目文档为准。

每条含**为什么**、**怎么做**、**要点**。

---

## 1. 数据驱动：配置与逻辑分离

**为什么**：数值散落在 .cs 里，调参要改代码重新编译，同一数值容易在多处重复后不一致。

**怎么做**：配置类带 `[CreateAssetMenu]`，运行时读配置，代码里只出现字段名、不出现具体数值。

```csharp
[CreateAssetMenu(menuName = "Config/Character Config")]
public class CharacterConfig : ScriptableObject
{
    [Header("生命")] public int maxHP = 100;
    [Header("移动")] public float moveSpeed = 5f;
}

public class CharacterBody : MonoBehaviour
{
    [SerializeField] private CharacterConfig config;
    private int hp;
    private void Awake() => hp = config.maxHP;
}
```

**要点**：
- 数值表集中在需求文档，架构文档只引用、不复制数值
- 不同系统的配置拆成不同 SO，避免做一个万能大配置
- 运行时临时状态不要存进 SO（编辑器模式下修改会持久化）

---

## 2. 关注点分离：逻辑层与表现层解耦

**为什么**：表现层直接读逻辑层字段会造成双向耦合——逻辑层改字段名 UI 就崩，且每帧读取产生无谓开销。

**怎么做**：逻辑层在数据变化时发事件，表现层订阅。

```csharp
// 逻辑层：变化时通知
public static class GameEvents
{
    public static event Action<int, int> OnHPChanged; // (current, max)
    public static void RaiseHPChanged(int cur, int max) => OnHPChanged?.Invoke(cur, max);
}

// 表现层：只在事件到达时更新
public class HPBar : MonoBehaviour
{
    [SerializeField] private Image fill;
    private void OnEnable()  => GameEvents.OnHPChanged += UpdateBar;
    private void OnDisable() => GameEvents.OnHPChanged -= UpdateBar; // 必须成对
    private void UpdateBar(int cur, int max) => fill.fillAmount = (float)cur / max;
}
```

**要点**：订阅与取消必须成对。进度条、倒计时这类**连续变化**的量直接每帧更新即可，不必强行包装成事件。

---

## 3. 状态可管：复杂行为用状态模式

**为什么**：用布尔标志和巨型 switch 管理行为，条件组合爆炸后无法维护。

**怎么做**：把行为拆成状态对象。状态是纯 C# 类，载体是 MonoBehaviour。

```csharp
public interface IState
{
    void Enter();
    void Update(float dt);
    void Exit();
}

public class IdleState : IState
{
    public void Enter() { }
    public void Update(float dt) { /* 满足条件时请求切换 */ }
    public void Exit() { }
}
```

**要点**：
- 状态间共享的数据放在载体类，状态本身只描述"这段行为做什么"
- 对象状态超过 5 个就该考虑状态模式；更少时简单分支即可
- 查询状态走载体类的封装方法，不要在业务代码里到处判状态类型

---

## 4. 生命周期成对：订阅 / 协程 / 借还

**为什么**：漏掉取消订阅或对象池归还，会造成内存泄漏与"幽灵回调"（回调持有已销毁对象）。

**要点**：
- `OnEnable` 订阅 / `OnDisable` 取消，成对出现
- 协程在 `OnDisable` 停止，或用 `destroyCancellationToken`
- 对象池 Get 与 Release 成对，场景切换时归还全部
- 静态事件在编辑器重编译时可能残留，用 `[RuntimeInitializeOnLoadMethod]` 或域重载处理

---

## 5. 性能基线

**要点**：
- 缓存组件引用，不在 Update 调 `GetComponent` / `Find` 系列 / `Camera.main`
- 避免 Update 中分配：Linq、字符串拼接、装箱、`new` 临时对象、闭包
- 高频实例化（子弹、飘字、特效）走对象池
- 物理查询加 LayerMask，避免全场景扫描
- 频繁变化的 UI 与静态 UI 拆到不同 Canvas，避免整体重建
- 优化前先用 Profiler 定位瓶颈，不凭直觉全局套规则

---

## 附：动作 / 战斗类项目的常见额外约定

以下来自动作类项目实践，**仅该类型项目参考**，其他类型不必照搬：

- **连续命中判定**：高速物体用 `BoxCast`/`SphereCast` 扫"上一帧位置→当前帧位置"，避免高速穿透漏检
- **结算走中间层**：命中检测方不直接调伤害接口，统一上报管理器裁决（无敌帧、命中去重、拼刀）
- **分层状态机**：顶层只装父状态（地面/空中/受击），叶子状态挂在父状态的子状态机里
- **命令层**：输入/AI 产出 Command 走状态机路由，玩家与敌人共用同一套执行层，区别只在"大脑"；环境强制切换（受击、落地）直接切状态，不走 Command
