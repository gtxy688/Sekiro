using UnityEngine;
using System.Collections.Generic;

using ARPG.Audio;
using ARPG.Boss.BehaviourTree;
using ARPG.Combat;
using ARPG.FrameWork.Body;
using ARPG.Mgr;
namespace ARPG.Boss
{

    [RequireComponent(typeof(CharacterBody))]
    public class BTBrain : MonoBehaviour, ICombatResettable
    {
        [Header("目标")]
        public Transform PlayerTarget;
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
        [Tooltip("只近战模式：屏蔽弓/后跳/特殊招，Boss 只用近战普通攻击 + 被动格挡/弹反。测弹反后立即反击的纯净环境")]
        public bool MeleeOnly = false;
        [Tooltip("禁用 Boss 主动出招（交锋/喝药重箭/主动抽招），只留追击走位 + 被动防御判定。测试被弹反回合时勾上，Boss 变纯挨打靶")]
        public bool DisableBossAttacks = false;
        [Tooltip("禁用 Boss 被动防御（玩家攻击不再触发强制格挡/计数弹反），测试玩家主动弹反时勾上")]
        public bool DisablePassiveDeflect = false;

        [Header("完整 AI")]
        public BossMoveTable moveTable;

        // MeleeOnly 调试白名单：只放行近战普通挥砍，屏蔽弓/后跳/特殊招。
        static readonly HashSet<string> MeleeOnlyMoveIds = new HashSet<string>
        {
            "Slash_Double", "Slash_Heavy", "Slash_SpinElbow", "Slash_StepTurn", "Kick",
            "Kengeki_Slash", "Kengeki_Double"
        };

        private CharacterBody body;
        private Node behaviorTreeRoot;
        private Blackboard blackboard;

        // 实例级招式过滤（null = 不过滤）。只影响本 Boss，绝不写回 BossMoveTable 资产——
        // SO 是共享资产，写回去会串到所有引用该表的 Boss（复战 / 多 Boss 必炸）。
        private HashSet<string> moveFilter;

        private void Awake()
        {
            body = GetComponent<CharacterBody>();
        }

        private void Start()
        {
            if (PlayerTarget == null)
            {
                Debug.LogError($"{name} 的 BTBrain 缺少 PlayerTarget，AI 已停用。");
                enabled = false;
                return;
            }

            if (PlayerBody == null && PlayerTarget != null)
            {
                PlayerBody = PlayerTarget.GetComponent<CharacterBody>();
                if (PlayerBody == null)
                    PlayerBody = PlayerTarget.GetComponentInParent<CharacterBody>();
            }

            if (PlayerBody == null)
            {
                Debug.LogError($"{name} 的 BTBrain 无法从 PlayerTarget 找到 CharacterBody，AI 已停用。");
                enabled = false;
                return;
            }

            if (moveTable == null)
            {
                Debug.LogError($"{name} 的 BTBrain 缺少 moveTable，AI 已停用。");
                enabled = false;
                return;
            }

            blackboard = new Blackboard();
            body.CombatTarget = PlayerTarget;
            body.MoveUsesWorldDir = true;
            // M7 被动防御（只狼攻防转换）：玩家命中 Boss 的瞬间做防御判定（普通格挡/计数升级弹反）。
            // 取代旧的"AI 短按防御 + 1.5s 格挡 CD"主动方案，Boss 不再裸受击。
            // 调试：DisablePassiveDeflect 勾上时关闭，Boss 裸吃伤害（测玩家主动弹反链）。
            body.EnablePassiveDeflect = !DisablePassiveDeflect;

            // 调试：MeleeOnly → 白名单只放行近战普通挥砍（弓/后跳/特殊招全部屏蔽），
            // 弹反后的立即反击抽到的也只会是近战刀招，方便验证"弹反 → 反手刀"。
            // 过滤器建在本 Boss 实例上（moveFilter），不写 moveTable 资产。
            if (MeleeOnly)
                moveFilter = new HashSet<string>(MeleeOnlyMoveIds);

            behaviorTreeRoot = ConstructBehaviorTree();
            behaviorTreeRoot.SetBlackboard(blackboard);
            CombatEventBus.OnRevived += HandlePlayerRevived;

            // 注册进本场战斗的重置清单。用 Ensure() 而非 Current：
            // 各组件的 Start 顺序不定，谁先跑到谁负责把 Scope 建出来。
            EncounterScope.Ensure()?.Register(this);
        }

        // 复战重置：把 AI 的运行时状态清干净。
        //
        // 这里每一项漏掉都有明确症状：
        //   - 黑板不清 → 上一场的招式冷却带进新一场，复战开局 Boss 发呆好几秒
        //   - 执行器不清 → 上一场被打断的招会在新一场接着播下一段
        //   - 复活阶段机不清 → Boss 卡在「等玩家起身」，站着不动
        public void ResetForEncounter()
        {
            blackboard?.Clear();

            postRevivePhase = PostRevivePhase.None;
            postReviveTimer = 0f;
            circlingIncapacitatedPlayer = false;

            ResetExecutors();

            // 注意：moveFilter 是 MeleeOnly 调试白名单，属于「设定」而不是「状态」，
            // 复战重开应当保留，不在这里清。
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

        private void Update()
        {
            if (PlayerTarget == null || behaviorTreeRoot == null) return;

            if (body.IsDefeated)
            {
                ResetExecutors();
                body.MoveDirection = Vector3.zero;
                body.PreferFastWalk = false;
                return;
            }

            // 忍杀演出 / 被弹反或识破硬直：树不跑。否则出招节点一失败就会落到
            // BT_MoveToTarget，近身 Roam 每帧 RotateYaw 对准玩家（识破后猛转）。
            if (body.IsFinisherLocked || body.IsParried)
            {
                // 树不跑时也要把 Busy 执行器清掉，否则硬直结束会接着播被打断招的下一段。
                ResetExecutors();
                return;
            }

            bool playerIncapacitated = PlayerBody != null && PlayerBody.IsIncapacitatedForBoss;
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
                moveToTarget?.Evaluate();
                return;
            }

            // 开场语音：可以走位，但不要出招。
            if (IsOpeningHold())
            {
                ResetExecutors();
                if (body.IsAttacking)
                    body.CancelAttackToIdle();
                moveToTarget?.Evaluate();
                return;
            }

            healPunish?.ArmIfPlayerHealing();
            behaviorTreeRoot.Evaluate();
        }

        private static bool IsOpeningHold()
        {
            return BossVoiceDirector.Instance != null && BossVoiceDirector.Instance.IsOpeningHold;
        }

        private void ResetExecutors()
        {
            activeExecutor?.ResetMove();
            kengekiExecutor?.ResetMove();
            interruptExecutor?.ResetMove();
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

        // 完整树：崩解跳过 → 交锋 → 喝药重箭 → 主动抽招 → 追击
        // 招架层已由 CharacterBody.TryPassiveDeflect（受击拦截）替代，不再挂 BT_DeflectIf。
        // 调试：DisableBossAttacks 勾上时裁剪掉全部主动出招节点，只留追击走位（Boss 变挨打靶）。
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
                healPunish = new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor);
                children.Add(healPunish);
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

        private void UpdatePostRevivePhase()
        {
            ResetExecutors();

            switch (postRevivePhase)
            {
                case PostRevivePhase.WaitPlayerStand:
                    HoldFacePlayer();
                    body.MoveDirection = Vector3.zero;
                    if (PlayerBody != null && PlayerBody.IsReviving)
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
            // 走 CharacterBody 统一 API：地面态换子状态，非地面态重建地面父状态（禁止直接判顶层类型）
            body.ForceChangeGroundedSubState(g => new BossReviveBackoffState(body, g));
        }

        private bool IsInReviveBackoff()
        {
            if (!body.IsGroundedTop)
                return postReviveTimer < 0.6f;
            return body.IsInGroundedSubState<BossReviveBackoffState>();
        }

        private void ArmPostReviveRoamGap()
        {
            if (blackboard == null) return;
            float gap = roamAfterAttack + Random.Range(0f, Mathf.Max(0f, roamAfterAttackJitter));
            blackboard.ActiveGapDuration = gap;
            blackboard.SetCooldown("active_gap");
        }
    }

}
