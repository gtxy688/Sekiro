using System.Collections.Generic;

namespace ARPG.Boss.BehaviourTree
{
    // 顺序执行子节点；运行中保留位置，失败或全部成功后从头开始。
    public class Sequence : Node
    {
        private int currentChildIndex = 0;

        public Sequence(List<Node> children) : base(children) { }

        public override NodeState Evaluate()
        {
            while (currentChildIndex < children.Count)
            {
                switch (children[currentChildIndex].Evaluate())
                {
                    case NodeState.Failure:
                        currentChildIndex = 0;
                        state = NodeState.Failure;
                        return state;

                    case NodeState.Success:
                        currentChildIndex++;
                        break;

                    case NodeState.Running:
                        state = NodeState.Running;
                        return state;
                }
            }

            currentChildIndex = 0;
            state = NodeState.Success;
            return state;
        }
    }

}
