using System.Collections.Generic;
public class Selector : Node
{
    // Running 记忆（M5）：记住正在运行（Running）的子节点索引，下一帧优先看它是否还在跑；
    // 一旦它结束（成功/失败），重新从头按优先级遍历——保证高优先级分支随时可抢占
    private int runningChildIndex = -1;

    public Selector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        // 1. 记忆续跑：上次 Running 的子节点还在跑就继续它
        if (runningChildIndex >= 0)
        {
            NodeState resume = children[runningChildIndex].Evaluate();
            if (resume == NodeState.Running)
            {
                state = NodeState.Running;
                return state;
            }
            runningChildIndex = -1; // 它结束了，从头重选
        }

        // 2. 按优先级顺序遍历
        foreach (Node node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    continue; // 这个方案不行，试下一个备选方案

                case NodeState.Success:
                    state = NodeState.Success;
                    return state; // 只要有一个方案成功了，直接向上级报告成功！

                case NodeState.Running:
                    state = NodeState.Running;
                    return state; // 如果某个方案正在执行中，就挂起等待
            }
        }

        // 所有的备选方案都试过了，全都不行，报告彻底失败
        state = NodeState.Failure;
        return state;
    }
}
