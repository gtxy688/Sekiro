// 死亡父状态（M14，顶层 HierarchicalState）：
// canRevive=true → 回生待机子状态（按攻击键复活 / 超时真死）
// canRevive=false → 游戏结束子状态（按攻击键重开场景）
public class DeadState : HierarchicalState
{
    private readonly bool canRevive;

    public DeadState(CharacterBody body, bool canRevive) : base(body)
    {
        this.canRevive = canRevive;
    }

    protected override BaseState GetInitialSubState()
    {
        return canRevive
            ? new RevivePendingState(body, this)
            : (BaseState)new GameOverState(body, this);
    }

    // 死亡期间命令放行给子状态（复活/重开按键由子状态处理并吞掉其余命令）
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return false;
    }

    // 死亡期间无视所有受击
    protected override bool OnParentHandleHit(HitData hit)
    {
        return true;
    }
}
