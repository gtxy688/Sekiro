using UnityEngine;

// 玩家成对忍杀状态：正常清命由动画事件完成，动画结束只做兜底与双方解锁。
public class FinisherState : BaseState
{
    private readonly CharacterBody victim;
    private readonly string animName;
    private bool hasSeenAnim;

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
        body.IsAttacking = false;
        body.DisableWeaponHit();
        body.Animator.CrossFade(animName, 0.05f);
    }

    public override void OnUpdate()
    {
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

        if (CombatManager.Instance != null)
        {
            if (!CombatManager.Instance.IsFinisherResolved(body))
            {
                Debug.LogError(
                    $"{animName} 未在命中帧触发 ExecuteFinisher，已在动画结束时执行兜底结算。");
                CombatManager.Instance.ExecuteFinisher(body);
            }
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
