using System.Collections.Generic;
public class Selector : Node
{
    public Selector(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {
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