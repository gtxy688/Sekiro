using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public class CharacterBody : MonoBehaviour
{
    // 1. 状态机
    public StateMachine MainStateMachine { get; private set; }

    // 2. 组件引用
    public Animator Animator { get; private set; }
    public Rigidbody Rb { get; private set; }

    // 武器的碰撞盒
    public WeaponHitbox Weapon { get; private set; }

    // 3. 移动意图：无论是手柄摇杆推的，还是 Boss AI 寻路计算的，都写到这里
    public Vector3 MoveDirection { get; set; }

    // 4. 物理状态 (Body 负责检测，State 读取)
    public bool IsGrounded { get; private set; }

    // ===== 战斗属性（M2）=====

    // 角色配置（SO）：玩家/Boss 各配一份，数值全部从这里读
    public CharacterConfig Config;

    // 运行时状态
    public int CurrentHP { get; private set; }
    public float CurrentPosture { get; private set; }
    public int GourdRemaining { get; private set; }
    public int ReviveRemaining { get; private set; }

    // 架势是否处于崩解状态（处决窗口内不自然回复）
    public bool IsPostureBroken { get; private set; }

    // 硬直时长：从 Config 读，容错给默认值（旧场景没拖 Config 也能跑）
    public float StunDuration => Config != null ? Config.StunDuration : 0.5f;

    // 距上次受击的时间，用于架势自然回复的延迟判断
    private float lastHitTime;

    [Header("环境检测设置")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody>();

        // 实例化纯 C# 的状态机引擎
        MainStateMachine = new StateMachine();

        // 从 Config 初始化战斗属性（M2）
        InitCombat();
    }

    private void Start()
    {
        // 启动状态机：直接进入最外层的主状态 (复合节点)
        MainStateMachine.ChangeState(new GroundedState(this));
    }

    private void Update()
    {
        // 1. 每帧更新物理环境感知 (例如是否接地)
        // 这样做的好处是：所有 State 只需要读取 body.IsGrounded，不需要在各自内部写射线检测
        UpdateEnvironmentalChecks();

        // 2. 架势自然回复（只狼：一段时间不受击就缓慢回架势）
        UpdatePostureDecay();

        // 3. 驱动主状态机运行 (主状态机会自动一层层往下驱动子状态机)
        MainStateMachine.Update();
    }

    // 接收大脑 (Brain) 传来的指令
    public bool TryExecuteCommand(ICommand cmd)
    {
        // 将大脑的指令直接喂给主状态机。
        // 返回 true  表示：指令被某个状态 (父状态或子状态) 成功消耗；
        // 返回 false 表示：当前层级下的所有状态都拒收这个指令。
        return MainStateMachine.HandleCommand(cmd);
    }

    // --- 物理环境检测 ---
    private void UpdateEnvironmentalChecks()
    {
        if (groundCheckPoint != null)
        {
            // 这里用简单的球形检测举例，实战中也可以用胶囊体 Cast
            IsGrounded = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            IsGrounded = true; // 容错
        }
    }

    // --- 供 State 调用的公共方法举例 ---
    // 比如在移动状态中，需要让角色转身
    public void RotateTowards(Vector3 direction, float speed)
    {
        if (direction.sqrMagnitude > 0.01f)
        {
            Quaternion targetRot = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * speed);
        }
    }

    // ===== 战斗数据方法（M2）=====

    // 从 Config 读取初始数值。Awake 里调用。
    private void InitCombat()
    {
        if (Config != null)
        {
            CurrentHP = Config.MaxHP;
            GourdRemaining = Config.GourdCount;
            ReviveRemaining = Config.ReviveCount;
        }
        CurrentPosture = 0f;
        IsPostureBroken = false;
    }

    // 受击结算：扣血 + 涨架势。由 ReceiveHit（物理）或外部调用。
    public void TakeDamage(int healthDmg, float postureDmg)
    {
        if (CurrentHP <= 0) return; // 已死不再重复结算

        CurrentHP -= healthDmg;
        AccumulatePosture(postureDmg);

        // 事件总线：血条/音效/相机都靠这个驱动
        CombatEventBus.TriggerTakeDamage(this, healthDmg, Mathf.Max(CurrentHP, 0));
        if (Config != null)
            CombatEventBus.TriggerHPChanged(this, Mathf.Max(CurrentHP, 0), Config.MaxHP);

        if (CurrentHP <= 0)
        {
            HandleDeath();
        }
    }

    // 累计架势。防御/弹反也会加少量（M9 细则接），这里统一入口。
    public void AccumulatePosture(float amount)
    {
        if (IsPostureBroken) return; // 崩解中不累计

        if (Config == null)
        {
            CurrentPosture = Mathf.Min(CurrentPosture + amount, 100f);
        }
        else
        {
            CurrentPosture = Mathf.Min(CurrentPosture + amount, Config.MaxPosture);
        }
        lastHitTime = Time.time; // 受击计时，用于架势回复延迟

        CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config != null ? Config.MaxPosture : 100f);

        if (CurrentPosture >= (Config != null ? Config.MaxPosture : 100f))
        {
            IsPostureBroken = true;
            // 崩解 → 切 EndureState / 处决窗口（M10 接），先只发事件
            CombatEventBus.TriggerPostureBroken(this);
        }
    }

    // 架势自然回复：停止受击超过 PostureDecayDelay 秒后，每秒回 PostureDecayRate
    private void UpdatePostureDecay()
    {
        // 崩解中 / 没配置 / 架势本来就是 0 → 不回复
        if (IsPostureBroken || Config == null || CurrentPosture <= 0f) return;

        if (Time.time - lastHitTime >= Config.PostureDecayDelay)
        {
            CurrentPosture = Mathf.Max(0f, CurrentPosture - Config.PostureDecayRate * Time.deltaTime);
            CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
        }
    }

    // 喝葫芦（M16）：有次数且没满血才生效
    public bool UseGourd()
    {
        if (Config == null) return false;
        if (GourdRemaining <= 0 || CurrentHP >= Config.MaxHP) return false;

        GourdRemaining--;
        CurrentHP = Mathf.Min(CurrentHP + Config.HealAmount, Config.MaxHP);

        CombatEventBus.TriggerGourdUsed(this, GourdRemaining);
        CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
        return true;
    }

    // 死亡判定（M14）：有复活次数 → 弹回生提示；否则真死
    private void HandleDeath()
    {
        if (ReviveRemaining > 0)
        {
            // 弹复活提示（M13 UI 接 OnReviveAvailable），等待玩家确认
            CombatEventBus.TriggerReviveAvailable(this);
        }
        else
        {
            CombatEventBus.TriggerDeath(this);
        }
    }

    // 复活（M14）：回满血 + 架势清零（用户决策：直接回满）
    public void Revive()
    {
        if (Config == null) return;

        CurrentHP = Config.MaxHP;
        CurrentPosture = 0f;
        IsPostureBroken = false;

        CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
        CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
    }

    // 处决清一条命（M10 用）：重置架势，崩解解除
    public void ClearLife()
    {
        CurrentPosture = 0f;
        IsPostureBroken = false;
        CombatEventBus.TriggerPostureChanged(this, 0f, Config != null ? Config.MaxPosture : 100f);
    }

    // 接收外界物理碰撞传来的打击
    public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint)
    {
        // 1. 【待办 A3】查询当前状态层级：弹反拦截 / 闪避免疫
        //    注意：DeflectState/DodgeState 都在 GroundedState 的"子"状态机里，
        //    必须逐层查，直接判顶层永远为 false（层级查询基建还没建）
        //    if (MainStateMachine.CurrentState is GroundedState g &&
        //        g.SubStateMachine.CurrentState is DeflectState)
        //    { HandlePerfectParry(attacker, hitPoint); return; }
        //    if (MainStateMachine.CurrentState is GroundedState g &&
        //        g.SubStateMachine.CurrentState is DodgeState)
        //    { return; } // 无敌帧免疫

        // 2. 伤害/架势结算（M2）：扣血 + 涨架势 + 死亡判定
        TakeDamage(healthDmg, postureDmg);

        // 3. 强制打断当前行为，切入受击父状态
        //    这属于环境/物理强制覆写，不走 Command，直接强切顶层状态机。
        //    无论当前在地面还是空中，都由 StunnedState 内部按 IsGrounded 分派受击子状态
        //    （若已触发死亡/复活，后续状态由死亡流程接管，这里仍进硬直）
        MainStateMachine.ChangeState(new StunnedState(this));
    }

    private void HandlePerfectParry(CharacterBody attacker, Vector3 hitPoint)
    {
        // // 增加攻击者的架势条（只狼核心机制）
        // attacker.AccumulatePosture(20f);

        // // 触发事件总线，让外部系统播放“叮”的一声打铁音效和火花特效
        // CombatEventBus.TriggerWeaponDeflected(hitPoint, DeflectType.Perfect);
    }
}
