using UnityEngine;
using System.Collections.Generic;

using ARPG.Audio;
using ARPG.Boss.BehaviourTree;
using ARPG.Combat;
using ARPG.FrameWork.Body;
using ARPG.Mgr;
namespace ARPG.Boss
{

    // Boss 决策入口：准备目标、黑板和行为树，再由 Update 驱动决策。
    // 动作执行交给 CharacterBody；自身只保留 AI 的决策和招式执行进度。
    [RequireComponent(typeof(CharacterBody))]
    public class BTBrain : MonoBehaviour, ICombatResettable
    {
        [Header("目标")]
        // AI 目标独立配置，不会从 CombatManager 的参与者引用中自动注入。
        public Transform PlayerTarget;
        // 用于读取目标的战斗状态，与 PlayerTarget 一起由场景绑定。
        public CharacterBody PlayerBody;

        [Header("追击")]
        public float attackRange = 3.0f;

        [Header("近身走位（选招阶段不罚站）")]
        [Tooltip("绕圈换边周期（秒）")]
        public float roamStrafeDuration = 1.2f;
        [Tooltip("侧向绕圈强度（0-1，越大转得越狠）")]
        public float roamStrafeStrength = 0.8f;
        [Tooltip("朝玩家逼近分量（0-1，<1 不会直接撞上去）")]
        public float roamApproachStrength = 0.35f;

        [Header("主动招间隔")]
        [Tooltip("主动出招结束后强制走位的最短秒数，避免刀刀衔接")]
        public float roamAfterAttack = 2.5f;
        [Tooltip("在最短间隔上再随机加这么多秒，走位节奏不那么机械")]
        public float roamAfterAttackJitter = 1.2f;

        [Header("调试（测试弹反用）")]
        [Tooltip("只近战模式：只放行 Kengeki_Slash / Kengeki_Double 两条贴身交锋平砍，屏蔽弓/后跳/肘击/踢腿/转身/特殊招。配合被动格挡，测弹反后立即反击的纯净环境")]
        public bool MeleeOnly = false;
        [Tooltip("禁用 Boss 主动出招（交锋/喝药重箭/主动抽招），只留追击走位 + 被动防御判定。测试被弹反回合时勾上，Boss 变纯挨打靶")]
        public bool DisableBossAttacks = false;
        [Tooltip("禁用 Boss 被动防御（玩家攻击不再触发强制格挡/计数弹反），测试玩家主动弹反时勾上")]
        public bool DisablePassiveDeflect = false;

        [Header("完整 AI")]
        public BossMoveTable moveTable;

        // MeleeOnly 只保留两条交锋平砍；距离不够时仍由走位节点靠近玩家。
        static readonly HashSet<string> MeleeOnlyMoveIds = new HashSet<string>
        {
            "Kengeki_Slash", "Kengeki_Double"
        };

        private CharacterBody body;
        private Node behaviorTreeRoot;
        private Blackboard blackboard;

        // 白名单属于 Boss 实例，不能写回多个角色共享的招式表；null 表示不过滤。
        private HashSet<string> moveFilter;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        // 必要依赖齐全才建立行为树；缺失时停用 AI，避免每帧在不完整配置上决策。
        private void Start()
        {
            if (PlayerTarget == null)
            {
                Debug.LogError($"{name} 的 BTBrain 缺少 PlayerTarget，AI 已停用。");
                enabled = false;
                return;
            }

            if (PlayerBody == null)
            {
                Debug.LogError($"{name} 的 BTBrain 缺少 PlayerBody，AI 已停用。");
                enabled = false;
                return;
            }

            if (moveTable == null)
            {
                Debug.LogError($"{name} 的 BTBrain 缺少 moveTable，AI 已停用。");
                enabled = false;
                return;
            }

            // 黑板保存本 Boss 的运行时决策数据，与共享的招式表资产分开。
            blackboard = new Blackboard();
            // 角色移动、攻击和射箭也需要自己的目标引用，不能使用玩家的锁定目标。
            body.CombatTarget = PlayerTarget;
            body.MoveUsesWorldDir = true;
            // 被动防御在受击时判定；调试开关只控制是否启用，不需要行为树主动抬刀。
            body.EnablePassiveDeflect = !DisablePassiveDeflect;

            // 调试过滤器属于本 Boss 实例，不修改共享的 moveTable 资产。
            if (MeleeOnly)
                moveFilter = new HashSet<string>(MeleeOnlyMoveIds);

            // 构建节点后提供黑板；Update 才开始评估行为树。
            behaviorTreeRoot = ConstructBehaviorTree();
            behaviorTreeRoot.SetBlackboard(blackboard);
            CombatEventBus.OnRevived += HandlePlayerRevived;

            // 自行确保 Scope 存在，注册不依赖 CombatManager.Start 先执行。
            EncounterScope.Ensure().Register(this);
        }

        // 复战时清除冷却、未完成招式和回生等待，避免沿用上一场的执行进度。
        public void ResetForEncounter()
        {
            blackboard.Clear();

            postRevivePhase = PostRevivePhase.None;
            postReviveTimer = 0f;
            circlingIncapacitatedPlayer = false;

            ResetExecutors();

            // moveFilter 是调试设定，复战时保留。
        }

        private BT_ExecuteMove activeExecutor;
        private BT_ExecuteMove kengekiExecutor;
        private BT_ExecuteMove interruptExecutor;
        private BT_HealPunish healPunish;
        private Node moveToTarget;
        private bool circlingIncapacitatedPlayer;

        private enum PostRevivePhase { None, WaitPlayerStand, DodgeBack }
        private PostRevivePhase postRevivePhase;
        private float postReviveTimer;
        private const float PostReviveStandDelay = 0.35f;

        // 特殊战斗阶段优先拦截，再评估行为树；等待起手时允许单独走位。
        private void Update()
        {
            if (PlayerTarget == null) return;

            if (body.IsDefeated)
            {
                ResetExecutors();
                body.MoveDirection = Vector3.zero;
                body.PreferFastWalk = false;
                return;
            }

            // 忍杀与硬直期间暂停整棵树，避免出招失败后落到走位并转向玩家。
            if (body.IsFinisherLocked || body.IsParried)
            {
                // 树不跑时也要把 Busy 执行器清掉，否则硬直结束会接着播被打断招的下一段。
                ResetExecutors();
                return;
            }

            // 玩家刚倒地时中止当前攻击，之后只走位，直到回生流程允许恢复进攻。
            bool playerIncapacitated = PlayerBody.IsIncapacitatedForBoss;
            if (playerIncapacitated && !circlingIncapacitatedPlayer)
            {
                circlingIncapacitatedPlayer = true;
                ResetExecutors();
                if (body.IsAttacking)
                    body.CancelAttackToIdle();
            }
            else if (!playerIncapacitated)
            {
                circlingIncapacitatedPlayer = false;
            }

            if (postRevivePhase != PostRevivePhase.None)
            {
                UpdatePostRevivePhase();
                return;
            }

            if (playerIncapacitated)
            {
                ResetExecutors();
                moveToTarget.Evaluate();
                return;
            }

            // 开场语音：可以走位，但不要出招。
            if (IsOpeningHold())
            {
                ResetExecutors();
                if (body.IsAttacking)
                    body.CancelAttackToIdle();
                moveToTarget.Evaluate();
                return;
            }

            healPunish?.ArmIfPlayerHealing();
            behaviorTreeRoot.Evaluate();
            // 已选好招但尚未到允许起手时刻，保留走位；被动格挡仍由 ReceiveHit 独立处理。
            if (activeExecutor.IsWaitingForStart
                || kengekiExecutor.IsWaitingForStart
                || interruptExecutor.IsWaitingForStart)
                moveToTarget.Evaluate();
        }

        private static bool IsOpeningHold()
        {
            return BossVoiceDirector.Instance != null && BossVoiceDirector.Instance.IsOpeningHold;
        }

        private void ResetExecutors()
        {
            activeExecutor.ResetMove();
            kengekiExecutor.ResetMove();
            interruptExecutor.ResetMove();
        }

        private void OnDisable()
        {
            CombatEventBus.OnRevived -= HandlePlayerRevived;

            if (EncounterScope.Current != null)
                EncounterScope.Current.Unregister(this);

            if (body == null) return;
            body.MoveDirection = Vector3.zero;
            body.MoveUsesWorldDir = false;
            body.PreferFastWalk = false;
            body.CombatTarget = null;
        }

        // 优先级：禁止行动 → 交锋 → 喝药重箭 → 主动选招 → 走位；被动防御由受击流程处理。
        private Node ConstructBehaviorTree()
        {
            activeExecutor = new BT_ExecuteMove(body, moveTable);
            kengekiExecutor = new BT_ExecuteMove(body, moveTable);
            interruptExecutor = new BT_ExecuteMove(body, moveTable);

            List<Node> children = new List<Node>
            {
                // 崩解 / 忍杀锁定 / 弹反·识破硬直：Success 吃掉本帧，不落到走位。
                new ConditionNode(() => body.IsPostureBroken || body.IsFinisherLocked || body.IsParried)
            };

            if (!DisableBossAttacks)
            {
                children.Add(new BT_Kengeki(
                    body, moveTable, PlayerTarget, kengekiExecutor, PlayerBody, moveFilter));
                // 喝药惩罚直接查找重箭，不经过白名单，因此纯近战调试时不挂此节点。
                if (!MeleeOnly)
                {
                    healPunish = new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor);
                    children.Add(healPunish);
                }
                children.Add(new BT_PickActive(
                    body, moveTable, PlayerTarget, activeExecutor, PlayerBody, moveFilter,
                    roamAfterAttack, roamAfterAttackJitter));
            }

            moveToTarget = new BT_MoveToTarget(body, PlayerTarget, attackRange,
                roamStrafeDuration, roamStrafeStrength, roamApproachStrength);
            children.Add(moveToTarget);

            return new Selector(children);
        }

        private void HandlePlayerRevived(CharacterBody player)
        {
            if (player != PlayerBody) return;
            postRevivePhase = PostRevivePhase.WaitPlayerStand;
            postReviveTimer = 0f;
            circlingIncapacitatedPlayer = false;
            ResetExecutors();
            if (body.IsAttacking)
                body.CancelAttackToIdle();
        }

        // 等玩家起身并留出缓冲，再后撤一步，最后恢复招间走位。
        private void UpdatePostRevivePhase()
        {
            ResetExecutors();

            switch (postRevivePhase)
            {
                case PostRevivePhase.WaitPlayerStand:
                    HoldFacePlayer();
                    body.MoveDirection = Vector3.zero;
                    if (PlayerBody.IsReviving)
                        return;

                    postReviveTimer += Time.deltaTime;
                    if (postReviveTimer < PostReviveStandDelay)
                        return;

                    BeginReviveBackoff();
                    postRevivePhase = PostRevivePhase.DodgeBack;
                    postReviveTimer = 0f;
                    return;

                case PostRevivePhase.DodgeBack:
                    if (IsInReviveBackoff())
                    {
                        postReviveTimer += Time.deltaTime;
                        return;
                    }

                    postRevivePhase = PostRevivePhase.None;
                    ArmPostReviveRoamGap();
                    return;
            }
        }

        private void HoldFacePlayer()
        {
            if (PlayerTarget == null) return;
            Vector3 to = PlayerTarget.position - body.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            body.SnapYaw(to);
        }

        private void BeginReviveBackoff()
        {
            // 统一入口负责复用或重建地面父状态，AI 不直接操作顶层状态机。
            body.ForceChangeGroundedSubState(
                g => new BossReviveBackoffState(body, g),
                "boss: revive backoff");
        }

        private bool IsInReviveBackoff()
        {
            if (!body.IsGroundedTop)
                return postReviveTimer < 0.6f;
            return body.IsInGroundedSubState<BossReviveBackoffState>();
        }

        private void ArmPostReviveRoamGap()
        {
            float gap = roamAfterAttack + Random.Range(0f, Mathf.Max(0f, roamAfterAttackJitter));
            blackboard.ActiveGapDuration = gap;
            blackboard.SetCooldown("active_gap");
        }
    }

}
