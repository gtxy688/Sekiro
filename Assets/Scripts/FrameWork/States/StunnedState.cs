using UnityEngine;

// 受击状态（顶层父状态）：被打时强制打断一切行为，
// 进入时按物理状态分派到地面/空中受击子状态
public class StunnedState : HierarchicalState
{
    public StunnedState(CharacterBody body) : base(body) { }

    protected override BaseState GetInitialSubState()
    {
        // 进受击那一刻看物理状态，分派到不同的受击子状态
        if (body.IsGrounded)
        {
            return new GroundStunnedState(body, this);
        }
        else
        {
            return new AirStunnedState(body, this);
        }
    }

    // 受击期间吞掉所有命令，防止硬直里还能还手/跑动
    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        return true;
    }
}