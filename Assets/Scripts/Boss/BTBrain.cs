using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterBody))]
public class BTBrain : MonoBehaviour
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

    [Header("调试（测试弹反用）")]
    [Tooltip("只近战模式：屏蔽弓/后跳/特殊招，Boss 只用近战普通攻击 + 被动格挡/弹反。测弹反后立即反击的纯净环境")]
    public bool MeleeOnly = false;
    [Tooltip("禁用 Boss 主动出招（交锋/喝药重箭/主动抽招），只留追击走位 + 被动防御判定。测试被弹反回合时勾上，Boss 变纯挨打靶")]
    public bool DisableBossAttacks = false;
    [Tooltip("禁用 Boss 被动防御（玩家攻击不再触发强制格挡/计数弹反），测试玩家主动弹反时勾上")]
    public bool DisablePassiveDeflect = false;

    [Header("完整 AI")]
    public BossMoveTable moveTable;

    private CharacterBody body;
    private Node behaviorTreeRoot;
    private Blackboard blackboard;

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
        if (MeleeOnly)
        {
            moveTable.moveWhitelist = new System.Collections.Generic.HashSet<string>
            {
                "Slash_Double", "Slash_Heavy", "Slash_SpinElbow", "Slash_StepTurn", "Kick",
                "Kengeki_Slash", "Kengeki_Double"
            };
        }

        behaviorTreeRoot = ConstructBehaviorTree();
        behaviorTreeRoot.SetBlackboard(blackboard);
    }

    private void Update()
    {
        if (PlayerTarget == null || behaviorTreeRoot == null) return;
        // 忍杀演出 / 被弹反或识破硬直：树不跑。否则出招节点一失败就会落到
        // BT_MoveToTarget，近身 Roam 每帧 RotateYaw 对准玩家（识破后猛转）。
        if (body.IsFinisherLocked || body.IsParried)
        {
            // 树不跑时也要把 Busy 执行器清掉，否则硬直结束会接着播被打断招的下一段。
            activeExecutor?.ResetMove();
            kengekiExecutor?.ResetMove();
            interruptExecutor?.ResetMove();
            return;
        }
        behaviorTreeRoot.Evaluate();
    }

    private void OnDisable()
    {
        if (body == null) return;
        body.MoveDirection = Vector3.zero;
        body.MoveUsesWorldDir = false;
        body.CombatTarget = null;
    }

    private BT_ExecuteMove activeExecutor;
    private BT_ExecuteMove kengekiExecutor;
    private BT_ExecuteMove interruptExecutor;

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
            children.Add(new BT_Kengeki(body, moveTable, PlayerTarget, kengekiExecutor));
            children.Add(new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor));
            children.Add(new BT_PickActive(body, moveTable, PlayerTarget, activeExecutor));
        }

        children.Add(new BT_MoveToTarget(body, PlayerTarget, attackRange,
            roamStrafeDuration, roamStrafeStrength, roamApproachStrength));

        return new Selector(children);
    }
}
