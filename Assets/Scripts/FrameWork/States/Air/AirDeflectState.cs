public class AirDeflectState : BaseState
{
    HierarchicalState parent;
    
    public AirDeflectState(CharacterBody body, HierarchicalState parent) : base(body) 
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