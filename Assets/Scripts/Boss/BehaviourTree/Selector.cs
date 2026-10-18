using System.Collections.Generic;

namespace ARPG.Boss.BehaviourTree
{

    // 按优先级尝试子节点；只有声明锁定的运行中节点会在下一帧优先续跑。
    public class Selector : Node
    {
        // 走位节点不锁定，保证每帧都能被更高优先级的出招节点抢占。
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

}
