using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 战斗服务的统一入口：Awake 补齐组件，Start 解析参与者、校验配置并完成场景装配。
    // 命中交给 CombatResolver，成对演出交给 DuelDirector，顿帧交给 TimeScaleController。
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

        [Header("处决")]
        [Tooltip("过渡期保留：Start 时同步给 DuelDirector，同上。")]
        public float finisherRange = 2f;

        [Tooltip("场景里拖玩家。若放了 EncounterScope，则以其 Player 为准，本字段自动退为回退值。")]
        public CharacterBody PlayerRef;

        [Tooltip("场景里拖 Boss。若放了 EncounterScope，则以其主对手为准。多 Boss 请填 EncounterScope.Opponents。")]
        public CharacterBody BossRef;

        // 每次读取当前参与者：优先用 Scope 对应引用，为空才回退到手工绑定字段。
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

            // 优先保留 Inspector 指定的依赖；未指定时找同物体组件，仍没有才添加。
            if (resolver == null) resolver = GetComponent<CombatResolver>();
            if (resolver == null) resolver = gameObject.AddComponent<CombatResolver>();

            if (director == null) director = GetComponent<DuelDirector>();
            if (director == null) director = gameObject.AddComponent<DuelDirector>();
        }

        private void Start()
        {
            // 先取得本场战斗范围，后续参与者解析和复位登记都要使用它。
            EncounterScope scope = EncounterScope.Ensure();

            // 等初始活动对象的 Awake 完成后再校验，避免 Scope 尚未设置 Current。
            ValidateFinisherRefs();
            ValidateFaction();

            // 将本组件保留的 Inspector 参数同步到实际执行组件，避免已有场景配置丢失。
            resolver.clashPostureMultiplier = clashPostureMultiplier;
            director.finisherRange = finisherRange;

            // 这里只提供顿帧配置，时间尺度的实际修改由 TimeScaleController 负责。
            TimeScaleController.EnableHitStop = enableHitStop;
            TimeScaleController.HitStopDuration = hitStopDuration;

            // 演出需要当前对手引用；发起处决的资格由 DuelDirector 的阵营规则判断。
            director.Configure(ActiveBoss);

            scope.Register(this);

            // 忽略双方 Collider 的 PhysX 互撞，避免冲量挤开站位；玩家在 LateUpdate 做身体分离。
            IgnoreCharacterPhysics(ActivePlayer, ActiveBoss);

            // 确保擂台外圈有边界；角色自身另有坠落保护。
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
            // 自动创建的空 Scope 不提供参与者，仍需校验回退的 PlayerRef / BossRef。
            if (EncounterScope.Current != null && EncounterScope.Current.HasParticipants) return;

            if (PlayerRef == null)
                Debug.LogError("CombatManager.PlayerRef 未绑定，处决无法发起。");
            if (BossRef == null)
                Debug.LogError("CombatManager.BossRef 未绑定，处决无法发起。");
            if (PlayerRef != null && PlayerRef == BossRef)
                Debug.LogError("CombatManager.PlayerRef 与 BossRef 指向同一角色，处决已禁用。");
        }

        // 开局校验参与者阵营，避免配置错误悄悄改变玩家表现或处决资格。
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

            // Boss 若误设为 Player，会取得玩家侧的处决资格。
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

        // ===== 命中结算 =====
        // 命中转交 Resolver 后申报顿帧，格挡或弹反也能触发；时间尺度由 TimeScaleController 统一修改。

        // A 的武器扫到 B 的 Hurtbox
        public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
        {
            resolver.ReportHit(hitbox, hurtbox, hitPoint);
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
            resolver.ReportProjectileHit(attacker, hurtbox, hitPoint,
                healthDmg, postureDmg, knockback, hitGrade);
            TimeScaleController.HitStop();
        }

        // 双方 Hitbox 相交 → 拼刀：只狼里拼刀双方都涨架势，不打伤害
        public void ReportClash(Hitbox a, Hitbox b, Vector3 point)
        {
            resolver.ReportClash(a, b, point);
        }

        // ===== 成对演出：转发 DuelDirector =====

        public bool TryExecuteFinisher(CharacterBody initiator, FinisherKind kind = FinisherKind.Ground)
        {
            return director.TryExecuteFinisher(initiator, kind);
        }

        public bool TryExecuteAvailableFinisher(CharacterBody initiator)
        {
            return director.TryExecuteAvailableFinisher(initiator);
        }

        public bool HasAvailableFinisher(CharacterBody initiator)
        {
            return director.HasAvailableFinisher(initiator);
        }

        public void ExecuteFinisher(CharacterBody source)
        {
            director.ExecuteFinisher(source);
        }

        public bool IsFinisherResolved(CharacterBody player)
        {
            return director.IsFinisherResolved(player);
        }

        public void CompleteFinisherSequence(CharacterBody player)
        {
            director.CompleteFinisherSequence(player);
        }

        public bool TryStartGrabThrow(CharacterBody attacker, CharacterBody victim)
        {
            return director.TryStartGrabThrow(attacker, victim);
        }

        public void CompleteGrabThrow(CharacterBody source)
        {
            director.CompleteGrabThrow(source);
        }

        // ===== 复战重置 =====
        public void ResetForEncounter()
        {
            // 取消旧顿帧，避免上一场的计时影响复战开场。
            TimeScaleController.CancelHitStop();

            director.Configure(ActiveBoss);
            director.ResetForEncounter();
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
