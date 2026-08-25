using System.Collections.Generic;

public class Selector : Node
{
    // Running 记忆：只对 ISelectorLock 续跑。禁止记住 BT_MoveToTarget，否则追击时招架抢不到。
    private int runningChildIndex = -1;

    public Selector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        if (runningChildIndex >= 0)
        {
            NodeState resume = children[runningChildIndex].Evaluate();
            if (resume == NodeState.Running)
            {
                state = NodeState.Running;
                return state;
            }
            runningChildIndex = -1;
        }

        for (int i = 0; i < children.Count; i++)
        {
            switch (children[i].Evaluate())
            {
                case NodeState.Failure:
                    continue;
                case NodeState.Success:
                    runningChildIndex = -1;
                    state = NodeState.Success;
                    return state;
                case NodeState.Running:
                    runningChildIndex = children[i] is ISelectorLock ? i : -1;
                    state = NodeState.Running;
                    return state;
            }
        }

        runningChildIndex = -1;
        state = NodeState.Failure;
        return state;
    }
}
