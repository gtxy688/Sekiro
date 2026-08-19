using UnityEngine;

// 闪避（垫步）状态：全权根运动，位移由垫步动画 Root 曲线驱动，代码只计时退出
// 无敌帧（M4）：前 DodgeIFrame 秒内普通攻击打不中（危字突刺除外 → 识破优先）
public class DodgeState : BaseState
{
    private HierarchicalState parent;
    private float dodgeTimer;
    private float dodgeDuration;
    private float iFrameDuration;

    public DodgeState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
        // 垫步持续时长/无敌帧从 Config 读（容错默认值）
        dodgeDuration = body.Config != null ? body.Config.DodgeDuration : 0.5f;
        iFrameDuration = body.Config != null ? body.Config.DodgeIFrame : 0.3f;
    }

    public override void OnEnter()
    {
        dodgeTimer = 0f;

        // 播垫步动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Dodge", 0.05f);
    }

    public override void OnUpdate()
    {
        dodgeTimer += Time.deltaTime;

        if (dodgeTimer >= dodgeDuration)
        {
            if (body.MoveDirection.sqrMagnitude > 0.01f)
            {
                bool locked = LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
                    && LockOnManager.Instance.Target != null;
                // 锁定下没有 DodgeToStrafe，直接进四向循环
                string enter = locked ? null : "DodgeToWalk";
                parent.SubStateMachine.ChangeState(new MoveState(body, parent, enter));
            }
            else
            {
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            }
        }
    }

    // 垫步不可打断；仍更新移动意图，结束时才能接 DodgeToWalk
    public override bool HandleCommand(ICommand cmd)
    {
        if (cmd is MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
        }
        return true;
    }

    // 受击拦截（M1/M17/M4）：
    //   突刺危字 + 垫步 → 触发识破（踩刀），拦截伤害（优先于无敌帧）
    //   普通攻击 + 无敌帧内 → 躲过（拦截伤害）
    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous && hit.perilousType == PerilousType.Thrust)
        {
            parent.SubStateMachine.ChangeState(new MikiriCounterState(body, parent, hit));
            return true;
        }

        // 无敌帧内：普通攻击打不中
        if (dodgeTimer <= iFrameDuration)
        {
            return true;
        }
        return false;
    }
}
