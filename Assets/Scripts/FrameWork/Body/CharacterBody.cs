using UnityEngine;

[RequireComponent(typeof(Animator), typeof(Rigidbody))]
public class CharacterBody : MonoBehaviour
{
    // 1. 状态机
    public StateMachine MainStateMachine { get; private set; }

    // 2. 组件引用
    public Animator Animator { get; private set; }
    public Rigidbody Rb { get; private set; }

    // 武器的碰撞盒（M3：BoxCast 版 Hitbox，不再用 OnTrigger）
    public Hitbox Weapon { get; private set; }

    // 3. 移动意图：无论是手柄摇杆推的，还是 Boss AI 寻路计算的，都写到这里
    public Vector3 MoveDirection { get; set; }

    // 4. 物理状态 (Body 负责检测，State 读取)
    public bool IsGrounded { get; private set; }

    // 全权根运动：位移由动画 Root 曲线驱动（Animator.applyRootMotion = true）
    // 代码不再直接设置水平速度，只负责朝向与状态切换
    public bool UseRootMotion = true;

    // ===== 战斗属性（M2）=====

    // 角色配置（SO）：玩家/Boss 各配一份，数值全部从这里读
    public CharacterConfig Config;

    // 默认攻击招式根节点（SO）：AttackCommand 不携带配置，状态机从这里取连招起点
    // （用户决策：轻重击通过不同 AttackConfig 区分，不通过 Command 字段）
    public AttackConfig LightAttack;

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

    [Tooltip("接地判定迟滞帧数：连续 N 帧结果一致才翻转，防物理抖动（默认 2）")]
    public int groundHysteresisFrames = 2;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody>();

        // 实例化纯 C# 的状态机引擎
        MainStateMachine = new StateMachine();

        // 查找武器 Hitbox（挂在手部骨骼上，所以用 GetComponentInChildren）
        Weapon = GetComponentInChildren<Hitbox>();
        if (Weapon != null)
        {
            Weapon.Initialize(this);
        }

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

    // 根运动桥接（关键）：Animator.applyRootMotion = true 时每帧回调这里。
    // 把动画的位移（deltaPosition）转成 Rigidbody 水平速度，Y 保留重力。
    // 这样位移由动画驱动（跑/攻/跳/垫步的突进都来自 Root 曲线），物理碰撞/重力仍正常。
    // 注意：实现 OnAnimatorMove 后 Unity 不再自动应用 root 旋转——转身完全交给代码 RotateTowards，
    //      避免动画 root 旋转和代码转向打架。
    private void OnAnimatorMove()
    {
        if (!UseRootMotion || Animator == null || Rb == null) return;

        Vector3 delta = Animator.deltaPosition;
        Vector3 v = Rb.velocity;
        v.x = delta.x / Time.deltaTime;
        v.z = delta.z / Time.deltaTime;
        // v.y 保留：重力由物理处理，跳跃/落地的 Y 来自物理
        Rb.velocity = v;
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
    private bool groundedHysteresis;     // 上一帧接地结果
    private int groundedChangeFrames;    // 连续"与上一帧相反"的帧数

    private void UpdateEnvironmentalChecks()
    {
        bool check;
        if (groundCheckPoint != null)
        {
            check = Physics.CheckSphere(groundCheckPoint.position, groundCheckRadius, groundLayer);
        }
        else
        {
            check = true; // 容错
        }

        // 迟滞防抖：结果必须连续 N 帧保持一致才翻转 IsGrounded。
        // 否则球边缘蹭到地面时，物理步进会让 true/false 每帧抖动，
        // 导致 GroundedState(Idle) ↔ AirState(Jump) 反复横跳（"莫名其妙的待机+跳跃动画"）
        if (check == groundedHysteresis)
        {
            groundedChangeFrames = 0;
        }
        else
        {
            groundedChangeFrames++;
            if (groundedChangeFrames >= groundHysteresisFrames)
            {
                groundedHysteresis = check;
                groundedChangeFrames = 0;
            }
        }
        IsGrounded = groundedHysteresis;
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

    // 开启武器判定（M3/M8）：攻击状态/动画事件调用。绑定本招式的伤害配置
    public void EnableWeaponHit(AttackConfig config)
    {
        if (Weapon == null || config == null) return;
        Weapon.SetConfig(config);
        Weapon.Enable();
    }

    // 关闭武器判定（M3/M8）
    public void DisableWeaponHit()
    {
        if (Weapon == null) return;
        Weapon.Disable();
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
    public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint,
                           bool isPerilous = false, PerilousType perilousType = PerilousType.None)
    {
        // 打包成值类型，供状态机做层级查询（M1）
        HitData hit = new HitData
        {
            attacker = attacker,
            healthDmg = healthDmg,
            postureDmg = postureDmg,
            hitPoint = hitPoint,
            isPerilous = isPerilous,           // M17 危字攻击标记
            perilousType = perilousType
        };

        // 1. 先问当前状态层级：能拦截吗？（防御/垫步/识破/受击期间）
        //    MainStateMachine 顶层只装 HierarchicalState，OnHitReceived 会逐层下钻到叶子状态
        //    （如 DeflectState/DodgeState/MikiriCounterState），不用直接判顶层状态类型（那条路永远 false）
        if (MainStateMachine.CurrentState != null &&
            MainStateMachine.CurrentState.OnHitReceived(hit))
        {
            return; // 被状态拦截了（盾反成功 / 垫步无敌 / 识破 / 二次受击）
        }

        // 2. 没拦住 → 伤害/架势结算（M2）：扣血 + 涨架势 + 死亡判定
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
