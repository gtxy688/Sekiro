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

    [Header("完整 AI")]
    public BossMoveTable moveTable;

    public AttackConfig BowShotConfig; // 保留槽位，弓在表里，不再单独挂

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

        if (body.LightAttack == null)
        {
            Debug.LogError($"{name} 的 CharacterBody 缺少 LightAttack，AI 已停用。");
            enabled = false;
            return;
        }

        blackboard = new Blackboard();
        blackboard.Set("target", PlayerTarget);
        body.CombatTarget = PlayerTarget;
        body.MoveUsesWorldDir = true;
        // M7 被动防御（只狼攻防转换）：玩家命中 Boss 的瞬间做防御判定（普通格挡/计数升级弹反）。
        // 取代旧的"AI 短按防御 + 1.5s 格挡 CD"主动方案，Boss 不再裸受击。
        body.EnablePassiveDeflect = true;
        behaviorTreeRoot = ConstructBehaviorTree();
        behaviorTreeRoot.SetBlackboard(blackboard);
    }

    private void Update()
    {
        if (PlayerTarget == null || behaviorTreeRoot == null) return;
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
    private Node ConstructBehaviorTree()
    {
        activeExecutor = new BT_ExecuteMove(body, moveTable);
        kengekiExecutor = new BT_ExecuteMove(body, moveTable);
        interruptExecutor = new BT_ExecuteMove(body, moveTable);

        return new Selector(new List<Node>
        {
            new ConditionNode(() => body.IsPostureBroken),

            new BT_Kengeki(body, moveTable, PlayerTarget, kengekiExecutor),

            new BT_HealPunish(body, moveTable, PlayerBody, interruptExecutor),

            new BT_PickActive(body, moveTable, PlayerTarget, activeExecutor),

            new BT_MoveToTarget(body, PlayerTarget, attackRange)
        });
    }
}
