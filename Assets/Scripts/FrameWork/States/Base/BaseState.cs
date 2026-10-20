using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;

namespace ARPG.FrameWork.States.Base
{
    // 所有行为状态的基类。
    public abstract class BaseState
    {
        // 状态通过所属角色访问组件、配置和状态切换入口。
        protected CharacterBody body;

        public BaseState(CharacterBody body)
        {
            this.body = body;
        }

        // ChangeState 时旧状态先 OnExit，新状态再 OnEnter；每帧只更新当前状态的 OnUpdate。
        public virtual void OnEnter() { }
        public virtual void OnUpdate() { }
        public virtual void OnExit() { }

        // true 表示已消费命令，Brain 可清掉对应缓冲；不保证实际执行了动作（也可能主动丢弃）。
        // false 表示本状态未消费，Brain 可在缓冲有效期内重试；本类不管理输入缓冲。
        public virtual bool HandleCommand(ICommand cmd) 
        { 
            return false; 
        }

        // true 表示状态已处理本次命中，上层不再重复结算；false 交由角色统一扣血并切入受击。
        public virtual bool OnHitReceived(HitData hit)
        {
            return false;
        }
    }
}
