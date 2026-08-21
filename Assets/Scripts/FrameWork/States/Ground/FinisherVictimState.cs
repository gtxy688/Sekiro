using UnityEngine;

// Boss 在忍杀确认窗口或成对忍杀演出中的锁定状态。
// 不自行超时：确认窗口由玩家状态负责，演出结束由 CombatManager 统一释放。
public class FinisherVictimState : BaseState
{
    private readonly string animName;

    public FinisherVictimState(CharacterBody body, string animName) : base(body)
    {
        this.animName = animName;
    }

    public override void OnEnter()
    {
        body.IsAttacking = false;
        body.DisableWeaponHit();

        if (!AnimUtil.HasState(body.Animator, animName))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少忍杀受害状态：{animName}");
            return;
        }

        body.Animator.CrossFade(animName, 0.05f);
    }

    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    public override bool OnHitReceived(HitData hit)
    {
        return true;
    }
}
