using UnityEngine;

// 受击状态（顶层父状态）：被打时强制打断一切行为。
// 玩家走 HitGrade；Boss 仍走 HurtContext + knockback。
public class StunnedState : HierarchicalState
{
    private readonly HurtContext context;
    private readonly HitGrade grade;
    private readonly bool useHitGrade;

    public StunnedState(CharacterBody body, HurtContext context = HurtContext.Normal) : base(body)
    {
        this.context = context;
        useHitGrade = false;
    }

    public StunnedState(CharacterBody body, HitGrade grade) : base(body)
    {
        this.grade = grade;
        useHitGrade = true;
        context = grade == HitGrade.Light ? HurtContext.Normal : HurtContext.Heavy;
    }

    protected override BaseState GetInitialSubState()
    {
        body.IsKnockedDown = false;
        return new GroundStunnedState(body, this, context, useHitGrade, grade);
    }

    protected override bool OnParentHandleCommand(ICommand cmd)
    {
        GroundStunnedState ground = SubStateMachine.CurrentState as GroundStunnedState;
        if (useHitGrade && cmd is DodgeCommand)
        {
            if (ground != null && ground.CanDodgeCancel)
            {
                body.IsKnockedDown = false;
                body.MainStateMachine.ChangeState(
                    new GroundedState(body, new DodgeState(body, null)));
                return true;
            }
            // 后摇未到：不消耗，留缓冲重试（对齐 StaggerBroken / MidToGuard）
            return false;
        }
        if (useHitGrade && cmd is DeflectCommand)
        {
            if (ground != null && ground.CanMidToGuard)
            {
                body.MainStateMachine.ChangeState(
                    new GroundedState(body, new MidToGuardState(body)));
                return true;
            }
            if (ground != null && ground.CanLightGuardCancel)
            {
                body.MainStateMachine.ChangeState(
                    new GroundedState(body, new DeflectState(body, null)));
                return true;
            }
        }
        return true;
    }

    protected override bool OnParentHandleHit(HitData hit)
    {
        if (!useHitGrade)
            return true;

        if (hit.perilousType == PerilousType.Grab)
        {
            body.TakeDamage(hit.healthDmg, hit.postureDmg);
            if (body.CurrentHP <= 0) return true;
            if (CombatManager.Instance != null)
                CombatManager.Instance.TryStartGrabThrow(hit.attacker, body);
            return true;
        }

        body.TakeDamage(hit.healthDmg, hit.postureDmg);
        if (body.CurrentHP <= 0 || body.IsPostureBroken)
            return true;

        GroundStunnedState ground = SubStateMachine.CurrentState as GroundStunnedState;
        if (ground != null)
            ground.ReceiveFollowUpHit(hit);
        return true;
    }
}
