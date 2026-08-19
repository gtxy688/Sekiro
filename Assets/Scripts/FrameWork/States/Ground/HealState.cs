using UnityEngine;

// 喝葫芦状态（M16）：播喝药动画 Drink，
// 进入时立即扣药回血（满血也可喝，HP 封顶），动画期间可被打断（OnHitReceived 不拦截 → 被切 Stunned），
// 结束回待机。被打断 = 药已消耗（只狼同款：喝药被砍药水照样没）
public class HealState : BaseState
{
    private HierarchicalState parent;
    private float timer;
    private float duration = 1.2f; // 喝药动画时长（占位值，可后续放 SO）

    public HealState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        timer = 0f;

        // 先扣药；没药才退回。满血也能喝（播 Drink、扣次数，血量封顶）
        if (!body.UseGourd())
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            return;
        }

        // 播喝药动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Drink", 0.1f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;
        if (timer >= duration)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    // 喝药期间吞掉其他命令（不可移动/攻击）
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    // 不拦截受击 → 被打会切 StunnedState（喝药被打断，符合策划案"会被打断"）
}
