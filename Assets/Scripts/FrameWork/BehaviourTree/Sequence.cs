using System.Collections.Generic;
public class Sequence : Node
{
    public Sequence(List<Node> children) : base(children) { }

    public override NodeState Evaluate()
    {

        foreach (Node node in children)
        {
            switch (node.Evaluate())
            {
                case NodeState.Failure:
                    state = NodeState.Failure;
                    return state; // 遇到失败，立刻停止遍历，向父节点报告失败！

                case NodeState.Success:
                    continue; // 当前小弟成功了，继续检查下一个小弟

                case NodeState.Running:
                    // 注意：标准的 Sequence 遇到 Running，也会立刻返回 Running，等待下一帧
                    state = NodeState.Running; 
                    return state; 
            }
        }

        // 如果全部遍历完都没被 return 掉，说明全绿通过了！
        state = NodeState.Success;
        return state;
    }
}