public class DeflectState : BaseState
{
    HierarchicalState parent;
    
    public DeflectState(CharacterBody body, HierarchicalState parent) : base(body) 
    {
        this.parent = parent;
    }

    public override void OnEnter() {  }

    public override void OnUpdate() { }

    // 处理传递到底层的命令
    public override bool HandleCommand(ICommand cmd)
    {
        // 松手（PlayerBrain 在 Defend.canceled 发 IdleCommand）→ 退出防御回待机
        if (cmd is IdleCommand)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            return true;
        }

        // 防御姿态期间吞掉其他所有命令（移动/攻击都不可用）
        // 弹反窗口（OnHitReceived 拦截）由 M4 细化
        return true;
    }
}