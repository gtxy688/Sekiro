using System.Collections;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 命中判定中间层的门面（Facade）。方案 B 决策不变：Hitbox 扫到 Hurtbox → 报告这里 → 调 target.ReceiveHit。
    //
    // 历史：这里曾经同时装着三类互不相关的职责——无状态结算、有状态成对演出、场景装配，
    // 431 行，外加 8 个瞬时状态字段挂在单例上。
    //
    // 现在：
    //   结算 → CombatResolver（无状态，多 Boss 时一行都不用改）
    //   演出 → DuelDirector（有状态，但状态属于"一场演出"对象，不再是单例上的散落字段）
    //   本类 → 只做转发 + 顿帧 + 兼容层
    //
    // 为什么保留门面：全项目 41 处 `CombatManager.Instance.X` 调用点，分布在 17 个文件。
    // 一次性全迁风险过高，也不是这个阶段该做的事。门面保证既有调用点零改动、行为零变化；
    // 新代码（复战流程）直接依赖 CombatResolver / DuelDirector，老代码按需逐步迁移。
    //
    // TODO（后续，不是现在）：
    //   - HitStop 改的是全局 Time.timeScale，应收敛到独立的时间表现服务
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
        public bool enableHitStop = true;   // 命中/弹反顿帧开关
        public float hitStopDuration = 0.05f;

        [Header("处决（M10）")]
        [Tooltip("过渡期保留：Start 时同步给 DuelDirector，同上。")]
        public float finisherRange = 2f;

        [Tooltip("场景里拖玩家。若放了 EncounterScope，则以其 Player 为准，本字段自动退为回退值。")]
        public CharacterBody PlayerRef;

        [Tooltip("场景里拖 Boss。若放了 EncounterScope，则以其主对手为准。多 Boss 请填 EncounterScope.Opponents。")]
        public CharacterBody BossRef;

        private Coroutine hitStopRoutine;

        // 运行时实际参与者：EncounterScope 优先，回退到序列化字段。
        // 复战换 Boss 时只改 EncounterScope，CombatManager 与 41 处调用点都不用动。
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

            // 过渡期参数同步：把 Inspector 上可能已调过的旧值带给新组件
            if (resolver != null) resolver.clashPostureMultiplier = clashPostureMultiplier;
            if (director != null) director.finisherRange = finisherRange;

            // 参与者注入：DuelDirector 自己不认识"玩家"，由这里告诉它
            if (director != null) director.Configure(ActivePlayer, ActiveBoss);

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

        // ===== 命中结算：转发 CombatResolver，顿帧留在本层 =====

        // A 的武器扫到 B 的 Hurtbox
        public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
        {
            if (resolver != null) resolver.ReportHit(hitbox, hurtbox, hitPoint);
            HitStop();
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
            HitStop();
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

        // ===== 顿帧（打击感）：短暂减速全局时间，营造命中重量感 =====
        // TODO：动的是全局 Time.timeScale，与 GamePause 抢同一个变量（见下方回写逻辑）。
        // 正解是收敛到独立的时间表现服务。属于"缓做"，先保持行为不变。
        public void HitStop(float duration = -1f)
        {
            if (!enableHitStop || GamePause.IsPaused) return;
            if (duration < 0f) duration = hitStopDuration;

            if (hitStopRoutine != null)
                StopCoroutine(hitStopRoutine);
            hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
        }

        private IEnumerator HitStopRoutine(float duration)
        {
            if (GamePause.IsPaused)
            {
                hitStopRoutine = null;
                yield break;
            }

            Time.timeScale = 0.05f;
            yield return new WaitForSecondsRealtime(duration);
            // 顿帧期间若打开暂停，结束时保持冻结，不要拨回 1
            Time.timeScale = GamePause.IsPaused ? 0f : 1f;
            hitStopRoutine = null;
        }

        // ===== 复战重置 =====
        public void ResetForEncounter()
        {
            // 顿帧协程必须停掉：否则上一场残留的协程会在复战开场把 timeScale 拨回去
            if (hitStopRoutine != null)
            {
                StopCoroutine(hitStopRoutine);
                hitStopRoutine = null;
            }
            Time.timeScale = GamePause.IsPaused ? 0f : 1f;

            if (director != null)
            {
                director.Configure(ActivePlayer, ActiveBoss);
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
