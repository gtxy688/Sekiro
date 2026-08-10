// 条件节点 
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
        // 条件满足返回 Success，否则返回 Failure
        state = condition() ? NodeState.Success : NodeState.Failure;
        return state;
    }
}