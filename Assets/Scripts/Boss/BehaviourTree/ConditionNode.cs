namespace ARPG.Boss.BehaviourTree
{
    // 将条件委托转换为 Success / Failure，供复合节点分支选择。
    public class ConditionNode : Node
    {
        public delegate bool ConditionDelegate();
        private ConditionDelegate condition;

        public ConditionNode(ConditionDelegate condition)
        {
            this.condition = condition;
        }

        public override NodeState Evaluate()
        {
            state = condition() ? NodeState.Success : NodeState.Failure;
            return state;
        }
    }
}
