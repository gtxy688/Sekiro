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
        body.IsFinisherLocked = true;
        body.IsAttacking = false;
        body.MoveDirection = Vector3.zero;
        body.DisableWeaponHit();
        if (body.Rb != null)
        {
            Vector3 v = body.Rb.velocity;
            body.Rb.velocity = new Vector3(0f, v.y, 0f);
        }

        if (CombatManager.Instance != null && CombatManager.Instance.PlayerRef != null)
        {
            Vector3 toPlayer =
                CombatManager.Instance.PlayerRef.transform.position - body.transform.position;
            body.SnapYaw(toPlayer, animName);
        }

        if (!AnimUtil.TryPlay(body.Animator, animName))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少忍杀受害状态：{animName}");
        }
    }

    public override void OnExit()
    {
        body.IsFinisherLocked = false;
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
