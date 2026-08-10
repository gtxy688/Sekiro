using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterBody))]
public class BTBrain : MonoBehaviour
{
    // 1. 核心依赖
    public Transform PlayerTarget;         
    private CharacterBody body;            
    
    // 行为树的根节点
    private Node behaviorTreeRoot;

    // 2. 初始化与驱动
    private void Awake()
    {
        // 独立获取自己的身体，不依赖任何基类
        body = GetComponent<CharacterBody>();
    }

    private void Start()
    {
        // 构建 Boss 的行为树逻辑
        behaviorTreeRoot = ConstructBehaviorTree();
    }

    private void Update()
    {
        // 每帧驱动行为树进行思考和决策
        if (PlayerTarget != null && behaviorTreeRoot != null)
        {
            behaviorTreeRoot.Evaluate();
        }
    }


    // 3. 构建行为树 (逻辑组装)
    private Node ConstructBehaviorTree()
    {
        float attackRange = 3.0f; // 攻击距离

        // 【战术 1】：近战攻击逻辑 (如果在攻击范围内，就执行一次攻击)
        Sequence meleeAttackSequence = new Sequence(new List<Node>
        {
            // 条件节点：距离 <= attackRange 吗？
            new ConditionNode(() => 
            {
                float dist = Vector3.Distance(body.transform.position, PlayerTarget.position);
                return dist <= attackRange;
            }),
            
            // 动作节点：执行轻攻击 (发送 Command)
            new BT_Attack(body, false)
        });

        // 【战术 2】：追击逻辑 (生成 MoveCommand 靠近玩家)
        Node moveToPlayer = new BT_MoveToTarget(body, PlayerTarget, attackRange);

        // 【根节点】：Selector (选择器)
        // 优先级：优先尝试近战攻击 (meleeAttackSequence)
        // 如果近战条件不满足 (Failure)，则退而求其次执行追击 (moveToPlayer)
        Selector root = new Selector(new List<Node>
        {
            meleeAttackSequence,
            moveToPlayer
        });

        return root;
    }
}