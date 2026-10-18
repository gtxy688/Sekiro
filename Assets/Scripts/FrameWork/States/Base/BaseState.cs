using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;

namespace ARPG.FrameWork.States.Base
{
    // 所有状态的基类
    public abstract class BaseState
    {
        // 状态通过所属角色访问组件、配置和状态切换入口。
        protected CharacterBody body;

        public BaseState(CharacterBody body)
        {
            this.body = body;
        }

        public virtual void OnEnter() { }
        public virtual void OnUpdate() { }
        public virtual void OnExit() { }

        // true 表示已消费命令并清除对应缓冲；false 表示未消费，允许缓冲重试。
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
