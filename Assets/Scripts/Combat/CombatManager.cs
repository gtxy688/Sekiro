using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 命中判定中间层的门面（Facade）。方案 B 决策不变：
    // Hitbox 扫到 Hurtbox → 报告这里 → 转 CombatResolver → 调 target.ReceiveHit。
    //
    // 历史：这里曾经同时装着四类互不相关的职责——无状态结算、有状态成对演出、
    // 时间尺度（顿帧）、场景装配，431 行，外加 8 个瞬时状态字段挂在单例上。
    //
    // 已经分出去的：
    //   结算 → CombatResolver（无状态，多 Boss 时一行都不用改）
    //   演出 → DuelDirector（有状态，但状态属于"一场演出"对象，不再是单例上的散落字段）
    //   时间 → TimeScaleController（顿帧的申报与执行都在那里，本类不再碰 Time.timeScale）
    //
    // 本类现在剩下的活（别被"只是个门面"骗了，它实际干五件事）：
    //   1. 门面转发 —— 上面三类的对外 API 一字未改，老调用点零改动
    //   2. 参与者解析 —— EncounterScope 优先，回退到 PlayerRef / BossRef 序列化字段
    //   3. 启动校验 —— 引用拖全了没、阵营配对没
    //   4. 依赖自装配 —— resolver / director 留空就自动补，旧场景打开即用
    //   5. 场景装配 —— 忽略角色互撞 + 空气墙。这条本不属于这里，见下方 TODO
    //
    // 为什么保留门面：全项目仍有 30 余处 `CombatManager.Instance.X` 调用点，分布在十几个文件
    // （数字随迁移推进而下降，想看当前值：
    //   grep -rn "CombatManager\.Instance" --include=*.cs Assets/Scripts）。
    // 一次性全迁风险过高，也不是这个阶段该做的事。门面保证既有调用点零改动、行为零变化；
    // 新代码（复战流程）直接依赖 CombatResolver / DuelDirector，老代码按需逐步迁移。
    //
    // TODO（后续，不是现在）：
    //   - IgnoreCharacterPhysics / ArenaBoundary 属场景装配，应移到独立的 ArenaSetup
    public class CombatManager : MonoBehaviour, ICombatResettable
    {
        public static CombatManager Instance { get; private set; }

        [Header("内部组件（留空则 Awake 自动补齐）")]
        public CombatResolver resolver;
        public DuelDirector director;

        [Header("拼刀参数")]
        [Tooltip("过渡期保留：Start 时同步给 CombatResolver，避免 Inspector 上已调过的值丢失。新代码请直接改 CombatResolver 上的值。")]
        public float clashPostureMultiplier = 1f;

        [Header("打击感")]
        [Tooltip("过渡期配置入口：Start 时同步给 TimeScaleController，避免 Inspector 上已调过的值丢失。真正的顿帧执行在 TimeScaleController，本类不写 Time.timeScale。")]
        public bool enableHitStop = true;   // 命中/弹反顿帧开关
        public float hitStopDuration = 0.05f;

        [Header("处决（M10）")]
        [Tooltip("过渡期保留：Start 时同步给 DuelDirector，同上。")]
        public float finisherRange = 2f;

        [Tooltip("场景里拖玩家。若放了 EncounterScope，则以其 Player 为准，本字段自动退为回退值。")]
        public CharacterBody PlayerRef;

        [Tooltip("场景里拖 Boss。若放了 EncounterScope，则以其主对手为准。多 Boss 请填 EncounterScope.Opponents。")]
        public CharacterBody BossRef;

        // 运行时实际参与者：EncounterScope 优先，回退到序列化字段。
        // 复战换 Boss 时只改 EncounterScope，CombatManager 与其余调用点都不用动。
        public CharacterBody ActivePlayer
        {
            get
            {
                CharacterBody fromScope = EncounterScope.Current != null
                    ? EncounterScope.Current.Player : null;
                return fromScope != null ? fromScope : PlayerRef;
            }
        }

        public CharacterBody ActiveBoss
        {
            get
            {
                CharacterBody fromScope = EncounterScope.Current != null
                    ? EncounterScope.Current.PrimaryOpponent : null;
                return fromScope != null ? fromScope : BossRef;
            }
        }

        private void Awake()
        {
            // 单例防重
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 自动补齐依赖：旧场景 / prefab 打开即用，不需要手工挂组件
            if (resolver == null) resolver = GetComponent<CombatResolver>();
            if (resolver == null) resolver = gameObject.AddComponent<CombatResolver>();

            if (director == null) director = GetComponent<DuelDirector>();
            if (director == null) director = gameObject.AddComponent<DuelDirector>();
        }

        private void Start()
        {
            // 复位契约的边界对象先就位：下面 ValidateFinisherRefs 与 ActivePlayer/ActiveBoss 都要读它。
            // 场景里没挂就自动创建一个空的（理由见 EncounterScope.Ensure 注释）。
            EncounterScope scope = EncounterScope.Ensure();

            // 放在 Start 而非 Awake：它依赖 EncounterScope.Current，
            // 而跨组件的 Awake 顺序不确定，Start 时所有 Awake 都已跑完。
            ValidateFinisherRefs();
            ValidateFaction();

            // 过渡期参数同步：把 Inspector 上可能已调过的旧值带给新组件
            if (resolver != null) resolver.clashPostureMultiplier = clashPostureMultiplier;
            if (director != null) director.finisherRange = finisherRange;

            // 顿帧参数同上。这里只是把配置喂过去，顿帧的实际执行归 TimeScaleController。
            TimeScaleController.EnableHitStop = enableHitStop;
            TimeScaleController.HitStopDuration = hitStopDuration;

            // 只注入对手，不注入玩家：谁能发起处决是阵营规则（DuelDirector.CanInitiateFinisher），
            // 不该由这里拖一个引用来决定。注入玩家就等于把规则焊死在实例上。
            if (director != null) director.Configure(ActiveBoss);

            scope?.Register(this);

            // 身体胶囊同时承担 Hurtbox 扫描。互撞冲量会挤开站位，所以忽略 PhysX 互撞；
            // 玩家仍在 CharacterBody.LateUpdate 里做胶囊分离，不会穿过 Boss。
            IgnoreCharacterPhysics(ActivePlayer, ActiveBoss);

            // 擂台外圈空气墙：走位/垫步/击退都出不了场（坠落兜底仍在 CharacterBody）
            ArenaBoundary.Ensure();
        }

        private void OnDestroy()
        {
            EncounterScope.Current?.Unregister(this);
            if (Instance == this)
                Instance = null;
        }

        private void ValidateFinisherRefs()
        {
            // 参与者由 EncounterScope 负责时，不再要求手工拖引用。
            // 判 HasParticipants 而非判 Current != null：
            // 自动兜底出来的是空壳，它不提供参与者，此时仍要按旧路径检查 PlayerRef/BossRef，
            // 否则「忘了拖引用」这个配置错误会被兜底逻辑一起吞掉。
            if (EncounterScope.Current != null && EncounterScope.Current.HasParticipants) return;

            if (PlayerRef == null)
                Debug.LogError("CombatManager.PlayerRef 未绑定，处决无法发起。");
            if (BossRef == null)
                Debug.LogError("CombatManager.BossRef 未绑定，处决无法发起。");
            if (PlayerRef != null && PlayerRef == BossRef)
                Debug.LogError("CombatManager.PlayerRef 与 BossRef 指向同一角色，处决已禁用。");
        }

        // 阵营校验。Faction 是新增字段，旧 prefab / 旧场景对象会落到默认值 Enemy，
        // 而玩家被当成敌人的后果是**静默**的：不崩、不报错，只是悄悄失去
        // 专属受击表现、无敌血、玩家侧音效与特效等一整批按身份分支的逻辑。
        // 这类错误最难查，所以开局就喊出来。
        private void ValidateFaction()
        {
            CharacterBody player = ActivePlayer;
            if (player != null && player.Faction != Faction.Player)
            {
                Debug.LogError(
                    $"[CombatManager] 玩家角色「{player.name}」的 Faction 是 {player.Faction}，应当是 Player。\n" +
                    "修法：选中它，在 Inspector 的 CharacterBody 组件上把 Faction 改成 Player。\n" +
                    "不改不会崩，但玩家会静默失去专属受击表现、无敌血、玩家侧音效与特效等按身份分支的逻辑。",
                    player);
            }

            // Boss 必须是 Enemy。这条以前由 DuelDirector 里的 `initiator == bossRef` 兜着；
            // 改成阵营判断后那行删了——它在逻辑上已被 CanInitiateFinisher 覆盖，
            // 但覆盖的前提是「Boss 确实不是 Player」。前提得有人验，否则 Boss 会获得处决资格。
            CharacterBody boss = ActiveBoss;
            if (boss != null && boss.Faction == Faction.Player)
            {
                Debug.LogError(
                    $"[CombatManager] Boss 角色「{boss.name}」的 Faction 是 Player，应当是 Enemy。\n" +
                    "修法：选中它，在 Inspector 的 CharacterBody 组件上把 Faction 改成 Enemy。\n" +
                    "不改的话 Boss 会取得处决资格，可能对自己或玩家发动处决演出。",
                    boss);
            }
        }

        // ===== 命中结算：转发 CombatResolver，顿帧向 TimeScaleController 申报 =====
        //
        // 为什么顿帧的「触发点」还在这里，但「执行」不在：
        // 顿帧原本是命中的固有表现，触发时机与命中判定强绑定。
        // 若改成让表现层去订阅 CombatEventBus.OnTakeDamage，被格挡/弹反的攻击不掉血、
        // 不发该事件，行为就变了——那是需求变更，不是重构，本次不做。
        // 所以这里保留触发点，但只做「申报一次顿帧」，
        // 实际改 Time.timeScale 的权力已完全交给 TimeScaleController。

        // A 的武器扫到 B 的 Hurtbox
        public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
        {
            if (resolver != null) resolver.ReportHit(hitbox, hurtbox, hitPoint);
            TimeScaleController.HitStop();
        }

        // 箭扫到 Hurtbox。伤害由调用方从招式表解析，不读 Hitbox.Config。
        public void ReportProjectileHit(
            CharacterBody attacker,
            Hurtbox hurtbox,
            Vector3 hitPoint,
            int healthDmg,
            float postureDmg,
            float knockback,
            HitGrade hitGrade)
        {
            if (resolver != null)
                resolver.ReportProjectileHit(attacker, hurtbox, hitPoint,
                    healthDmg, postureDmg, knockback, hitGrade);
            TimeScaleController.HitStop();
        }

        // 双方 Hitbox 相交 → 拼刀：只狼里拼刀双方都涨架势，不打伤害
        public void ReportClash(Hitbox a, Hitbox b, Vector3 point)
        {
            if (resolver != null) resolver.ReportClash(a, b, point);
        }

        // ===== 成对演出：转发 DuelDirector =====

        public bool TryExecuteFinisher(CharacterBody initiator, FinisherKind kind = FinisherKind.Ground)
        {
            return director != null && director.TryExecuteFinisher(initiator, kind);
        }

        public bool TryExecuteAvailableFinisher(CharacterBody initiator)
        {
            return director != null && director.TryExecuteAvailableFinisher(initiator);
        }

        public void ExecuteFinisher(CharacterBody source)
        {
            if (director != null) director.ExecuteFinisher(source);
        }

        public bool IsFinisherResolved(CharacterBody player)
        {
            return director != null && director.IsFinisherResolved(player);
        }

        public void CompleteFinisherSequence(CharacterBody player)
        {
            if (director != null) director.CompleteFinisherSequence(player);
        }

        public bool TryStartGrabThrow(CharacterBody attacker, CharacterBody victim)
        {
            return director != null && director.TryStartGrabThrow(attacker, victim);
        }

        public void CompleteGrabThrow(CharacterBody source)
        {
            if (director != null) director.CompleteGrabThrow(source);
        }

        // ===== 复战重置 =====
        public void ResetForEncounter()
        {
            // 顿帧必须取消：否则上一场残留的计时会在复战开场把时间拨回去。
            // 拨到多少由 TimeScaleController 统一求值，这里只说"取消"。
            TimeScaleController.CancelHitStop();

            if (director != null)
            {
                director.Configure(ActiveBoss);
                director.ResetForEncounter();
            }
        }

        private static void IgnoreCharacterPhysics(CharacterBody a, CharacterBody b)
        {
            if (a == null || b == null) return;

            Collider[] aCols = a.GetComponentsInChildren<Collider>(true);
            Collider[] bCols = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < aCols.Length; i++)
            {
                if (aCols[i] == null) continue;
                for (int j = 0; j < bCols.Length; j++)
                {
                    if (bCols[j] == null) continue;
                    Physics.IgnoreCollision(aCols[i], bCols[j], true);
                }
            }
        }
    }
}
