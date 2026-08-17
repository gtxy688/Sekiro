using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterBody))]
public class BTBrain : MonoBehaviour
{
    // 1. 核心依赖
    public Transform PlayerTarget;
    public CharacterBody PlayerBody;          // 玩家 CharacterBody（反制判定用，可留空自动取）
    public AttackConfig BowShotConfig;        // 射箭招式配置（占位，投射物后补）
    public float attackRange = 3.0f;          // 攻击距离

    private CharacterBody body;

    // 行为树的根节点 + 共享黑板
    private Node behaviorTreeRoot;
    private Blackboard blackboard;

    // 2. 初始化与驱动
    private void Awake()
    {
        body = GetComponent<CharacterBody>();
    }

    private void Start()
    {
        if (PlayerBody == null && PlayerTarget != null)
        {
            PlayerBody = PlayerTarget.GetComponent<CharacterBody>();
        }

        // 黑板 + 行为树构建（M5）
        blackboard = new Blackboard();
        blackboard.Set("target", PlayerTarget);
        behaviorTreeRoot = ConstructBehaviorTree();
        behaviorTreeRoot.SetBlackboard(blackboard);
    }

    private void Update()
    {
        if (PlayerTarget != null && behaviorTreeRoot != null)
        {
            behaviorTreeRoot.Evaluate();
        }
    }

    private float Distance()
    {
        return Vector3.Distance(body.transform.position, PlayerTarget.position);
    }

    // 3. 构建行为树（M7 三层 AI）
    // 优先级从高到低：
    //   ① 玩家攻击中 + 距离近 → 招架（短按弹反，可能反杀玩家）【交锋/防御层】
    //   ② 距离 ≤3m → 近战连段（AttackSet 逐刀，含突刺危字）【主动计划-贴身】
    //   ③ 3-5m → 短连段【主动计划-中距】
    //   ④ 5-7m → 射箭【主动计划-远距】
    //   ⑤ 其他 → 追击【主动计划-接近】
    private Node ConstructBehaviorTree()
    {
        Selector root = new Selector(new List<Node>
        {
            // ① 招架反制：玩家挥刀时 Boss 短按弹反
            new Sequence(new List<Node>
            {
                new ConditionNode(() =>
                    PlayerBody != null && PlayerBody.IsAttacking
                    && !blackboard.IsOnCooldown("deflect", 1.5f)
                    && Distance() <= 2.5f),
                new BT_Deflect(body)
            }),

            // ② 贴身近战连段
            new Sequence(new List<Node>
            {
                new ConditionNode(() => Distance() <= 3f && !blackboard.IsOnCooldown("combo", 2f)),
                new BT_Combo(body, 3)
            }),

            // ③ 中距短连段
            new Sequence(new List<Node>
            {
                new ConditionNode(() => Distance() > 3f && Distance() <= 5f && !blackboard.IsOnCooldown("combo", 2f)),
                new BT_Combo(body, 2)
            }),

            // ④ 远距射箭
            new Sequence(new List<Node>
            {
                new ConditionNode(() => Distance() > 5f && Distance() <= 7f && !blackboard.IsOnCooldown("bow", 2f)),
                new BT_BowShot(body, BowShotConfig)
            }),

            // ⑤ 追击
            new BT_MoveToTarget(body, PlayerTarget, attackRange)
        });

        return root;
    }
}
