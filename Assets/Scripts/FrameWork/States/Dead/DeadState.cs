using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;

namespace ARPG.FrameWork.States.Dead
{
    // 死亡父状态：可回生时进入选择待机，否则进入游戏结束并允许攻击键重载场景。
    // HP 归零时由 CombatStats 回调 CharacterBody 进入；数值结算已经完成，本层只选死亡流程。
    public class DeadState : HierarchicalState
    {
        private readonly bool canRevive;
        private readonly bool alreadyDowned; // 放弃回生时已经躺着，真死不再重播倒地。

        public DeadState(CharacterBody body, bool canRevive, bool alreadyDowned = false) : base(body)
        {
            this.canRevive = canRevive;
            this.alreadyDowned = alreadyDowned;
        }

        protected override BaseState GetInitialSubState()
        {
            return canRevive
                ? new RevivePendingState(body, this)
                : (BaseState)new GameOverState(body, this, alreadyDowned);
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

}
