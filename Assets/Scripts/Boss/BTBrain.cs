using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterBody))]
public class BTBrain : MonoBehaviour
{
    [Header("目标")]
    public Transform PlayerTarget;
    public CharacterBody PlayerBody;

    [Header("简单 AI（先看效果）")]
    public float attackRange = 3.0f;
    public float attackCooldown = 2.5f;   // 打完一刀后隔多久再打
    public float deflectRange = 2.5f;
    public float deflectCooldown = 1.5f;

    public AttackConfig BowShotConfig; // 保留槽位，这版简单树不用

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

    private float Distance()
    {
        return Vector3.Distance(body.transform.position, PlayerTarget.position);
    }

    // 简单树：你砍我就格 → 够近且冷却好了就砍一刀 → 否则追过来
    private Node ConstructBehaviorTree()
    {
        return new Selector(new List<Node>
        {
            new Sequence(new List<Node>
            {
                new ConditionNode(() =>
                    PlayerBody != null && PlayerBody.IsAttacking
                    && !blackboard.IsOnCooldown("deflect", deflectCooldown)
                    && Distance() <= deflectRange),
                new BT_Deflect(body)
            }),

            new Sequence(new List<Node>
            {
                new ConditionNode(() =>
                    !body.IsPostureBroken
                    && Distance() <= attackRange
                    && !blackboard.IsOnCooldown("attack", attackCooldown)),
                new BT_HitOnce(body)
            }),

            new BT_MoveToTarget(body, PlayerTarget, attackRange)
        });
    }
}
