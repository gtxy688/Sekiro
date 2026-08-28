using UnityEngine;

// 玩家成对忍杀状态：动画播完后代码结算清命并解锁双方，不依赖命中帧事件。
public class FinisherState : BaseState
{
    private readonly CharacterBody victim;
    private readonly string animName;
    private bool hasSeenAnim;
    private bool hasCompleted;

    public FinisherState(
        CharacterBody body,
        CharacterBody victim,
        string animName) : base(body)
    {
        this.victim = victim;
        this.animName = animName;
    }

    public override void OnEnter()
    {
        hasSeenAnim = false;
        hasCompleted = false;
        body.IsFinisherLocked = true;
        body.IsAttacking = false;
        body.AttackUninterruptible = false;
        body.MoveDirection = Vector3.zero;
        body.DisableWeaponHit();
        if (body.Rb != null)
        {
            Vector3 v = body.Rb.velocity;
            body.Rb.velocity = new Vector3(0f, v.y, 0f);
        }

        if (victim != null)
        {
            Vector3 toVictim = victim.transform.position - body.transform.position;
            body.SnapYaw(toVictim, animName);
        }

        if (!AnimUtil.TryPlay(body.Animator, animName))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少忍杀状态：{animName}");
        }
    }

    public override void OnExit()
    {
        body.IsFinisherLocked = false;
    }

    public override void OnUpdate()
    {
        if (hasCompleted) return;

        body.MoveDirection = Vector3.zero;

        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, animName))
        {
            hasSeenAnim = true;
            if (info.normalizedTime < 0.98f) return;
        }
        else
        {
            if (!hasSeenAnim || body.Animator.IsInTransition(0)) return;
        }

        hasCompleted = true;
        if (CombatManager.Instance != null)
        {
            CombatManager.Instance.ExecuteFinisher(body);
            CombatManager.Instance.CompleteFinisherSequence(body);
        }
        else
        {
            Debug.LogError("处决动画结束时找不到 CombatManager。");
            body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

    // 处决期间吞掉所有命令（不可打断）
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    public override bool OnHitReceived(HitData hit)
    {
        return true;
    }
}
