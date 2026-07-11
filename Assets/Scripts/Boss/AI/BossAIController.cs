
/// <summary>
/// 玩家状态信息结构体 — Boss AI 决策所需的玩家上下文。
/// 由 BossAIController 消费，不包含玩家模块直接引用。
/// </summary>
[System.Serializable]
public struct PlayerStateInfo
{
    /// <summary>与 Boss 的距离（米）</summary>
    public float distanceToBoss;

    /// <summary>玩家当前连击计数</summary>
    public int playerComboCount;

    /// <summary>玩家是否正在喝药</summary>
    public bool isHealing;

    /// <summary>玩家架势是否即将崩溃（> 80%）</summary>
    public bool isPostureNearBreak;
}

/// <summary>
/// Boss AI 行为状态枚举，对应状态机中的状态。
/// </summary>
public enum BossAIState
{
    Idle,
    Move,
    Attack,
    Ranged,
    Stagger,
    Collapse,
    Executed
}

/// <summary>
/// Boss AI 控制器 — 管理行为决策和状态转换。
/// 核心逻辑为 static 方法，便于 EditMode 测试。
/// 运行时状态由 MonoBehaviour 组件驱动。
/// </summary>
public class BossAIController
{
    #region Runtime State

    private BossAIState _currentState = BossAIState.Idle;
    private float _staggerDuration;
    private bool _isCounterAttackPending;

    #endregion

    #region Properties

    /// <summary>当前 AI 行为状态</summary>
    public BossAIState CurrentState => _currentState;

    /// <summary>当前硬直剩余时长（秒）</summary>
    public float StaggerDuration => _staggerDuration;

    /// <summary>弹刀成功后是否等待反击</summary>
    public bool IsCounterAttackPending => _isCounterAttackPending;

    #endregion

    #region State Transitions

    /// <summary>
    /// 切换到反击状态（弹刀成功后调用）。
    /// 设置反击标记并进入 Attack 状态。
    /// </summary>
    public void TransitionToCounterAttackState()
    {
        _isCounterAttackPending = true;
        _currentState = BossAIState.Attack;
    }

    /// <summary>
    /// 切换到硬直状态（被弹刀时调用）。
    /// </summary>
    /// <param name="duration">硬直时长（秒）</param>
    public void TransitionToStaggerState(float duration)
    {
        _staggerDuration = duration;
        _currentState = BossAIState.Stagger;
    }

    /// <summary>
    /// 切换到指定 AI 状态
    /// </summary>
    /// <param name="state">目标状态</param>
    public void TransitionTo(BossAIState state)
    {
        _currentState = state;
        if (state != BossAIState.Attack)
            _isCounterAttackPending = false;
    }

    /// <summary>
    /// 消费反击标记（攻击状态进入时调用）
    /// </summary>
    public void ConsumeCounterAttack()
    {
        _isCounterAttackPending = false;
    }

    #endregion

    #region Attack Selection (Static, Testable)

    /// <summary>
    /// 招式定义：名称、权重、最大触发距离
    /// </summary>
    private struct AttackOption
    {
        public string Name;
        public int Weight;
        public float MaxDistance;
        public bool RequirePlayerNotComboing;

        public AttackOption(string name, int weight, float maxDist, bool requireNotComboing = false)
        {
            Name = name;
            Weight = weight;
            MaxDistance = maxDist;
            RequirePlayerNotComboing = requireNotComboing;
        }
    }

    /// <summary>
    /// 一阶段全部招式选项（名称、权重、距离限制）
    /// </summary>
    private static readonly AttackOption[] MeleeOptions = new AttackOption[]
    {
        new AttackOption("横斩",   25, 3.0f),
        new AttackOption("上挑",   25, 3.0f),
        new AttackOption("三连斩", 20, 2.5f),
        new AttackOption("突刺",   15, 3.5f),
        new AttackOption("下劈",   10, 2.0f),
        new AttackOption("跳跃劈", 10, 4.0f),
        new AttackOption("扫击",    5, 2.0f),
    };

    private static readonly AttackOption RangedOption =
        new AttackOption("射箭", 15, float.MaxValue);

    /// <summary>玩家连续攻击阈值，超过此值时近距离也可选择后撤射箭</summary>
    private const int PlayerComboThreshold = 3;

    /// <summary>远距离判定阈值（米），超过此距离时默认选择射箭</summary>
    private const float FarDistanceThreshold = 4f;

    /// <summary>
    /// 根据玩家状态选择下一个攻击招式（纯计算，可测试）。
    /// 流程：1. 筛选距离内可用招式 → 2. 权重加权随机 → 3. 返回对应 AttackData。
    /// </summary>
    /// <param name="playerState">玩家当前状态信息</param>
    /// <param name="randomSeed">随机种子（决定权重随机结果）</param>
    /// <returns>选中的攻击数据，无可用招式时返回横斩作为默认</returns>
    public static AttackData SelectAttack(PlayerStateInfo playerState, int randomSeed)
    {
        float dist = playerState.distanceToBoss;
        bool playerComboing = playerState.playerComboCount >= PlayerComboThreshold;

        // 收集可用招式
        var available = new System.Collections.Generic.List<AttackOption>();

        if (dist > FarDistanceThreshold)
        {
            // 远距离：只能射箭
            available.Add(RangedOption);
        }
        else
        {
            // 近/中距离：筛选距离内的近战招式
            for (int i = 0; i < MeleeOptions.Length; i++)
            {
                var opt = MeleeOptions[i];
                if (dist <= opt.MaxDistance)
                    available.Add(opt);
            }

            // 玩家连续攻击时，近距离也可选择后撤射箭
            if (playerComboing)
                available.Add(RangedOption);
        }

        if (available.Count == 0)
            return BossAttackData.CreateHorizontalSlash();

        // 计算总权重
        int totalWeight = 0;
        for (int i = 0; i < available.Count; i++)
            totalWeight += available[i].Weight;

        // 加权随机选择
        int roll = Mod(randomSeed, totalWeight);
        int cumulative = 0;
        for (int i = 0; i < available.Count; i++)
        {
            cumulative += available[i].Weight;
            if (roll < cumulative)
                return CreateAttackByName(available[i].Name);
        }

        // 兜底
        return CreateAttackByName(available[available.Count - 1].Name);
    }

    /// <summary>
    /// 根据招式名称创建对应的 AttackData
    /// </summary>
    internal static AttackData CreateAttackByName(string name)
    {
        switch (name)
        {
            case "横斩":   return BossAttackData.CreateHorizontalSlash();
            case "上挑":   return BossAttackData.CreateUpwardSlash();
            case "下劈":   return BossAttackData.CreateDownwardChop();
            case "突刺":   return BossAttackData.CreateThrust();
            case "跳跃劈": return BossAttackData.CreateJumpSlash();
            case "扫击":   return BossAttackData.CreateSweep();
            case "三连斩": return BossAttackData.CreateTripleSlash();
            case "射箭":   return BossAttackData.CreateArrow();
            case "后撤步": return BossAttackData.CreateBackstep();
            default:       return BossAttackData.CreateHorizontalSlash();
        }
    }

    /// <summary>
    /// 安全的取模运算（处理负数种子）
    /// </summary>
    private static int Mod(int a, int b)
    {
        int result = a % b;
        return result < 0 ? result + b : result;
    }

    #endregion
}
