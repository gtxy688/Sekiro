using System.Collections.Generic;

namespace ARPG.Boss.BehaviourTree
{


    // 节点评估结果，供复合节点决定续跑、推进或尝试下一分支。
    public enum NodeState
    {
        Running, // 运行中：任务还没执行完，下一帧还要继续
        Success, // 当前任务完成
        Failure  // 条件不满足或任务失败
    }

    // 行为树节点基类，统一评估接口、子节点和黑板传递。
    public abstract class Node
    {
        protected NodeState state;
        public NodeState State => state;

        // 如果是复合节点（比如 Sequence/Selector），它会有子节点
        protected List<Node> children = new List<Node>();

        // 构建树时由根节点向下传递的实例级黑板。
        protected Blackboard blackboard;

        public Node() { }

        public Node(List<Node> children)
        {
            this.children = children;
        }

        // 递归注入子节点；另持有执行器的节点需重写并继续传递黑板。
        public virtual void SetBlackboard(Blackboard bb)
        {
            blackboard = bb;
            foreach (Node child in children)
            {
                child.SetBlackboard(bb);
            }
        }

        public abstract NodeState Evaluate();
    }

}
