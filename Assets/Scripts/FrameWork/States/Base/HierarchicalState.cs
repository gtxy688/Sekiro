using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;

namespace ARPG.FrameWork.States.Base
{
    // 父状态先拦截命令与受击，未处理时再转交当前子状态。
    public abstract class HierarchicalState : BaseState
    {
        // 每个父状态独立持有自己的子状态机。
        public StateMachine SubStateMachine { get; private set; }

        public HierarchicalState(CharacterBody body) : base(body)
        {
            // 子状态切换也回报给 CharacterBody，这样调试快照能显示完整路径。
            SubStateMachine = new StateMachine(
                (previous, current, reason) => body.NotifyStateChanged(this, previous, current, reason),
                state => body.GetStatePathForDebug(this, state));
        }

        // 要求子类必须提供一个默认进入的子状态
        protected abstract BaseState GetInitialSubState();

        public override void OnEnter()
        {
            // 自动切入默认子状态
            BaseState initial = GetInitialSubState();
            if (initial != null)
            {
                SubStateMachine.ChangeState(initial, "parent: enter initial child");
            }
        }

        public override void OnUpdate()
        {
            // 如果父状态自己没被切走，就驱动当前的子状态更新
            SubStateMachine.Update();
        }

        public override void OnExit()
        {
            // 强制退出当前正在运行的子状态
            SubStateMachine.ChangeState(null, "parent: exit child");
        }

        // 父层先处理跨子状态规则，未消费的命令再交给当前叶子。
        public override bool HandleCommand(ICommand cmd)
        {
            // 先应用父状态的公共限制。
            if (OnParentHandleCommand(cmd))
            {
                return true;
            }

            // 未拦截时交给当前子状态。
            if (SubStateMachine.CurrentState != null)
            {
                return SubStateMachine.CurrentState.HandleCommand(cmd);
            }

            return false;
        }

        // 留给具体父状态去实现拦截逻辑
        protected virtual bool OnParentHandleCommand(ICommand cmd) 
        { 
            return false; 
        }

        // 父层先处理统一受击规则，未拦截时继续转交叶子的防御或无敌判定。
        public override bool OnHitReceived(HitData hit)
        {
            // 先应用父状态的受击规则。
            if (OnParentHandleHit(hit))
            {
                return true;
            }

            // 未拦截时交给当前子状态。
            if (SubStateMachine.CurrentState != null)
            {
                return SubStateMachine.CurrentState.OnHitReceived(hit);
            }

            return false;
        }

        // 留给具体父状态去实现受击拦截逻辑
        protected virtual bool OnParentHandleHit(HitData hit)
        {
            return false;
        }
    }
}
