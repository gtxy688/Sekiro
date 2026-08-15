// 所有状态的基类
public abstract class BaseState
{
    // 每个状态都需要知道自己的“老板”是谁，方便切换
    protected CharacterBody body;

    public BaseState(CharacterBody body)
    {
        this.body = body;
    }

    public virtual void OnEnter() { }
    public virtual void OnUpdate() { }
    public virtual void OnExit() { }

    // 返回值代表：“我这个状态是否消耗掉了这个命令？”
    // 返回 true：消耗掉了，停止传递 (缓冲池也会清空)。
    // 返回 false：我不关心这个命令，继续抛给别人。
    public virtual bool HandleCommand(ICommand cmd) 
    { 
        return false; 
    }

    // 受击响应（M1）：镜像 Command 路由，让受击结算能查到"当前在弹反吗/闪避吗/受击中吗"。
    // 返回 true  = 状态拦截住了（弹反成功 / 无敌帧 / 二次受击），不扣血。
    // 返回 false = 不拦截，由上层扣血并切入受击父状态。
    public virtual bool OnHitReceived(HitData hit)
    {
        return false;
    }
}