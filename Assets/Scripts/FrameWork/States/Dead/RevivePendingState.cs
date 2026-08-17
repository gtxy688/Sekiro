using UnityEngine;

// 回生待机状态（M14）：死亡倒地，等待玩家按攻击键复活；
// 超时未确认 → 真死（进游戏结束）
public class RevivePendingState : BaseState
{
    private HierarchicalState parent;
    private float timer;
    private float timeout = 3f; // 回生确认窗口（占位值）

    public RevivePendingState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        timer = 0f;
        // 倒地动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Death_Pending", 0.1f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= timeout)
        {
            // 超时未确认 → 真死
            CombatEventBus.TriggerDeath(body);
            body.MainStateMachine.ChangeState(new DeadState(body, false));
        }
    }

    // 按攻击键 → 复活（回满血 + 无敌帧后续接）
    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is AttackCommand)
        {
            body.Revive();
            // 回生动画（占位名，M8 接动画前）
            body.Animator.CrossFade("Revive", 0.1f);
            // 复活后回地面继续打（Boss 不重置）
            body.MainStateMachine.ChangeState(new GroundedState(body));
            return true;
        }
        return true; // 其余命令全吞
    }
}
