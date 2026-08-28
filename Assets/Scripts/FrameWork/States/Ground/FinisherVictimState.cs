using UnityEngine;

// Boss 在忍杀确认窗口或成对忍杀演出中的锁定状态。
// 不自行超时：确认窗口由玩家状态负责，演出结束由 CombatManager 统一释放。
public class FinisherVictimState : BaseState
{
    private readonly string animName;
    private bool triedNestedPath;
    private float timer;

    public FinisherVictimState(CharacterBody body, string animName) : base(body)
    {
        this.animName = animName;
    }

    public override void OnEnter()
    {
        triedNestedPath = false;
        timer = 0f;
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

        // 成对忍杀开演才水平对视。确认窗口钉住被打断时的朝向。
        if (IsPairedFinisherAnim(animName) &&
            CombatManager.Instance != null &&
            CombatManager.Instance.PlayerRef != null)
        {
            body.ClearCombatYawFrozen();
            Vector3 toPlayer =
                CombatManager.Instance.PlayerRef.transform.position - body.transform.position;
            body.SnapYaw(toPlayer, animName);
        }
        else
        {
            body.FreezeCombatYaw();
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

    public override void OnUpdate()
    {
        body.MoveDirection = Vector3.zero;
        if (triedNestedPath || body.Animator == null) return;

        timer += Time.deltaTime;
        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(info, animName))
        {
            triedNestedPath = true;
            return;
        }

        if (timer <= 0.05f) return;

        triedNestedPath = true;
        AnimUtil.TryPlay(body.Animator, animName);
    }

    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    public override bool OnHitReceived(HitData hit)
    {
        return true;
    }

    // 资源名是 Finsher_*（缺字母 i）；Miriki 拼写也走这个前缀。
    private static bool IsPairedFinisherAnim(string name)
    {
        return !string.IsNullOrEmpty(name) && name.StartsWith("Finsher_");
    }
}
