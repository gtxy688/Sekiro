// 受击状态
public class StunnedState : BaseState
{
    HierarchicalState parent;
    
    public StunnedState(CharacterBody body, HierarchicalState parent) : base(body) 
    {
        this.parent = parent;
    }

    public override void OnEnter() {  }

    public override void OnUpdate() { }

    // 处理传递到底层的命令
    public override bool HandleCommand(ICommand cmd)
    {
        // 同上,不多写了
        return true;
    }
}