using UnityEngine;

// 处决状态（M10）：玩家播忍杀动画（占位名 Finisher），
// 动画结束时刻调用目标 ClearLife（清一条命）+ 目标恢复，自身回待机。
// 由 CombatManager.TryExecuteFinisher 强切（顶层走 GroundedState 初始子状态），
// 结束直接切顶层，不依赖 parent 引用
public class FinisherState : BaseState
{
    private CharacterBody victim;
    private float timer;
    private float duration = 2f; // 忍杀动画时长（占位值）

    public FinisherState(CharacterBody body, CharacterBody victim) : base(body)
    {
        this.victim = victim;
    }

    public override void OnEnter()
    {
        timer = 0f;

        // 面向处决目标
        if (victim != null)
        {
            Vector3 dir = victim.transform.position - body.transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.01f)
            {
                body.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        // 播忍杀动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Finisher", 0.1f);

        // 表现：处决特效/音效 + 强震屏
        CombatEventBus.TriggerFinisher(victim != null ? victim.transform.position : body.transform.position);
        CombatEventBus.TriggerCameraShake(1f);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        if (timer >= duration)
        {
            // 动画结束：清目标一条命 + 目标恢复 + 自己回待机
            victim?.ClearLife();
            if (victim != null && victim.LivesRemaining > 0)
            {
                victim.MainStateMachine.ChangeState(new GroundedState(victim));
            }
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    // 处决期间吞掉所有命令（不可打断）
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }
}
