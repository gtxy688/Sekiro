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
        public bool EnableStateDebugLog;

        // 2. 组件引用
        public Animator Animator { get; private set; }
        public Rigidbody Rb { get; private set; }

        // 移动感知模块：接地检测（迟滞防抖）/坠图保护/跳跃冲量/锁定动画参数/输入换算/跟髋骨采样
        // 已迁入 Locomotion。懒创建理由同 Facing；EnsureGroundDetectionWired（Awake 接线序列化字段）保留在本类。
        private Locomotion loco;
        private Locomotion Loco => loco ??= new Locomotion(this);

        // 跳跃镜头跟髋骨采样已迁入 Locomotion（相机在 LateUpdate 采样）
        public float GetJumpFollowWorldY() => Loco.GetJumpFollowWorldY();

        // 武器的碰撞盒（M3）：运行时状态与判定开关已迁入 WeaponController，这里只转发
        public Hitbox Weapon => WeaponCtrl.Weapon;
        public Hitbox ActiveHitbox => WeaponCtrl.ActiveHitbox;

        // 武器模块：Hitbox 槽位解析 + 判定开关 + 射箭；构造时完成原 InitHitboxes（Awake 时机）
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

        // ===== 战斗属性（M2）=====

        // 角色配置（SO）：玩家/Boss 各配一份，数值全部从这里读
        public CharacterConfig Config;

        // 玩家默认普攻（SO）。AttackCommand 不携带配置，待机/走路从这里取连招起点。
        // Boss 出招走 moveTable，不读这个槽。
        public AttackConfig LightAttack;
        public AttackConfig ThrustAttack;
        public AttackConfig AirAttack;

        // ===== 战斗数值（M2）=====
        // 运行时数值状态与结算已迁入 CombatStats（普通类）；本类只保留只读转发与两个编排入口
        // （崩解 → ForcePostureBroken / 死亡 → DeadState，经构造注入的委托）。懒创建理由同 Facing。
        private CombatStats combat;
        private CombatStats Combat => combat ??= new CombatStats(
            this,
            source => ForcePostureBroken(source),
            revive => EnterDead(revive, reason: revive ? "death: enter revive pending" : "death: enter game over"));

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
        // 这些不是用来替代 HFSM 当前状态的调试显示，而是给战斗数值、AI、表现层读取的
        // 低成本事实快照。状态进入/退出时负责维护；跨模块不要直接判 CurrentState 类型。

        // 是否正在格挡姿态（DeflectState 长按中）——架势回复 ×5 用
        public bool IsGuarding { get; set; }

        // 是否正在攻击（AttackState 期间）——Boss AI 反制判定用（避免查状态类型，架构红线）
        public bool IsAttacking { get; set; }
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

        // 陷阱3-1 状态复用：无参 GroundedState 每角色仅一实例。OnEnter 走 GetInitialSubState
        // （Idle 已随实例缓存）、OnExit 清空子状态机，重复进入安全——消灭高频"回待机"的
        // Grounded+Idle 成对分配。带强制子状态的变体（被弹/崩解/喝药等物理覆写）仍按需 new。
        private GroundedState sharedGrounded;
        public GroundedState SharedGrounded => sharedGrounded ??= new GroundedState(this);

        // 转向模块：朝向的运行时状态与决策已迁入 FacingController（重构试点一），
        // 本类只保留同名转发接口，调用方零改动。按需创建，避免为未使用的角色提前分配。
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
        // （记账已迁入 DeflectMemory，这里只转发）
        public int ConsecutiveTimesParried => Deflect.ConsecutiveTimesParried;

        public void NotifyPerfectlyParried() => Deflect.NotifyPerfectlyParried();

        public void ResetConsecutiveTimesParried() => Deflect.ResetConsecutiveTimesParried();

        // 下一次出手覆盖：Boss 表行烘焙、玩家突刺会先写这里。null 则玩家回退 LightAttack。
        public AttackConfig ActiveAttack { get; set; }

        // 当前招式表行/段。出箭读伤害；AttackState 不清，BT_ExecuteMove.ResetMove 清。
        public BossMoveEntry CurrentMoveEntry { get; set; }
        public BossMoveWindow CurrentMoveWindow { get; set; }

        // 硬直 / 受击后摇：从 Config 读。玩家垫步取消：Light=StunDuration，Mid=KnockdownStunDuration，Heavy=HeavyStunDuration。
        public float StunDuration => Config != null ? Config.StunDuration : 0.5f;

        public float KnockdownStunDuration => Config != null ? Config.KnockdownStunDuration : 1.2f;

        public float HeavyStunDuration => Config != null ? Config.HeavyStunDuration : 1.2f;

        public float BrokenDeflectDodgeOpenTime =>
            Config != null ? Config.BrokenDeflectDodgeOpenTime : 0f;

        public float ArrowHeavyDeflectDodgeOpenTime =>
            Config != null ? Config.ArrowHeavyDeflectDodgeOpenTime : 0f;

        public float MidToGuardDeflectDodgeOpenTime =>
            Config != null ? Config.MidToGuardDeflectDodgeOpenTime : 0f;

        // 弹反记忆模块：抖刀惩罚 + Boss 被动防御 + 连续被弹的记账已迁入 DeflectMemory。
        // 切状态编排权通过委托留在本类（模块"请求"、Façade 编排）；懒创建理由同 Facing。
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

        // ===== 阵营 =====
        // 「这是谁」的唯一真相源。判断身份一律查 Faction 属性，
        // 不要写 body == CombatManager.Instance.PlayerRef 这种对象身份比较。
        //
        // 注意：这里刻意不自动推导。推导要么靠 GetComponent<PlayerBrain>()——
        // 那会让框架层反过来依赖玩家层（PlayerBrain 在 ARPG.Player），方向是错的；
        // 要么靠 CombatManager 在 Start 里回填——但各组件 Start 顺序不定，
        // 谁先查谁就拿到未推导的错值。字段是序列化的，Awake 时就已就位，两条坑都绕开。
        // 本类内部一律写 Configs.Faction 而不是 Faction：
        // 属性名与枚举类型名完全同名（Faction Faction），虽然 C# 的 "Color Color" 规则
        // 允许这种写法，但读起来容易绕，索性写全，一眼看得出哪个是类型。
        [Header("阵营")]
        [Tooltip("玩家角色必须选 Player。选错不会崩，但会静默退化——" +
                 "玩家会失去专属受击表现、无敌血等，且很难察觉。启动时会有报错提醒。")]
        [SerializeField] private Faction faction = Configs.Faction.Enemy;

        public Faction Faction => faction;

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

            EnsureGroundDetectionWired();

            // 实例化纯 C# 的状态机引擎
            MainStateMachine = new StateMachine(NotifyStateChanged, GetStatePathForDebug);

            // 武器模块构造（原 InitHitboxes：解析默认刀 + Initialize）
            _ = WeaponCtrl;

            // 从 Config 初始化战斗属性（M2，原 InitCombat 已并入 CombatStats 构造）
            _ = Combat;

            // 记录初始站位：复战 / 连战要把角色拉回这里。
            // 不重载场景的话没有别人会帮它复位，Boss 会死在上一场倒下的地方。
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
        }

        // 地面检测接线：groundCheckPoint/groundLayer 都没赋值时，UpdateEnvironmentalChecks
        // 会走 check=true 容错分支，IsGrounded 恒 true → 走出擂台边缘不进空中状态，
        // 人悬浮着直接掉出地图。这里按胶囊体脚底自动生成检测点、按 Layer 名补掩码；
        // Inspector 里赋过值的以手动为准。
        private void EnsureGroundDetectionWired()
        {
            if (groundCheckPoint == null)
            {
                Transform t = transform.Find("GroundCheck");
                if (t == null)
                {
                    GameObject go = new GameObject("GroundCheck");
                    t = go.transform;
                    t.SetParent(transform, false);
                    float bottom = 0f;
                    CapsuleCollider cap = bodyCollider as CapsuleCollider;
                    if (cap != null)
                        bottom = cap.center.y - cap.height * 0.5f;
                    t.localPosition = new Vector3(0f, bottom + 0.05f, 0f);
                }
                groundCheckPoint = t;
            }

            if (groundLayer == 0)
            {
                int ground = LayerMask.NameToLayer("Ground");
                groundLayer = 1 << (ground >= 0 ? ground : 3);
            }
        }

        private void Start()
        {
            // 进状态机前先采一次接地。迟滞默认 false，否则头几帧 IsGrounded 仍假，
            // GroundedState 会当成踩空切 AirState，进场播 Jump/Fall。
            Physics.SyncTransforms();
            UpdateEnvironmentalChecks();
            EnterGrounded("lifecycle: initial grounded");

            // 注册进本场战斗的重置清单。用 Ensure() 而非 Current：
            // 各组件的 Start 顺序不定，谁先跑到谁负责把 Scope 建出来。
            EncounterScope.Ensure()?.Register(this);
        }

        private void OnDestroy()
        {
            if (EncounterScope.Current != null)
                EncounterScope.Current.Unregister(this);
        }

        // 复战重置：把角色恢复到「战斗刚开始」的瞬间——数值、锁标志、站位、状态机全部归零。
        //
        // 为什么需要：项目此前唯一的重置手段是重载场景（MonoBehaviour 全部重建，状态自然归零），
        // 所以下面这些状态从没被显式清过，也从没暴露过问题。
        // 复战 / 连战要走「原地重开」，不重载场景，那时漏掉任何一项都会原样带进下一场。
        public void ResetForEncounter()
        {
            // 1. 数值：HP / 架势 / 葫芦 / 复活次数 / 命数
            Combat.ResetForEncounter();

            // 2. 战斗内锁标志。漏掉任何一个复战开场都会异常，
            //    其中 IsFinisherLocked 残留最致命——双方会永久无法操作。
            IsKnockedDown = false;
            IsGuarding = false;
            IsHealing = false;
            AttackUninterruptible = false;
            IsAttackRecoveryOpen = false;
            IsFinisherLocked = false;
            IsParried = false;
            KengekiArmed = false;
            SetReviving(false);

            // 3. 出招残留：出招途中被打断重开，会带着上一刀的判定数据
            IsAttacking = false;
            ActiveHitPulseIndex = -1;
            ActiveAttack = null;
            CurrentMoveEntry = null;
            CurrentMoveWindow = null;
            DisableWeaponHit();

            // 4. 连弹计数：JumpThrust 的抽招条件，跨场累积会让复战开局就放出不该放的招
            ResetConsecutiveTimesParried();
            ResetAirJump2();

            // 5. 朝向锁定
            ClearCombatYawFrozen();
            SetSuppressRootYaw(false);

            // 6. 位移：不清的话刚体会带着上一场的速度继续滑出去
            MoveDirection = Vector3.zero;
            PreferFastWalk = false;
            if (Rb != null)
            {
                Rb.velocity = Vector3.zero;
                Rb.angularVelocity = Vector3.zero;
            }

            // 7. 站位与朝向复位。流程层若要把角色放到别处，在 ResetAll() 之后覆盖即可。
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
            Physics.SyncTransforms();

            // 8. 状态机回地面待机
            if (MainStateMachine != null)
                EnterGrounded("encounter reset: grounded idle");

            // 9. 推事件：这些数值是直接改的，不走 TakeDamage 那条结算路径，
            //    不主动通知的话 UI 会停在上一场的空血条 / 满架势条上
            if (Config != null)
            {
                CombatEventBus.TriggerHPChanged(this, CurrentHP, Config.MaxHP);
                CombatEventBus.TriggerPostureChanged(this, CurrentPosture, Config.MaxPosture);
            }
            CombatEventBus.TriggerFinisherOpportunityChanged(this, false);
        }

        private void Update()
        {
            // 1. 每帧更新物理环境感知 (例如是否接地)
            // 这样做的好处是：所有 State 只需要读取 body.IsGrounded，不需要在各自内部写射线检测
            UpdateEnvironmentalChecks();

            // 1.5 坠出地图兜底：地面是整块 MeshCollider、边缘无围栏，
            // 走位/垫步/击退越过边缘后只剩重力会无限下坠，必须传回安全点
            UpdateFallSafety();

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

        private void FixedUpdate()
        {
            // 跳跃冲量等物理帧再写（Animator 是 Animate Physics），消费逻辑在 Locomotion
            Loco.ApplyPendingJumpVelocity();
        }

        // 起跳：只打垂直初速度。根运动保持开着，由 OnAnimatorMove 丢掉 Y、保留 XZ。（实现已迁入 Locomotion）
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

            if (CombatManager.Instance == null) return;
            // TODO(多 Boss)：这里取的是「主对手」。多 Boss 时应改为遍历所有对手或取最近的那个。
            // 这属于「找对象」而非「判身份」，不在本次阵营改造范围内。
            CharacterBody other = CombatManager.Instance.BossRef;
            if (other == null || other.IsFinisherLocked) return;

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

        public bool UsesAirState => Config != null && Config.UseAirState;

        // 01-states：根运动走 OnAnimatorMove。写了这个回调，Unity 不再自动套 Root，必须自己加。
        // 空中丢掉 Root.y，否则跳跃高度被动画钉住；XZ 必须吃，否则中心不动、贴图自己滑，切 Idle 会瞬移回去。
        private void OnAnimatorMove()
        {
            if (Animator == null || Rb == null || !UseRootMotion) return;

            Vector3 delta = Animator.deltaPosition;
            if (IsAirborne)
            {
                delta.y = 0f;
            }

            transform.position += delta;

            // 旋转优先级（Hold > 攻击转向 > Root 抑制 > 动画增量）决策已迁入转向模块。
            // 顺序保持原样：先写位置增量，再决定旋转，最后统一同步刚体。
            Facing.ApplyRootRotation(Animator);

            Rb.position = transform.position;
            Rb.rotation = transform.rotation;
        }

        // 接收大脑 (Brain) 传来的指令
        public bool TryExecuteCommand(ICommand cmd)
        {
            if (IsFinisherLocked) return true;

            // 将大脑的指令直接喂给主状态机。
            // 返回 true  表示：指令被某个状态 (父状态或子状态) 成功消耗；
            // 返回 false 表示：当前层级下的所有状态都拒收这个指令。
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

            if (EnableStateDebugLog)
            {
                Debug.Log(
                    $"[HFSM] {name}: {previousPath} -> {currentPath} " +
                    $"({LastStateChangeReason})", this);
            }
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

            if (EnableStateDebugLog)
            {
                Debug.Log(
                    $"[HFSM] {name}: {previousPath} -> {currentPath} " +
                    $"({LastStateChangeReason})", this);
            }
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
            // 重建时叶子以 null 父级创建：与旧直接重建语义一致（见 BossReviveBackoffState）
            EnterGrounded(factory(null), reason + ": rebuild parent");
        }

        // 换到"格挡 / 垫步"叶子：三个受击/起身状态（MidToGuard / Standing / StaggerBroken）
        // 共用的取消配方，收敛成一处
        public bool TryChangeToDeflectOrDodge(bool toDeflect)
        {
            return TryChangeGroundedSubState(g => toDeflect
                ? (BaseState)new DeflectState(this, g)
                : new DodgeState(this, g));
        }

        // 喝药重箭等打断：命中段会拒收 AttackCommand，防御态会吞掉命令却不出招，必须强切。
        public bool StartAttack(AttackConfig config, bool interruptCurrent = false)
        {
            if (config == null) return false;
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

        // --- 物理环境检测 / 坠图保护：实现已迁入 Locomotion（含迟滞防抖与安全点记录），
        //     这里保留同名私有转发，Update/Start 的调用点与顺序零改动 ---
        private void UpdateEnvironmentalChecks() => Loco.UpdateEnvironmentalChecks();

        private void UpdateFallSafety() => Loco.UpdateFallSafety();

        // --- 转向接口：实现已迁入 FacingController，签名不变，全项目调用方零改动 ---
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

        // 摇杆输入 → 世界移动方向（相机相对，原神式）：实现已迁入 Locomotion
        // 输入先经相机水平朝向变换，W = 远离镜头、A/D = 屏幕左右，与相机摆放无关
        public Vector3 InputToWorldDir(Vector2 inputDir) => Loco.InputToWorldDir(inputDir);

        // AttackCommand 不携带配置（用户决策 9）：有 ActiveAttack 用覆盖，否则玩家走 LightAttack。
        public AttackConfig GetAttackConfig()
        {
            return ActiveAttack != null ? ActiveAttack : LightAttack;
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

        // ===== 抖刀惩罚（M4）/ 弹反窗口 =====
        // 记账与窗口缩放已迁入 DeflectMemory，这里只转发。

        public void RegisterDeflectPress() => Deflect.RegisterDeflectPress();

        public float LastDeflectCancelTime => Deflect.LastDeflectCancelTime;

        public void NotifyDeflectCancel() => Deflect.NotifyDeflectCancel();

        // 当前生效的弹反窗口（已计入抖刀惩罚）
        public float GetDeflectWindow() => Deflect.GetDeflectWindow();

        // 锁定四向移动参数：有 MoveZ 用 MoveZ，否则回退 MoveY。实现已迁入 Locomotion
        public void SetMoveStrafe(float x, float z, bool instant) => Loco.SetMoveStrafe(x, z, instant);

        // 被完美弹反后的硬直入口（M4）：物理强制覆写，不走 Command，直接切顶层状态机。
        // 普通弹反只用于玩家被 Boss 弹开。飞舟互弹时双方都走这里播 Deflected_Boat。
        // ParriedState 装在 GroundedState 内（通过带初始子状态的构造），顶层结构不变。
        public void ForceParryStun(string animName = null, bool armKengeki = true)
        {
            IsAttacking = false;
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
            IsAttacking = false;
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

        // ===== M7 Boss 被动防御（只狼攻防转换）=====
        // 只狼模式：Boss 非攻击/非硬直时被玩家命中 → 强制格挡判定；连续格挡达阈值后
        // 升级为完美弹反（弹开玩家、抢回主动权）。由 BTBrain 启动时对 Boss 开启。
        // 计数与升级判定已迁入 DeflectMemory；本字段是 Inspector 配置（prefab 已保存该值），保留原位。
        public bool EnablePassiveDeflect;

        public bool TryPassiveDeflect(HitData hit) => Deflect.TryPassiveDeflect(hit);

        // 架势崩解硬直入口（M9）：玩家 = 击飞倒地（不被处决）；Boss = 处决窗口（红点）。
        public void ForcePostureBroken(PostureBreakSource source = PostureBreakSource.Attack)
        {
            IsAttacking = false;
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

        // ===== 武器接口：实现已迁入 WeaponController，签名不变，全项目调用方零改动 =====

        // 开启武器判定（M3/M8）：攻击状态/动画事件调用。绑定本招式的伤害配置
        public void EnableWeaponHit(AttackConfig config) => WeaponCtrl.EnableWeaponHit(config);

        // 关闭武器判定（M3/M8）
        public void DisableWeaponHit() => WeaponCtrl.DisableWeaponHit();

        // 时间轴 arrowCues 到点由 AttackState 调用。伤害读招式表该支出箭，不读烘焙 AttackConfig。
        public void SpawnArrow(int cueIndex = 0) => WeaponCtrl.SpawnArrow(cueIndex);

        public Vector3 GetProjectileAimPoint() => WeaponCtrl.GetProjectileAimPoint();

        // ===== 战斗数值接口：实现已迁入 CombatStats，签名不变，全项目调用方零改动 =====

        // 受击结算：扣血 + 涨架势。由 ReceiveHit（物理）或外部调用。
        public void TakeDamage(int healthDmg, float postureDmg, CharacterBody instigator = null)
            => Combat.TakeDamage(healthDmg, postureDmg, instigator);

        // 累计架势。防御/弹反也会加少量（M9 细则接），这里统一入口。
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

        // 喝葫芦（M16）：有次数就能喝；满血也播动画、扣次数，HP 加完仍封顶
        public bool UseGourd() => Combat.UseGourd();

        // 对手已倒地：打断当前攻击回 Idle，给 Boss 改走位用。不经过 Command。
        public void CancelAttackToIdle()
        {
            if (IsFinisherLocked || IsPostureBroken || IsDefeated) return;
            IsAttacking = false;
            AttackUninterruptible = false;
            IsAttackRecoveryOpen = false;
            ActiveAttack = null;
            DisableWeaponHit();
            ClearSteerYaw();
            SetSuppressRootYaw(false);
            EnterGrounded("boss: cancel attack to idle");
        }

        // 死亡判定（M14）：判定与事件已迁入 CombatStats.HandleDeath，
        // 状态切换（DeadState 回生/真死）经 onDeath 委托由本类编排。

        // 复活（M14）：回满血 + 架势清零（用户决策：直接回满），扣除一次复活次数
        public void Revive() => Combat.Revive();

        // 处决清一条命（M10 用）：扣命 → 没命了发胜利事件；还有命 → 重置架势回满血接着打
        public void ClearLife() => Combat.ClearLife();

        // 崩解标志/架势条清掉，不切状态。倒地中再挨刀时先清再进受击，避免闪 Idle。
        public void ClearPostureBreak(float remainingRatio = 0f)
        {
            Combat.ClearPostureBreak(remainingRatio);
        }

        // 崩解超时恢复（M9）：架势清空 + 崩解解除，不扣命（与处决清命区分）
        public void RecoverFromBreak(float remainingRatio = 0f)
        {
            ClearPostureBreak(remainingRatio);
            EnterGrounded("combat: recover from posture break");
        }

        // 动画事件可选入口：正常结算改由 FinisherState 在动画结束时驱动。
        public void ExecuteFinisher()
        {
            CombatManager.Instance?.ExecuteFinisher(this);
        }

        // 接收外界物理碰撞传来的打击
        public void ReceiveHit(CharacterBody attacker, int healthDmg, float postureDmg, Vector3 hitPoint,
                               bool isPerilous = false, PerilousType perilousType = PerilousType.None,
                               float knockback = 0f,
                               HitGrade hitGrade = HitGrade.Light,
                               bool isProjectile = false)
        {
            if (IsFinisherLocked || IsDefeated) return;

            // 打包成值类型，供状态机做层级查询（M1）
            HitData hit = new HitData
            {
                attacker = attacker,
                healthDmg = healthDmg,
                postureDmg = postureDmg,
                hitPoint = hitPoint,
                isPerilous = isPerilous,           // M17 危字攻击标记
                perilousType = perilousType,
                knockback = knockback,             // 受击表现接口：击退强度
                hitGrade = hitGrade,
                isProjectile = isProjectile
            };

            bool alreadyBroken = IsPostureBroken;

            // 0. M7 Boss 被动防御（只狼攻防转换）：可防御时命中强制转格挡判定。
            //    先于状态拦截，保证玩家连打压制时 Boss 始终先进入防御反应，而不是裸受击。
            if (TryPassiveDeflect(hit)) return;

            // 1. 先问当前状态层级：能拦截吗？（防御/垫步/识破/受击期间）
            //    MainStateMachine 顶层只装 HierarchicalState，OnHitReceived 会逐层下钻到叶子状态
            //    （如 DeflectState/DodgeState/MikiriCounterState），不用直接判顶层状态类型（那条路永远 false）
            if (MainStateMachine.CurrentState != null &&
                MainStateMachine.CurrentState.OnHitReceived(hit))
            {
                return; // 被状态拦截了（盾反成功 / 垫步无敌 / 识破 / 二次受击）
            }

            // 2. 没拦住 → 伤害/架势结算（M2）：扣血 + 涨架势 + 死亡判定
            TakeDamage(healthDmg, postureDmg, attacker);
            // 玩家挨实锤打断 Boss 连弹计数，下次要重新弹满 2 次才可能 JumpThrust。
            if (HitReactionUtil.IsPlayer(this) && attacker != null)
                attacker.ResetConsecutiveTimesParried();

            if (CurrentHP <= 0) return;

            if (hit.isPerilous && hit.perilousType == PerilousType.Grab
                && HitReactionUtil.IsPlayer(this)
                && CombatManager.Instance != null
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
