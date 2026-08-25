// 条件招架（已废弃，树里不挂，文件保留参考）：
// M7 起 Boss 的格挡改为 CharacterBody.TryPassiveDeflect 受击拦截（命中瞬间强制格挡判定），
// 不再由 AI 短按防御键触发，规避了旧方案的 1.5s 格挡 CD 与随机性。
public class BT_DeflectIf : Node, ISelectorLock
{
    private readonly BT_Deflect inner;
    private readonly System.Func<bool> canStart;

    public BT_DeflectIf(CharacterBody body, System.Func<bool> canStart)
    {
        inner = new BT_Deflect(body);
        children.Add(inner);
        this.canStart = canStart;
    }

    public override NodeState Evaluate()
    {
        if (inner.IsActive) return inner.Evaluate();
        if (!canStart()) return NodeState.Failure;
        return inner.Evaluate();
    }
}
