using UnityEngine;

// 受击状态（顶层父状态）：被打时强制打断一切行为，
// 进入时按物理状态分派到地面/空中受击子状态
public class StunnedState : HierarchicalState
{
    public StunnedState(CharacterBody body) : base(body) { }

    protected override BaseState GetInitialSubState()
    {
        // 空中受击状态已移除（无 Hurt_Air 动画），受击统一播地面受击
        // 空中被打：硬直结束后由 GroundStunnedState 切回地面（可能轻微穿地，接受）
        return new GroundStunnedState(body, this);
    }

    // 受击期间吞掉所有命令，防止硬直里还能还手/跑动
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return true;
    }

    // 受击期间二次受击：全部拦截，防止硬直被刷新（M1）
    protected override bool OnParentHandleHit(HitData hit)
    {
        return true;
    }
}