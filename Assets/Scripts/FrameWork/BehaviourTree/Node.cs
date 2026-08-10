using System.Collections.Generic;


// 1. 行为树的三大核心返回状态
public enum NodeState
{
    Running, // 运行中：任务还没执行完，下一帧还要继续
    Success, // 成功：任务搞定了
    Failure  // 失败：条件不满足，或任务搞砸了
}

// 2. 所有节点的绝对基类
public abstract class Node
{
    protected NodeState state;
    public NodeState State => state;

    // 如果是复合节点（比如 Sequence/Selector），它会有子节点
    protected List<Node> children = new List<Node>();

    // 无参构造（用于叶子节点）
    public Node() { }

    // 带参构造（用于控制节点，传入它的小弟）
    public Node(List<Node> children)
    {
        this.children = children;
    }

    // 魔法的核心：每一个节点都必须实现自己的 Evaluate 方法
    public abstract NodeState Evaluate();
}