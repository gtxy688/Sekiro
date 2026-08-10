using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(CharacterBody))]
public class BossBrain : BrainBase
{
    public Transform PlayerTarget;
    private Node rootNode;

    protected override void Awake()
    {
        base.Awake();
        // 组装行为树
        rootNode = ConstructBehaviorTree();
    }

    protected override void Update()
    {
        base.Update(); // 处理可能存在的指令缓冲 (如果有的话)
        
        // 每帧驱动行为树思考
        if (PlayerTarget != null)
        {
            rootNode.Evaluate();
        }
    }

    // 构建 AI 逻辑树
    private Node ConstructBehaviorTree()
    {
        float attackRange = 2.5f;

        // 【节点 1】：如果玩家在攻击范围内，就执行一次轻攻击
        Sequence meleeAttackSequence = new Sequence(new List<Node>
        {
            new ConditionNode(() => Vector3.Distance(body.transform.position, PlayerTarget.position) <= attackRange),
            new BT_Attack(body, isHeavy: false)
        });

        // 【节点 2】：移动到玩家身边
        Node moveToPlayer = new BT_MoveToTarget(body, PlayerTarget, attackRange);

        // 【根节点】：Selector (选择器)
        // 逻辑顺序：先尝试近战攻击 -> 如果距离不够(条件失败)，则尝试向玩家移动
        Selector root = new Selector(new List<Node>
        {
            meleeAttackSequence,
            moveToPlayer
        });

        return root;
    }
}