using System.Collections.Generic;
public class Sequence : Node
{
    // Running 记忆（M5）：子节点返回 Running 时记住索引，下一帧从同一索引继续
    private int currentChildIndex = 0;

    public Sequence(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
        while (currentChildIndex < children.Count)
        {
            switch (children[currentChildIndex].Evaluate())
            {
                case NodeState.Failure:
                    currentChildIndex = 0; // 重置，下次重跑
                    state = NodeState.Failure;
                    return state;

                case NodeState.Success:
                    currentChildIndex++; // 下一个
                    break;

                case NodeState.Running:
                    state = NodeState.Running; // 记住索引，下帧继续
                    return state;
            }
        }

        currentChildIndex = 0;
        state = NodeState.Success;
        return state;
    }
}
