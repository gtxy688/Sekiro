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
    // Boss AI 给的是世界 XZ；玩家输入是相机相对。MoveState 据此选转向
    public bool MoveUsesWorldDir { get; set; }
    // 每个角色自己的战斗目标；Boss 不能读取玩家 LockOnManager 的目标（它会指向 Boss 自己）。
    public Transform CombatTarget { get; set; }

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
    public AttackConfig ThrustAttack;

    // 运行时状态
    public int CurrentHP { get; private set; }
    public float CurrentPosture { get; private set; }
    public int GourdRemaining { get; private set; }
    public int ReviveRemaining { get; private set; }

    // 架势是否处于崩解状态（处决窗口内不自然回复）
    public bool IsPostureBroken { get; private set; }
    public PostureBreakSource CurrentPostureBreakSource { get; private set; }

    // 剩余命数（Boss 一阶段 2 条命；玩家 1 条）
    public int LivesRemaining { get; private set; }

    // 是否正在格挡姿态（DeflectState 长按中）——架势回复 ×5 用
    public bool IsGuarding { get; set; }

    // 是否正在攻击（AttackState 期间）——Boss AI 反制判定用（避免查状态类型，架构红线）
    public bool IsAttacking { get; set; }
    public bool IsAttackRecoveryOpen { get; set; }
    public bool IsHealing { get; set; }

    // Boss 招式集（AI 切换招式用）：AttackCommand 不携带配置（用户决策 9），
    // BT 节点先设置 ActiveAttack，AttackState 优先读它，null 则回退 LightAttack
    public AttackConfig[] AttackSet;
    public AttackConfig ActiveAttack { get; set; }

    // 硬直时长：从 Config 读，容错给默认值（旧场景没拖 Config 也能跑）
    public float StunDuration => Config != null ? Config.StunDuration : 0.5f;

    // 距上次受击的时间，用于架势自然回复的延迟判断
    private float lastHitTime;

    // 抖刀惩罚状态（M4）：连点防御缩短弹反窗口
    private int deflectMashCount;
    private float lastDeflectPressTime;
    private float deflectWindowScale = 1f;

    [Header("环境检测设置")]
    public Transform groundCheckPoint;
    public float groundCheckRadius = 0.2f;
    public LayerMask groundLayer;

    [Tooltip("游戏相机（原神式相机相对移动用）。不拖则自动回退 Camera.main")]
    public Camera GameCamera;

    [Tooltip("接地判定迟滞帧数：连续 N 帧结果一致才翻转，防物理抖动（默认 2）")]
    public int groundHysteresisFrames = 2;

    // 玩家 Controller 用 MoveZ，Boss 用 MoveY。缓存起来避免每帧 SetFloat 打到不存在的参数。
    private int moveXHash;
    private int moveForwardHash;
    private bool moveParamsResolved;
    private Collider bodyCollider;

    private void Awake()
    {
        Animator = GetComponent<Animator>();
        Rb = GetComponent<Rigidbody>();
        bodyCollider = GetComponent<Collider>();

        // 【相机微抖修复】刚体插值：
        // 位移来自动画根运动（Update 直改 transform），但物理系统每 FixedUpdate(50Hz) 会同步/回写刚体位置，
        // 相机在 LateUpdate(60Hz) 采样时会看到 50Hz 台阶 → 匀速跑动时镜头细微抖动。
        // Interpolate 让渲染位置在物理步进之间插值，把台阶抹平（物理驱动的标准做法）。
        Rb.interpolation = RigidbodyInterpolation.Interpolate;
        Rb.freezeRotation = true;
        if (Animator != null) Animator.applyRootMotion = UseRootMotion;

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
        EnsureRuntimeReady();
        if (MainStateMachine == null) return;

        // 1. 每帧更新物理环境感知 (例如是否接地)
        // 这样做的好处是：所有 State 只需要读取 body.IsGrounded，不需要在各自内部写射线检测
        UpdateEnvironmentalChecks();

        // 2. 架势自然回复（只狼：一段时间不受击就缓慢回架势）
        UpdatePostureDecay();

        // 3. 驱动主状态机运行 (主状态机会自动一层层往下驱动子状态机)
        MainStateMachine.Update();
    }

    private void LateUpdate()
    {
        if (MainStateMachine == null) return;
        // 根运动已经写完位移后再做阻挡，避免 PhysX 冲量把人弹开。
        ResolveOpponentOverlap();
    }

    // 改脚本后仍停在 Play 时，纯 C# 状态机会丢。下一帧补一套，避免 Update NRE。
    private void EnsureRuntimeReady()
    {
        if (Animator == null) Animator = GetComponent<Animator>();
        if (Rb == null) Rb = GetComponent<Rigidbody>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider>();

        if (MainStateMachine == null)
        {
            MainStateMachine = new StateMachine();
        }

        if (MainStateMachine.CurrentState == null)
        {
            if (IsPostureBroken)
            {
                MainStateMachine.ChangeState(
                    new GroundedState(this, new StaggerBrokenState(this)));
            }
            else
            {
                MainStateMachine.ChangeState(new GroundedState(this));
            }
        }
    }

    // 玩家/Boss 忽略物理互撞后，用分离把玩家挡在 Boss 体外。Boss 不被胶囊挤走。
    private void ResolveOpponentOverlap()
    {
        if (CombatManager.Instance == null) return;
        if (this != CombatManager.Instance.PlayerRef) return;

        CharacterBody other = CombatManager.Instance.BossRef;
        if (other == null) return;

        Collider otherCol = other.bodyCollider != null
            ? other.bodyCollider
            : other.GetComponent<Collider>();
        if (bodyCollider == null || otherCol == null) return;

        Vector3 direction;
        float distance;
        if (!Physics.ComputePenetration(
                bodyCollider, transform.position, transform.rotation,
                otherCol, other.transform.position, other.transform.rotation,
                out direction, out distance))
        {
            return;
        }

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;

        Vector3 delta = direction.normalized * (distance + 0.01f);
        transform.position += delta;
        if (Rb != null)
        {
            Rb.position = transform.position;
            Vector3 velocity = Rb.velocity;
            Rb.velocity = new Vector3(0f, velocity.y, 0f);
        }
    }

    // 根运动交回 Animator：不要写 OnAnimatorMove，否则 Unity 不再自动应用 Root。

    // 接收大脑 (Brain) 传来的指令
    public bool TryExecuteCommand(ICommand cmd)
    {
        EnsureRuntimeReady();
        if (MainStateMachine == null) return false;

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
    // 水平转向（度/秒）。刚体冻结旋转后只改 transform，避免和插值抢 yaw
    public void RotateYaw(Vector3 worldDir, float degreesPerSecond)
    {
        if (worldDir.sqrMagnitude < 0.01f) return;
        worldDir.y = 0f;
        if (worldDir.sqrMagnitude < 0.01f) return;
        Quaternion target = Quaternion.LookRotation(worldDir.normalized, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(transform.rotation, target, degreesPerSecond * Time.deltaTime);
    }

    // 摇杆输入 → 世界移动方向（相机相对，原神式）：
    // 输入先经相机水平朝向变换，W = 远离镜头、A/D = 屏幕左右，与相机摆放无关
    public Vector3 InputToWorldDir(Vector2 inputDir)
    {
        Camera cam = GameCamera != null ? GameCamera : Camera.main;
        if (cam != null)
        {
            Vector3 camForward = cam.transform.forward;
            camForward.y = 0f;
            camForward.Normalize();
            Vector3 camRight = cam.transform.right;
            camRight.y = 0f;
            camRight.Normalize();
            return (camForward * inputDir.y + camRight * inputDir.x).normalized;
        }
        // 没有相机时回退世界方向（容错）
        return new Vector3(inputDir.x, 0f, inputDir.y).normalized;
    }

    // 当前出手配置（M7 Boss AI 用）：Boss BT 先设 ActiveAttack 再发 AttackCommand；
    // 玩家正常走 LightAttack。AttackCommand 不携带配置（用户决策 9）
    public AttackConfig GetAttackConfig()
    {
        return ActiveAttack != null ? ActiveAttack : LightAttack;
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
            LivesRemaining = Config.LifeCount;
        }
        CurrentPosture = 0f;
        IsPostureBroken = false;
        CurrentPostureBreakSource = PostureBreakSource.Attack;
    }

    // ===== 受击动画接口（用户预留扩展点）=====
    // 按受击语境取动画名：留空的字段逐级回退到普通受击动画，再回退到写死的默认名。
    // 用户后续想区分"格挡受击/完美弹反受击/强力击退受击"，只需在 CharacterConfig 填对应动画名，
    // 代码零改动。
    public string ResolveHurtAnim(HurtContext context)
    {
        if (Config != null)
        {
            string name = null;
            switch (context)
            {
                case HurtContext.Normal:    name = Config.HurtAnim_Normal; break;
                case HurtContext.Heavy:     name = string.IsNullOrEmpty(Config.HurtAnim_Heavy) ? Config.HurtAnim_Normal : Config.HurtAnim_Heavy; break;
                case HurtContext.Guard:     name = string.IsNullOrEmpty(Config.HurtAnim_Guard) ? Config.HurtAnim_Normal : Config.HurtAnim_Guard; break;
                case HurtContext.GuardHeavy:
                    if (!string.IsNullOrEmpty(Config.HurtAnim_GuardHeavy)) name = Config.HurtAnim_GuardHeavy;
                    else if (!string.IsNullOrEmpty(Config.HurtAnim_Guard)) name = Config.HurtAnim_Guard;
                    else name = Config.HurtAnim_Normal;
                    break;
                case HurtContext.Deflected: name = string.IsNullOrEmpty(Config.HurtAnim_Deflected) ? Config.HurtAnim_Normal : Config.HurtAnim_Deflected; break;
            }
            if (!string.IsNullOrEmpty(name)) return name;
        }
        return "Hurt_Ground";
    }

    // ===== 抖刀惩罚（M4）=====
    // DeflectState 进入时调用：0.5s 内连点 ≥3 次 → 窗口 ×0.75，下限 0.1s；停止 0.5s 后恢复
    public void RegisterDeflectPress()
    {
        float now = Time.time;
        if (now - lastDeflectPressTime > (Config != null ? Config.DeflectMashWindow : 0.5f))
        {
            deflectMashCount = 0;
            deflectWindowScale = 1f;
        }
        deflectMashCount++;
        lastDeflectPressTime = now;

        if (Config != null && deflectMashCount > Config.DeflectMashLimit)
        {
            deflectWindowScale *= Config.DeflectMashPenalty;
            float min = Config.DeflectWindowMin;
            float baseWindow = Config.DeflectWindow;
            if (baseWindow * deflectWindowScale < min) deflectWindowScale = min / baseWindow;
        }
    }

    // 当前生效的弹反窗口（已计入抖刀惩罚）
    public float GetDeflectWindow()
    {
        float baseWindow = Config != null ? Config.DeflectWindow : 0.3f;
        return baseWindow * deflectWindowScale;
    }

    // 锁定四向移动参数：有 MoveZ 用 MoveZ，否则回退 MoveY。
    public void SetMoveStrafe(float x, float z, bool instant)
    {
        if (Animator == null) return;
        ResolveMoveParams();
        if (instant)
        {
            Animator.SetFloat(moveXHash, x);
            Animator.SetFloat(moveForwardHash, z);
        }
        else
        {
            Animator.SetFloat(moveXHash, x, 0.1f, Time.deltaTime);
            Animator.SetFloat(moveForwardHash, z, 0.1f, Time.deltaTime);
        }
    }

    private void ResolveMoveParams()
    {
        if (moveParamsResolved || Animator == null) return;
        moveXHash = Animator.StringToHash("MoveX");
        string forwardName = "MoveY";
        foreach (AnimatorControllerParameter parameter in Animator.parameters)
        {
            if (parameter.type == AnimatorControllerParameterType.Float &&
                parameter.name == "MoveZ")
            {
                forwardName = "MoveZ";
                break;
            }
        }
        moveForwardHash = Animator.StringToHash(forwardName);
        moveParamsResolved = true;
    }

    // 被完美弹反后的硬直入口（M4）：物理强制覆写，不走 Command，直接切顶层状态机。
    // ParriedState 装在 GroundedState 内（通过带初始子状态的构造），顶层结构不变。
    public void ForceParryStun()
    {
        EnsureRuntimeReady();
        IsAttacking = false;
        DisableWeaponHit();
        MainStateMachine.ChangeState(new GroundedState(this, new ParriedState(this)));
    }

    // 架势崩解硬直入口（M9）：玩家 = 击飞倒地（不被处决）；Boss = 处决窗口（红点）。
    public void ForcePostureBroken(PostureBreakSource source = PostureBreakSource.Attack)
    {
        EnsureRuntimeReady();
        IsAttacking = false;
        DisableWeaponHit();
        CombatEventBus.TriggerCameraShake(0.8f); // 崩解震屏

        // 弹反/识破受害姿态只给 Boss：玩家没有这些状态，崩解走普通击飞倒地。
        BaseState brokenState;
        switch (source)
        {
            case PostureBreakSource.Deflect:
                brokenState = CanEnterFinisherVictim("Stagger_Broken_Deflect")
                    ? new FinisherVictimState(this, "Stagger_Broken_Deflect")
                    : new StaggerBrokenState(this);
                break;
            case PostureBreakSource.Mikiri:
                brokenState = CanEnterFinisherVictim("Stagger_Broken_Miriki")
                    ? new FinisherVictimState(this, "Stagger_Broken_Miriki")
                    : new StaggerBrokenState(this);
                break;
            default:
                brokenState = new StaggerBrokenState(this);
                break;
        }

        MainStateMachine.ChangeState(new GroundedState(this, brokenState));
    }

    // 只有 Animator 里真有受害姿态才进处决等待；玩家不需要这些状态。
    private bool CanEnterFinisherVictim(string animName)
    {
        return AnimUtil.HasState(Animator, animName);
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
    // allowBreak=false：本次累计不会导致崩解（只狼：完美弹反时自己的架势永不崩防）
    public bool AccumulatePosture(
        float amount,
        bool allowBreak = true,
        PostureBreakSource source = PostureBreakSource.Attack)
    {
        if (IsPostureBroken) return false; // 崩解中不累计

        float maxPosture = Config != null ? Config.MaxPosture : 100f;
        CurrentPosture = Mathf.Min(CurrentPosture + amount, maxPosture);
        lastHitTime = Time.time; // 受击计时，用于架势回复延迟

        CombatEventBus.TriggerPostureChanged(this, CurrentPosture, maxPosture);

        if (allowBreak && CurrentPosture >= maxPosture)
        {
            IsPostureBroken = true;
            CurrentPostureBreakSource = source;
            // 崩解 → 崩解硬直（玩家击飞倒地 / Boss 处决窗口）
            CombatEventBus.TriggerPostureBroken(this);
            CombatEventBus.TriggerFinisherOpportunityChanged(this, true);
            ForcePostureBroken(source);
            return true;
        }

        return false;
    }

    // 架势自然回复：停止受击超过 PostureDecayDelay 秒后，每秒回 PostureDecayRate
    private void UpdatePostureDecay()
    {
        // 崩解中 / 没配置 / 架势本来就是 0 → 不回复
        if (IsPostureBroken || Config == null || CurrentPosture <= 0f) return;

        if (Time.time - lastHitTime >= Config.PostureDecayDelay)
        {
            // 基础回复速度
            float rate = Config.PostureDecayRate;

            // 按住格挡 2s 后回复 ×5（只狼：格挡姿态回架势快）
            if (IsGuarding)
            {
                rate *= Config.GuardPostureRecoveryMultiplier;
            }

            // Boss 非线性回复：架势越高回越慢（反函数手感）
            if (Config.PostureDecayInverse)
            {
                rate *= 1f - CurrentPosture / Config.MaxPosture;
            }

            CurrentPosture = Mathf.Max(0f, CurrentPosture - rate * Time.deltaTime);
            CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
        }
    }

    // 喝葫芦（M16）：有次数就能喝；满血也播动画、扣次数，HP 加完仍封顶
    public bool UseGourd()
    {
        if (Config == null) return false;
        if (GourdRemaining <= 0) return false;

        GourdRemaining--;
        CurrentHP = Mathf.Min(CurrentHP + Config.HealAmount, Config.MaxHP);

        CombatEventBus.TriggerGourdUsed(this, GourdRemaining);
        CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
        return true;
    }

    // 死亡判定（M14）：有复活次数 → 进回生待机（可按攻击键复活，超时真死）；否则直接真死
    private void HandleDeath()
    {
        IsAttacking = false;
        DisableWeaponHit();
        if (ReviveRemaining > 0)
        {
            // 弹复活提示（M13 UI 接 OnReviveAvailable），进回生待机状态
            CombatEventBus.TriggerReviveAvailable(this);
            MainStateMachine.ChangeState(new DeadState(this, true));
        }
        else
        {
            CombatEventBus.TriggerDeath(this);
            MainStateMachine.ChangeState(new DeadState(this, false));
        }
    }

    // 复活（M14）：回满血 + 架势清零（用户决策：直接回满），扣除一次复活次数
    public void Revive()
    {
        if (Config == null) return;

        ReviveRemaining--;
        CurrentHP = Config.MaxHP;
        CurrentPosture = 0f;
        IsPostureBroken = false;

        CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
        CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
        CombatEventBus.TriggerRevived(this); // UI 隐藏回生提示
    }

    // 处决清一条命（M10 用）：扣命 → 没命了发胜利事件；还有命 → 重置架势回满血接着打
    public void ClearLife()
    {
        LivesRemaining--;
        CurrentPosture = 0f;
        IsPostureBroken = false;
        CombatEventBus.TriggerFinisherOpportunityChanged(this, false);

        CombatEventBus.TriggerLifeCleared(this, LivesRemaining);

        if (LivesRemaining <= 0)
        {
            CombatEventBus.TriggerVictory(this);
            return;
        }
        CurrentHP = Config != null ? Config.MaxHP : CurrentHP;
        CombatEventBus.TriggerPostureChanged(this, 0f, Config != null ? Config.MaxPosture : 100f);
        CombatEventBus.TriggerHPChanged(this, CurrentHP, Config != null ? Config.MaxHP : 0);
    }

    // 崩解超时恢复（M9）：架势清空 + 崩解解除，不扣命（与处决清命区分）
    public void RecoverFromBreak(float remainingRatio = 0f)
    {
        EnsureRuntimeReady();
        IsPostureBroken = false;
        float maxPosture = Config != null ? Config.MaxPosture : 100f;
        CurrentPosture = Mathf.Clamp01(remainingRatio) * maxPosture;
        CombatEventBus.TriggerPostureChanged(this, CurrentPosture, maxPosture);
        CombatEventBus.TriggerFinisherOpportunityChanged(this, false);
        MainStateMachine.ChangeState(new GroundedState(this));
    }

    // 动画事件可选入口：正常结算改由 FinisherState 在动画结束时驱动。
    public void ExecuteFinisher()
    {
        CombatManager.Instance?.ExecuteFinisher(this);
    }

    // 接收外界物理碰撞传来的打击
    public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint,
                           bool isPerilous = false, PerilousType perilousType = PerilousType.None,
                           float knockback = 0f)
    {
        EnsureRuntimeReady();
        // 打包成值类型，供状态机做层级查询（M1）
        HitData hit = new HitData
        {
            attacker = attacker,
            healthDmg = healthDmg,
            postureDmg = postureDmg,
            hitPoint = hitPoint,
            isPerilous = isPerilous,           // M17 危字攻击标记
            perilousType = perilousType,
            knockback = knockback              // 受击表现接口：击退强度
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

        // 死亡（DeadState）或架势崩解（StaggerBrokenState）已由 TakeDamage 内部流程接管，
        // 这两种情况不能再覆盖为普通受击硬直
        if (CurrentHP <= 0 || IsPostureBroken) return;

        // 3. 强制打断当前行为，切入受击父状态
        //    这属于环境/物理强制覆写，不走 Command，直接强切顶层状态机。
        //    受击动画选择：knockback > 0 → Heavy（击飞/击退），否则 Normal（接口预留）
        HurtContext ctx = knockback > 0f ? HurtContext.Heavy : HurtContext.Normal;
        MainStateMachine.ChangeState(new StunnedState(this, ctx));
    }
}
