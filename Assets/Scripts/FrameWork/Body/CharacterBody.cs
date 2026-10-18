using System;
using UnityEngine;

using ARPG.Boss;
using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.FrameWork.States.Dead;
using ARPG.FrameWork.States.Ground;
using ARPG.FrameWork.States.Air;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 玩家和 Boss 共用的角色入口：连接 Unity 生命周期、内部模块与 HFSM。
    // Awake 准备组件和初始数据，Start 进入初始状态，Update 驱动状态机。
    [RequireComponent(typeof(Animator), typeof(Rigidbody))]
    public class CharacterBody : MonoBehaviour, ICombatResettable
    {
        // 1. 状态机
        public StateMachine MainStateMachine { get; private set; }

        // HFSM 调试快照：逻辑状态和动画状态分开看，避免只看 Animator 猜状态机。
        public string CurrentStatePath { get; private set; } = "<uninitialized>";
        public string PreviousStatePath { get; private set; } = "<none>";
        public string LastStateChangeReason { get; private set; } = "<none>";
        public int StateTransitionCount { get; private set; }

        // 2. 组件引用
        public Animator Animator { get; private set; }
        public Rigidbody Rb { get; private set; }

        // 移动与环境感知由 Locomotion 管理，接地点和地面层由 Inspector 配置。
        private Locomotion loco;
        private Locomotion Loco => loco ??= new Locomotion(this);

        // 供相机在 LateUpdate 读取跳跃跟随高度。
        public float GetJumpFollowWorldY() => Loco.GetJumpFollowWorldY();

        // 外部通过角色访问武器；Hitbox 的选择和开关由 WeaponController 管理。
        public Hitbox Weapon => WeaponCtrl.Weapon;
        public Hitbox ActiveHitbox => WeaponCtrl.ActiveHitbox;

        // 首次访问时创建并绑定 Hitbox，之后复用同一模块；Awake 会主动触发这次创建。
        private WeaponController weaponCtrl;
        private WeaponController WeaponCtrl => weaponCtrl ??= new WeaponController(this);

        [Header("Hitbox 槽位")]
        [Tooltip("刀。空则 Awake 自动找（会跳过肘/脚引用）")]
        public Hitbox weaponHitbox;
        [Tooltip("Elbow 段用：挂在拳头/指关节，不要挂肘关节。玩家不拖")]
        public Hitbox elbowHitbox;
        [Tooltip("预留踢击。本需求不拖")]
        public Hitbox kickHitbox;

        [Header("射箭")]
        [Tooltip("弓弦出箭点，随弓骨。玩家不拖")]
        public Transform arrowSpawn;
        [Tooltip("箭 Prefab：模型 + ArrowProjectile。不要 Hitbox、不要 Collider")]
        public ArrowProjectile arrowPrefab;
        public float arrowSpeed = 32f;
        public float arrowCastRadius = 0.12f;
        public float arrowLifetime = 2f;
        public LayerMask arrowTargetLayers;
        [Tooltip("被瞄准的胸口。空则用 Hurtbox 中心。玩家拖，Boss 不拖")]
        public Transform projectileAimPoint;

        // 3. 移动意图：无论是手柄摇杆推的，还是 Boss AI 寻路计算的，都写到这里
        public Vector3 MoveDirection { get; set; }
        // Boss AI 给的是世界 XZ；玩家输入是相机相对。MoveState 据此选转向
        public bool MoveUsesWorldDir { get; set; }
        // 每个角色自己的战斗目标；Boss 不能读取玩家 LockOnManager 的目标（它会指向 Boss 自己）。
        public Transform CombatTarget { get; set; }
        // Boss 远距离拉近用 Walk（快），近身绕圈用 Walk_Strafe（慢）。玩家不设，仍按锁定选动画。
        public bool PreferFastWalk { get; set; }
        // HP 归零（倒地待回生 / 真死）。AI 用这个停招，不要去判 DeadState 类型。
        public bool IsDowned => CurrentHP <= 0;
        // 回生动画播放中（HP 已回满但人还没站起来）。Boss 仍应按「玩家失能」处理。
        public bool IsReviving { get; private set; }
        public bool IsIncapacitatedForBoss => IsDowned || IsReviving;

        public void SetReviving(bool reviving)
        {
            IsReviving = reviving;
        }

        // 玩家 Mid/Heavy 受击已过「倒地过程」、处于躺地可被 Jump_Danger 抓取。≠ IsDowned（HP=0）。
        public bool IsKnockedDown { get; set; }

        // 4. 物理状态 (Locomotion 负责检测，State 读取)
        public bool IsGrounded => Loco.IsGrounded;

        // 全权根运动：位移由动画 Root 曲线驱动（Animator.applyRootMotion = true）
        // 空中只吃 Root 的 XZ（贴图/骨骼跟动画），Y 留给跳跃初速度和重力
        public bool UseRootMotion = true;

        // AirState 期间为 true。OnAnimatorMove 用它决定要不要丢掉 Root 的 Y
        public bool IsAirborne { get; set; }

        // ===== 战斗属性 =====

        // 角色属性配置；当前 HP、架势等可变数据保存在 CombatStats，不写回 SO。
        public CharacterConfig Config;

        // 玩家默认普攻（SO）。AttackCommand 不携带配置，待机/走路从这里取连招起点。
        // Boss 出招走 moveTable，不读这个槽。
        public AttackConfig LightAttack;
        public AttackConfig ThrustAttack;
        public AttackConfig AirAttack;

        // ===== 战斗数值 =====
        // 数值由 CombatStats 管理；通过两个回调请求本角色处理崩解和死亡状态切换。
        private CombatStats combat;
        private CombatStats Combat => combat ??= new CombatStats(
            this,
            source => ForcePostureBroken(source),
            revive => EnterDead(revive, reason: revive ? "death: enter revive pending" : "death: enter game over"));

        // 对外保留统一的角色入口，数值的实际存储在内部模块中。
        public int CurrentHP => Combat.CurrentHP;
        public float CurrentPosture => Combat.CurrentPosture;
        public int GourdRemaining => Combat.GourdRemaining;
        public int ReviveRemaining => Combat.ReviveRemaining;
        // 架势是否处于崩解状态（处决窗口内不自然回复）
        public bool IsPostureBroken => Combat.IsPostureBroken;
        public PostureBreakSource CurrentPostureBreakSource => Combat.CurrentPostureBreakSource;
        // 剩余命数（Boss 一阶段 2 条命；玩家 1 条）
        public int LivesRemaining => Combat.LivesRemaining;
        public bool IsDefeated => Combat.IsDefeated;

        // ===== 跨模块战斗事实 =====
        // 供战斗数值、AI 和表现层查询；由状态维护标志，或在本类中解析当前叶子状态。

        // 是否正在格挡姿态（DeflectState 长按中）——架势回复 ×5 用
        public bool IsGuarding { get; set; }

        // 是否正在攻击——由当前 HFSM 攻击叶子派生，Boss AI 仍只通过此门面查询。
        public bool IsAttacking => GetCurrentLeafState() is AttackStateBase;

        public bool IsParryCounterPending => GetCurrentLeafState() is DeflectState deflect
            && deflect.HasPendingCounter;

        public float PlayerParryExpectedHitTime { get; private set; }
        public float AttackHitNotBeforeTime { get; private set; }

        // 玩家弹断本角色时只保护标准普通第一刀，不跟踪玩家实际按键或连招。
        public void ArmPlayerParryAttackWindow(AttackConfig firstAttack)
        {
            ClearPlayerParryAttackWindow();
            if (firstAttack == null || firstAttack.hitPulses == null) return;
            float firstHit = float.PositiveInfinity;
            foreach (var pulse in firstAttack.hitPulses)
            {
                if (AttackWindowSync.PulseIsMelee(pulse))
                    firstHit = Mathf.Min(firstHit, pulse.start);
            }
            if (float.IsPositiveInfinity(firstHit)) return;
            PlayerParryExpectedHitTime = Time.time
                + Mathf.Max(0f, Config.PlayerParryAttackPreparationTime) + Mathf.Max(0f, firstHit);
            AttackHitNotBeforeTime = PlayerParryExpectedHitTime
                + Mathf.Max(0f, Config.PlayerParryAttackHitBuffer);
        }

        public void ClearPlayerParryAttackWindow()
        {
            PlayerParryExpectedHitTime = 0f;
            AttackHitNotBeforeTime = 0f;
        }

        public bool TryGetParriedRecoveryTime(out float recoveryTime)
        {
            recoveryTime = 0f;
            return GetCurrentLeafState() is ParriedState parried
                && parried.TryGetRecoveryTime(out recoveryTime);
        }

        private BaseState GetCurrentLeafState()
        {
            BaseState state = MainStateMachine?.CurrentState;
            while (state is HierarchicalState hierarchical
                   && hierarchical.SubStateMachine?.CurrentState is BaseState child)
            {
                state = child;
            }

            return state;
        }
        public bool IsAttackRecoveryOpen { get; set; }
        public bool IsHealing { get; set; }
        // 危字 / 飞舟：挨打仍扣血涨架势，不切受击、招不中断
        public bool AttackUninterruptible { get; set; }

        // 忍杀演出 / Elbow 投技中：双方锁命令/受击/强切，直到动画播完
        public bool IsFinisherLocked
        {
            get => isFinisherLocked;
            set
            {
                isFinisherLocked = value;
                if (value)
                {
                    Loco.CancelPendingJump();
                    MoveDirection = Vector3.zero;
                }
            }
        }
        private bool isFinisherLocked;

        // 普通回待机复用同一个地面父状态；进入时恢复默认子状态，退出时清空子状态机。
        // 指定初始子状态的强制切换仍创建独立实例。
        private GroundedState sharedGrounded;
        public GroundedState SharedGrounded => sharedGrounded ??= new GroundedState(this);

        // 转向由 FacingController 管理，首次使用时创建并复用。
        private FacingController facing;
        private FacingController Facing => facing ??= new FacingController(this);

        // 识破打断后继续锁水平朝向，直到下一招；硬直一结束走位就会对准玩家猛转。
        public bool IsCombatYawFrozen => Facing.IsCombatYawFrozen;

        // 被完美弹刀硬直中（避免查 ParriedState 类型）
        public bool IsParried { get; set; }
        // 硬直结束后交锋层可抽一招；距离过远或抽空则清掉
        public bool KengekiArmed { get; set; }

        // 多段刀当前脉冲（弹反 Boat 最后一刀等）。AttackState 写入。
        public int ActiveHitPulseIndex { get; set; } = -1;
        public bool AirJump2Used => Loco.AirJump2Used;

        public void ResetAirJump2() => Loco.ResetAirJump2();
        // 连续被对手近战完美弹开的次数。JumpThrust（3022）抽招读这个；出手或交锋中断后清零。
        public int ConsecutiveTimesParried => Deflect.ConsecutiveTimesParried;

        public void NotifyPerfectlyParried() => Deflect.NotifyPerfectlyParried();

        public void ResetConsecutiveTimesParried() => Deflect.ResetConsecutiveTimesParried();

        // 下一次出手覆盖：Boss 表行烘焙、玩家突刺会先写这里。null 则玩家回退 LightAttack。
        public AttackConfig ActiveAttack { get; set; }

        // 当前招式表行/段。出箭读伤害；AttackState 不清，BT_ExecuteMove.ResetMove 清。
        public BossMoveEntry CurrentMoveEntry { get; set; }
        public BossMoveWindow CurrentMoveWindow { get; set; }

        // 硬直 / 受击后摇：从 Config 读。玩家垫步取消：Light=StunDuration，Mid=KnockdownStunDuration，Heavy=HeavyStunDuration。
        public float StunDuration => Config.StunDuration;

        public float KnockdownStunDuration => Config.KnockdownStunDuration;

        public float HeavyStunDuration => Config.HeavyStunDuration;

        public float BrokenDeflectDodgeOpenTime =>
            Config.BrokenDeflectDodgeOpenTime;

        public float ArrowHeavyDeflectDodgeOpenTime =>
            Config.ArrowHeavyDeflectDodgeOpenTime;

        public float MidToGuardDeflectDodgeOpenTime =>
            Config.MidToGuardDeflectDodgeOpenTime;

        // DeflectMemory 记录 Boss 被动防御和连续被弹次数，通过委托请求本角色切入防御。
        private DeflectMemory deflect;
        private DeflectMemory Deflect => deflect ??= new DeflectMemory(this,
            (hit, mode) => TryChangeGroundedSubState(g => new DeflectState(this, g,
                remash: false, mode: mode, pendingHit: hit)));

        [Header("环境检测设置")]
        public Transform groundCheckPoint;
        public float groundCheckRadius = 0.2f;
        public LayerMask groundLayer;

        [Tooltip("游戏相机（原神式相机相对移动用）。不拖则自动回退 Camera.main")]
        public UnityEngine.Camera GameCamera;

        [Tooltip("接地判定迟滞帧数：连续 N 帧结果一致才翻转，防物理抖动（默认 2）")]
        public int groundHysteresisFrames = 2;

        [Header("坠出地图保护")]
        [Tooltip("掉到该 Y 以下判定坠图，传回最近安全落点（地面 y≈0 时用默认值即可）")]
        public float fallKillY = -12f;

        private Collider bodyCollider;

        // 复战复位用的初始站位。Awake 时记录——此刻 transform 已是场景里摆好的位置。
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        // 阵营直接序列化，Awake 前即可查询，避免依赖 PlayerBrain 或跨对象初始化顺序。
        // 本类用 Configs.Faction 区分枚举类型与同名属性。
        [Header("阵营")]
        [Tooltip("玩家角色必须选 Player。选错不会崩，但会静默退化——" +
                 "玩家会失去专属受击表现、无敌血等，且很难察觉。启动时会有报错提醒。")]
        [SerializeField] private Faction faction = Configs.Faction.Enemy;

        public Faction Faction => faction;

        // 先准备角色自身的依赖；跨对象装配和初始状态进入留到 Start。
        private void Awake()
        {
            Animator = GetComponent<Animator>();
            Rb = GetComponent<Rigidbody>();
            bodyCollider = GetComponent<Collider>();

            // 开启刚体插值；朝向由角色逻辑和根运动控制，避免物理碰撞把角色转倒。
            Rb.interpolation = RigidbodyInterpolation.Interpolate;
            Rb.freezeRotation = true;
            Animator.applyRootMotion = UseRootMotion;

            // 这里只创建状态机；两个回调用于调试，当前状态要到 Start 才设置。
            MainStateMachine = new StateMachine(NotifyStateChanged, GetStatePathForDebug);

            // 访问属性触发首次构造：解析各 Hitbox，并写入所属角色引用。
            _ = WeaponCtrl;

            // 构造 CombatStats 时调用 ResetForEncounter，从 Config 取得初始数值。
            _ = Combat;

            // 保存初始站位，供不重载场景的复战复位使用。
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        private void Start()
        {
            // 先同步物理位置并采样接地，避免检测初始值为 false 时误切空中状态。
            Physics.SyncTransforms();
            UpdateEnvironmentalChecks();
            // 地面父状态进入后，会在自己的子状态机中进入默认 IdleState。
            EnterGrounded("lifecycle: initial grounded");

            // 各组件的 Start 顺序不定，Ensure 保证注册时已有战斗范围对象。
            EncounterScope.Ensure().Register(this);
        }

        private void OnDestroy()
        {
            if (EncounterScope.Current != null)
                EncounterScope.Current.Unregister(this);
        }

        // 不重载场景地恢复角色数值、战斗标志、初始站位和地面待机状态。
        public void ResetForEncounter()
        {
            // 1. 数值：HP / 架势 / 葫芦 / 复活次数 / 命数
            Combat.ResetForEncounter();

            // 清除战斗锁，避免上一场的受击或成对演出阻断新一场操作。
            IsKnockedDown = false;
            IsGuarding = false;
            IsHealing = false;
            AttackUninterruptible = false;
            IsAttackRecoveryOpen = false;
            IsFinisherLocked = false;
            IsParried = false;
            KengekiArmed = false;
            SetReviving(false);
            ClearPlayerParryAttackWindow();

            // 3. 出招残留：出招途中被打断重开，会带着上一刀的判定数据
            ActiveHitPulseIndex = -1;
            ActiveAttack = null;
            CurrentMoveEntry = null;
            CurrentMoveWindow = null;
            DisableWeaponHit();

            // 4. 连弹计数：JumpThrust 的抽招条件，跨场累积会让复战开局就放出不该放的招
            ResetConsecutiveTimesParried();
            ResetAirJump2();

            // 弹反窗口与空按惩罚不跨场保留。
            ResetDeflectAttempts();

            // 5. 朝向锁定
            ClearCombatYawFrozen();
            SetSuppressRootYaw(false);

            // 6. 位移：不清的话刚体会带着上一场的速度继续滑出去
            MoveDirection = Vector3.zero;
            PreferFastWalk = false;
            Rb.velocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;

            // 7. 站位与朝向复位。流程层若要把角色放到别处，在 ResetAll() 之后覆盖即可。
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Physics.SyncTransforms();

            // 8. 状态机回地面待机
            EnterGrounded("encounter reset: grounded idle");

            // 复位绕过伤害结算，需主动发事件让 UI 同步初始数值。
            CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
            CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
            CombatEventBus.TriggerFinisherOpportunityChanged(this, false);
        }

        private void Update()
        {
            // 先更新接地等环境信息，状态机本帧读取同一份结果。
            UpdateEnvironmentalChecks();

            // 坠出地图时传回最近安全点。
            UpdateFallSafety();

            // 2. 架势自然回复（只狼：一段时间不受击就缓慢回架势）
            UpdatePostureDecay();

            // 3. 驱动主状态机运行 (主状态机会自动一层层往下驱动子状态机)
            MainStateMachine.Update();
        }

        private void LateUpdate()
        {
            // 根运动已经写完位移后再做阻挡，避免 PhysX 冲量把人弹开。
            ResolveOpponentOverlap();
        }

        private void FixedUpdate()
        {
            // 跳跃冲量等物理帧再写（Animator 是 Animate Physics），消费逻辑在 Locomotion
            Loco.ApplyPendingJumpVelocity();
        }

        // 起跳只提供垂直初速度，水平位移继续使用根运动。
        public void QueueJump() => Loco.QueueJump();

        // 返回 true = 命令吃掉。playedNew = 本次新播了 Jump2（已用过则为 false）。
        public bool TryAirJump2(out bool playedNew) => Loco.TryAirJump2(out playedNew);

        // 玩家/Boss 忽略物理互撞后，用分离把玩家挡在 Boss 体外。Boss 不被胶囊挤走。
        private void ResolveOpponentOverlap()
        {
            // 只有玩家会被胶囊挤开，Boss 不被挤走。查阵营，不再比 PlayerRef 引用。
            if (faction != Configs.Faction.Player) return;
            // 忍杀成对 Root 会短暂重叠，挤开会对不齐。
            if (IsFinisherLocked) return;

            // 当前只处理 CombatManager 指定的主对手。
            CharacterBody other = CombatManager.Instance.BossRef;
            if (other == null || other.IsFinisherLocked) return;

            Collider otherCol = other.bodyCollider;

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
            Rb.position = transform.position;
            Vector3 velocity = Rb.velocity;
            Rb.velocity = new Vector3(0f, velocity.y, 0f);
        }

        public bool UsesAirState => Config.UseAirState;

        // 手动应用根运动；空中只取 XZ，Y 留给跳跃速度与重力。
        private void OnAnimatorMove()
        {
            if (!UseRootMotion) return;

            Vector3 delta = Animator.deltaPosition;
            if (IsAirborne)
            {
                delta.y = 0f;
            }

            transform.position += delta;

            // 先应用位移，再由转向模块决定朝向，最后同步刚体。
            Facing.ApplyRootRotation(Animator);

            Rb.position = transform.position;
            Rb.rotation = transform.rotation;
        }

        // 接收大脑 (Brain) 传来的指令
        public bool TryExecuteCommand(ICommand cmd)
        {
            if (IsFinisherLocked) return true;

            // true 表示某层已消费命令，false 表示当前层级未接受、可由大脑保留缓冲重试。
            return MainStateMachine.HandleCommand(cmd);
        }

        // ===== 状态查询 / 切入 =====
        // 业务代码不要直接判顶层状态类型；顶层类型判定集中在这里。

        // 当前顶层是否为地面态（只读查询）
        public bool IsGroundedTop => MainStateMachine?.CurrentState is GroundedState;

        // ===== 顶层状态切换门面 =====
        // 跨顶层的切换统一经过这里；父状态内部的叶子切换仍由父状态负责。
        public void EnterGrounded(string reason = "enter grounded")
        {
            MainStateMachine.ChangeState(SharedGrounded, reason);
        }

        public void EnterGrounded(BaseState initialSubState, string reason = "enter grounded sub-state")
        {
            MainStateMachine.ChangeState(
                initialSubState == null ? SharedGrounded : new GroundedState(this, initialSubState),
                reason);
        }

        public void EnterAirborne(string reason = "enter air")
        {
            MainStateMachine.ChangeState(new AirState(this), reason);
        }

        public void EnterStunned(HitGrade grade, string reason = "hit: player reaction")
        {
            MainStateMachine.ChangeState(new StunnedState(this, grade), reason);
        }

        public void EnterStunned(HurtContext context, string reason = "hit: reaction")
        {
            MainStateMachine.ChangeState(new StunnedState(this, context), reason);
        }

        public void EnterDead(bool canRevive, bool alreadyDowned = false,
            string reason = "death: enter dead")
        {
            // 死亡按生命周期取消未完成的弹反尝试，不记空按。
            ResetDeflectAttempts();
            MainStateMachine.ChangeState(new DeadState(this, canRevive, alreadyDowned), reason);
        }

        // 状态调试回调：顶层和子状态都通过这里留下同一份可读快照。
        public void NotifyStateChanged(
            BaseState previous, BaseState current, string reason)
        {
            string previousPath = MainStateMachine?.PreviousStatePath
                ?? (previous == null ? "<none>" : previous.GetType().Name);
            string currentPath = MainStateMachine?.CurrentStatePath
                ?? (current == null ? "<none>" : current.GetType().Name);
            PreviousStatePath = previousPath;
            CurrentStatePath = currentPath;
            LastStateChangeReason = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
            StateTransitionCount++;

        }

        // 父状态的子状态机在切换时调用。这里不递归向下查找，
        // 因为当前项目的 HFSM 深度固定为两层，显示父/子已经足够排错。
        public void NotifyStateChanged(
            HierarchicalState parent, BaseState previous, BaseState current, string reason)
        {
            string previousPath = parent.SubStateMachine?.PreviousStatePath
                ?? parent.GetType().Name + "/<none>";
            string currentPath = parent.SubStateMachine?.CurrentStatePath
                ?? parent.GetType().Name + "/<none>";
            PreviousStatePath = previousPath;
            CurrentStatePath = currentPath;
            LastStateChangeReason = string.IsNullOrEmpty(reason) ? "unspecified" : reason;
            StateTransitionCount++;

        }

        public string GetStatePathForDebug(BaseState state)
        {
            return ResolveStatePath(null, state);
        }

        public string GetStatePathForDebug(HierarchicalState parent, BaseState state)
        {
            return ResolveStatePath(parent, state);
        }

        private string ResolveStatePath(HierarchicalState parent, BaseState state)
        {
            if (state == null) return "<none>";
            if (parent != null)
                return parent.GetType().Name + "/" + state.GetType().Name;

            if (state is HierarchicalState hierarchical)
            {
                BaseState child = hierarchical.SubStateMachine?.CurrentState;
                return child == null
                    ? hierarchical.GetType().Name + "/<none>"
                    : hierarchical.GetType().Name + "/" + child.GetType().Name;
            }

            return state.GetType().Name;
        }

        // 当前地面子状态是否为指定类型（语义查询，不暴露状态引用）
        public bool IsInGroundedSubState<T>() where T : BaseState
        {
            return MainStateMachine?.CurrentState is GroundedState g
                && g.SubStateMachine?.CurrentState is T;
        }

        // 地面上换子状态：只在顶层确为地面态时执行，否则返回 false（不改动）
        public bool TryChangeGroundedSubState(
            Func<GroundedState, BaseState> factory,
            string reason = "grounded child transition")
        {
            if (MainStateMachine?.CurrentState is not GroundedState g) return false;
            g.SubStateMachine.ChangeState(factory(g), reason);
            return true;
        }

        // 地面态换子状态；顶层非地面态（空中/受击/死亡）则重建地面父状态切入该叶子
        public void ForceChangeGroundedSubState(
            Func<GroundedState, BaseState> factory,
            string reason = "force grounded child transition")
        {
            if (MainStateMachine?.CurrentState is GroundedState g)
            {
                g.SubStateMachine.ChangeState(factory(g), reason);
                return;
            }
            // 重建时叶子先以 null 父级创建，由状态进入时取得新父状态。
            EnterGrounded(factory(null), reason + ": rebuild parent");
        }

        // 受击或起身时切入格挡 / 垫步；pressId 需传到防御入口才能开启主动弹反窗口，0 表示系统回防。
        public bool TryChangeToDeflectOrDodge(bool toDeflect, int pressId = 0)
        {
            return TryChangeGroundedSubState(g => toDeflect
                ? (BaseState)new DeflectState(this, g, pressId: pressId)
                : new DodgeState(this, g));
        }

        // 喝药重箭等打断：命中段会拒收 AttackCommand，防御态会吞掉命令却不出招，必须强切。
        public bool StartAttack(AttackConfig config, bool interruptCurrent = false)
        {
            if (IsParried || IsPostureBroken || IsFinisherLocked) return false;

            ActiveAttack = config;
            if (!interruptCurrent)
            {
                return TryExecuteCommand(new AttackCommand());
            }

            if (TryChangeGroundedSubState(g => new AttackState(this, g, config)))
                return true;

            return TryExecuteCommand(new AttackCommand());
        }

        // 环境检测与坠图保护。
        private void UpdateEnvironmentalChecks() => Loco.UpdateEnvironmentalChecks();

        private void UpdateFallSafety() => Loco.UpdateFallSafety();

        // ===== 转向接口 =====
        // 水平转向（度/秒）。刚体冻结旋转后只改 transform，避免和插值抢 yaw
        public void RotateYaw(Vector3 worldDir, float degreesPerSecond) => Facing.RotateYaw(worldDir, degreesPerSecond);

        // 立即水平朝向（忍杀开演前对齐，不用每帧转）。
        // holdUntilState：Animator 还没切到该状态前，每帧 OnAnimatorMove 后再 Snap 一次。
        public void SnapYaw(Vector3 worldDir, string holdUntilState = null) => Facing.SnapYaw(worldDir, holdUntilState);

        // 攻击转向：在 OnAnimatorMove 里转，才能盖过同一帧的 Clip 根旋转
        public void SetSteerYaw(Vector3 worldDir, float degreesPerSecond) => Facing.SetSteerYaw(worldDir, degreesPerSecond);

        public void ClearSteerYaw() => Facing.ClearSteerYaw();

        public void SetSuppressRootYaw(bool suppress) => Facing.SetSuppressRootYaw(suppress);

        // 钉住当前水平朝向：清掉 Snap 残留 hold，丢掉之后的 Root yaw / 走位转向。
        public void FreezeCombatYaw() => Facing.FreezeCombatYaw();

        public void ClearCombatYawFrozen() => Facing.ClearCombatYawFrozen();

        // 按相机水平朝向将输入转换为世界移动方向。
        public Vector3 InputToWorldDir(Vector2 inputDir) => Loco.InputToWorldDir(inputDir);

        // AttackCommand 不携带配置：优先用本次覆盖配置，否则回退到默认普攻。
        public AttackConfig GetAttackConfig()
        {
            return ActiveAttack != null ? ActiveAttack : LightAttack;
        }

        // 按受击语境读取配置，空值逐级回退到普通受击，最终使用默认动画名。
        public string ResolveHurtAnim(HurtContext context)
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
            return "Hurt_Ground";
        }

        // ===== 主动弹反尝试：输入身份 / 窗口资格 / 空按记账 =====
        // 状态经此门面使用 DeflectAttemptTracker，避免各自维护窗口和空按计数。
        private DeflectAttemptTracker deflectAttempts;

        private DeflectAttemptTracker DeflectAttempts => deflectAttempts ??= new DeflectAttemptTracker();

        // 真实物理按下：生成新的按下身份。只有 PlayerBrain 的按下回调可以调用。
        public int BeginDeflectPress() => DeflectAttempts.BeginPress();

        // 松开：按身份记录释放，不再用 IdleCommand 覆盖尚未消费的防御按下。
        public void NotifyDeflectReleased() => DeflectAttempts.ReleasePress();

        public bool IsDeflectHeld => DeflectAttempts.IsDeflectHeld;

        public bool IsDeflectPressReleased(int pressId) => DeflectAttempts.IsPressReleased(pressId);

        // 开启一次主动尝试。返回 AttemptId；系统回防、重复按下、复位残留都返回 0（不开窗口、不计空按）。
        // 调用方必须是"确实要让身体进入防御"的状态入口，不能用 TryExecuteCommand == true 代替。
        public int BeginDeflectAttempt(int pressId)
            => DeflectAttempts.BeginAttempt(pressId, Time.time, ResolveDeflectSettings());

        // 按当前时间推进尝试到期。状态 Update 与命中入口都要先调用它。
        public void AdvanceDeflectAttempt() => DeflectAttempts.Advance(Time.time);

        // 唯一的弹反资格查询：未结束的尝试 + 固定半开窗口。
        public bool IsDeflectParryActive(int attemptId)
            => DeflectAttempts.CanParry(attemptId, Time.time);

        // 成功记账：清空连续空按与窗口缩减。特殊成功分支切状态之前必须先调用。
        public bool RecordDeflectSuccess(int attemptId)
            => DeflectAttempts.RecordSuccess(attemptId, Time.time);

        // 显式结束一次尝试（垫步离开、被不可弹反的命中打断等）。幂等。
        public bool EndDeflectAttempt(int attemptId, DeflectAttemptEndReason reason)
            => DeflectAttempts.EndAttempt(attemptId, reason);

        // 这一击真的落实了、而当前尝试没弹掉它：按失败结束（空按一次）。
        public void NotifyDeflectHitFailed()
            => DeflectAttempts.EndAttempt(
                DeflectAttempts.ActiveAttemptId, DeflectAttemptEndReason.HitFailed);

        // 当前尝试缓存的窗口（调试/表现读取用，不参与判定）。
        public float CurrentDeflectWindow => DeflectAttempts.ActiveWindow;

        public int DeflectEmptyCount => DeflectAttempts.EmptyCount;

        // 复战 / 死亡：取消未完成尝试（不记空按），清除空按计数与输入身份。
        public void ResetDeflectAttempts() => DeflectAttempts.Reset();

        // 输入被暂停 / 结算 / 生命周期阻断：取消未完成尝试与输入身份，保留空按记忆。
        public void ClearDeflectInput() => DeflectAttempts.ClearInput();

        // 参数快照：来源仍是 CharacterConfig，不为弹反参数新建 SO。
        // 非法值在这里夹紧并只告警一次——运行时不夹紧会除零或让弹反窗口永久消失。
        private bool warnedDeflectSettings;

        private DeflectAttemptTracker.Settings ResolveDeflectSettings()
        {
            DeflectAttemptTracker.Settings settings = new DeflectAttemptTracker.Settings
            {
                Window = Config.DeflectWindow,
                EmptyGraceCount = Config.DeflectEmptyGraceCount,
                EmptyPenalty = Config.DeflectEmptyPenalty,
                WindowMin = Config.DeflectWindowMin,
                EmptyResetInterval = Config.DeflectEmptyResetInterval,
            };

            DeflectAttemptTracker.Settings normalized = settings.Normalized(out bool corrected);
            if (corrected && !warnedDeflectSettings)
            {
                warnedDeflectSettings = true;
                Debug.LogWarning(
                    $"{name} 的弹反窗口参数非法，已按安全值夹紧：" +
                    $"DeflectWindow={settings.Window}，DeflectWindowMin={settings.WindowMin}，" +
                    $"DeflectEmptyPenalty={settings.EmptyPenalty}，" +
                    $"DeflectEmptyGraceCount={settings.EmptyGraceCount}，" +
                    $"DeflectEmptyResetInterval={settings.EmptyResetInterval}。" +
                    "请检查 CharacterConfig（编辑器校验见 ArpgValidationRules）", this);
            }
            return normalized;
        }

        // 锁定四向移动参数：有 MoveZ 用 MoveZ，否则回退 MoveY。
        public void SetMoveStrafe(float x, float z, bool instant) => Loco.SetMoveStrafe(x, z, instant);

        // 强制打断当前攻击，将被弹硬直装入新的 GroundedState，不经 Command。
        // animName 可指定飞舟互弹等动画，armKengeki 决定硬直后是否开放交锋抽招。
        public void ForceParryStun(string animName = null, bool armKengeki = true)
        {
            AttackUninterruptible = false;
            DisableWeaponHit();
            ActiveHitPulseIndex = -1;
            if (armKengeki)
                KengekiArmed = true;
            EnterGrounded(new ParriedState(this, animName), "combat: parried");
        }

        public void ForceParryStun()
        {
            ForceParryStun(null);
        }

        // 被识破但未崩解：停挥刀，播 Mikiri_Deflect（资源侧曾写成 Miriki_Deflect）。
        public void ForceMikiriStun()
        {
            AttackUninterruptible = false;
            DisableWeaponHit();
            string anim = AnimUtil.ResolveState(Animator, "Mikiri_Deflect", "Miriki_Deflect");
            if (string.IsNullOrEmpty(anim))
            {
                ForceParryStun();
                return;
            }

            FreezeCombatYaw();
            EnterGrounded(new ParriedState(this, anim, freezeYawAfterExit: true),
                "combat: mikiri stun");
        }

        // Boss 可防御时受击转入被动格挡，连挡达阈值后升级为完美弹反；由 BTBrain 启动时开启。
        public bool EnablePassiveDeflect;

        public bool TryPassiveDeflect(HitData hit) => Deflect.TryPassiveDeflect(hit);

        // 架势崩解硬直入口：玩家 = 击飞倒地（不被处决）；Boss = 处决窗口（红点）。
        public void ForcePostureBroken(PostureBreakSource source = PostureBreakSource.Attack)
        {
            AttackUninterruptible = false;
            DisableWeaponHit();
            KengekiArmed = false;
            CombatEventBus.TriggerCameraShake(0.8f); // 崩解震屏

            // 弹反/识破受害姿态只给 Boss：玩家没有这些状态，崩解走普通击飞倒地。
            BaseState brokenState;
            switch (source)
            {
                case PostureBreakSource.Deflect:
                {
                    string deflectAnim = AnimUtil.ResolveState(Animator, "Stagger_Broken_Deflect");
                    brokenState = deflectAnim != null
                        ? new FinisherVictimState(this, deflectAnim)
                        : new StaggerBrokenState(this);
                    break;
                }
                case PostureBreakSource.Mikiri:
                {
                    string mikiriAnim = AnimUtil.ResolveState(
                        Animator, "Stagger_Broken_Mikiri", "Stagger_Broken_Miriki");
                    brokenState = mikiriAnim != null
                        ? new FinisherVictimState(this, mikiriAnim)
                        : new StaggerBrokenState(this);
                    break;
                }
                default:
                    brokenState = new StaggerBrokenState(this);
                    break;
            }

            EnterGrounded(brokenState, "combat: posture broken");
        }

        // ===== 武器接口 =====

        // 攻击状态按窗口开启武器判定，并绑定本招伤害配置。
        public void EnableWeaponHit(AttackConfig config) => WeaponCtrl.EnableWeaponHit(config);

        // 关闭武器判定
        public void DisableWeaponHit() => WeaponCtrl.DisableWeaponHit();

        // 时间轴 arrowCues 到点由 AttackState 调用。伤害读招式表该支出箭，不读烘焙 AttackConfig。
        public void SpawnArrow(int cueIndex = 0) => WeaponCtrl.SpawnArrow(cueIndex);

        public Vector3 GetProjectileAimPoint() => WeaponCtrl.GetProjectileAimPoint();

        // ===== 战斗数值接口 =====

        // 受击结算：扣血 + 涨架势。由 ReceiveHit（物理）或外部调用。
        public void TakeDamage(int healthDmg, float postureDmg, CharacterBody instigator = null)
            => Combat.TakeDamage(healthDmg, postureDmg, instigator);

        // 统一累计架势，并按来源与 allowBreak 决定是否触发崩解。
        // allowBreak=false：本次累计不会导致崩解（只狼：完美弹反时自己的架势永不崩防）
        public bool AccumulatePosture(
            float amount,
            bool allowBreak = true,
            PostureBreakSource source = PostureBreakSource.Attack,
            CharacterBody instigator = null)
            => Combat.AccumulatePosture(amount, allowBreak, source, instigator);

        // 刷新架势回复延迟。完美弹反自己不涨架势，但刀刃相撞仍算战斗，不能开始回条。
        public void MarkCombatTime() => Combat.MarkCombatTime();

        // 架势自然回复：停止受击超过 PostureDecayDelay 秒后，每秒回 PostureDecayRate（Update 驱动）
        private void UpdatePostureDecay() => Combat.UpdatePostureDecay();

        // 喝葫芦：有次数就能喝；满血也播动画、扣次数，HP 加完仍封顶
        public bool UseGourd() => Combat.UseGourd();

        // 对手已倒地：打断当前攻击回 Idle，给 Boss 改走位用。不经过 Command。
        public void CancelAttackToIdle()
        {
            if (IsFinisherLocked || IsPostureBroken || IsDefeated) return;
            AttackUninterruptible = false;
            IsAttackRecoveryOpen = false;
            ActiveAttack = null;
            DisableWeaponHit();
            ClearSteerYaw();
            SetSuppressRootYaw(false);
            EnterGrounded("boss: cancel attack to idle");
        }

        // 复活时回满血、清空架势并扣除一次复活次数。
        public void Revive() => Combat.Revive();

        // 处决清一条命：扣命 → 没命了发胜利事件；还有命 → 重置架势回满血接着打
        public void ClearLife() => Combat.ClearLife();

        // 崩解标志/架势条清掉，不切状态。倒地中再挨刀时先清再进受击，避免闪 Idle。
        public void ClearPostureBreak(float remainingRatio = 0f)
        {
            Combat.ClearPostureBreak(remainingRatio);
        }

        // 崩解超时恢复：架势清空 + 崩解解除，不扣命（与处决清命区分）
        public void RecoverFromBreak(float remainingRatio = 0f)
        {
            ClearPostureBreak(remainingRatio);
            EnterGrounded("combat: recover from posture break");
        }

        // 动画事件可选入口：正常结算改由 FinisherState 在动画结束时驱动。
        public void ExecuteFinisher()
        {
            CombatManager.Instance.ExecuteFinisher(this);
        }

        // 接收近战或箭矢上报，先由防御与当前状态拦截，再结算伤害和强制受击状态。
        public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint,
                               bool isPerilous = false, PerilousType perilousType = PerilousType.None,
                               float knockback = 0f,
                               HitGrade hitGrade = HitGrade.Light,
                               bool isProjectile = false)
        {
            if (IsFinisherLocked || IsDefeated) return;

            // 打包成值类型，供状态机做层级查询
            HitData hit = new HitData
            {
                attacker = attacker,
                healthDmg = healthDmg,
                postureDmg = postureDmg,
                hitPoint = hitPoint,
                isPerilous = isPerilous,           // 危字攻击标记
                perilousType = perilousType,
                knockback = knockback,             // 受击表现接口：击退强度
                hitGrade = hitGrade,
                isProjectile = isProjectile
            };

            bool alreadyBroken = IsPostureBroken;

            // 被动防御先于当前状态拦截，让可防御的 Boss 当场转入格挡反应。
            if (TryPassiveDeflect(hit)) return;

            // 父状态先拦截，再逐层转交当前叶子处理防御、无敌或二次受击。
            if (MainStateMachine.CurrentState != null &&
                MainStateMachine.CurrentState.OnHitReceived(hit))
            {
                return; // 被状态拦截了（盾反成功 / 垫步无敌 / 识破 / 二次受击）
            }

            // 2. 没拦住 → 伤害/架势结算：扣血 + 涨架势 + 死亡判定
            TakeDamage(healthDmg, postureDmg, attacker);

            // 实际受伤后结束未成功的防御尝试；致死已按生命周期取消，避免重复记空按。
            if (CurrentHP > 0)
                NotifyDeflectHitFailed();

            // 玩家挨实锤打断 Boss 连弹计数，下次要重新弹满 2 次才可能 JumpThrust。
            if (HitReactionUtil.IsPlayer(this) && attacker != null)
                attacker.ResetConsecutiveTimesParried();

            if (CurrentHP <= 0) return;

            if (hit.isPerilous && hit.perilousType == PerilousType.Grab
                && HitReactionUtil.IsPlayer(this)
                && CombatManager.Instance.TryStartGrabThrow(attacker, this))
                return;

            HurtContext ctx = knockback > 0f ? HurtContext.Heavy : HurtContext.Normal;

            // 玩家已经倒地后再挨刀：解除崩解，切 Hurt_Heavy 倒地受击。
            // Boss 崩解是处决窗口，保持倒地，不能被普通命中抬起来。
            if (alreadyBroken)
            {
                if (HitReactionUtil.IsPlayer(this))
                {
                    ClearPostureBreak();
                    EnterStunned(HitGrade.Heavy, "hit: player knocked down follow-up");
                }
                return;
            }

            // 本次命中才刚打崩：TakeDamage 已切 StaggerBroken，不要覆盖成普通受击
            if (IsPostureBroken) return;

            // 危字 / 飞舟：结算伤害但不切受击，招继续
            if (AttackUninterruptible)
                return;

            // 3. 强制打断当前行为，切入受击父状态。玩家按招式等级，Boss 仍按击退。
            if (HitReactionUtil.IsPlayer(this))
                EnterStunned(hit.hitGrade);
            else
                EnterStunned(ctx);
        }
    }

}
