using UnityEngine;

// 闪避（垫步）状态：全权根运动，位移由垫步动画 Root 曲线驱动，代码只计时退出
// 无敌帧（OnHitReceived 拦截）M4 细化
public class DodgeState : BaseState
{
    private HierarchicalState parent;
    private float dodgeTimer;
    private float dodgeDuration;

    public DodgeState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
        // 垫步持续时长从 Config 读（容错默认值）
        dodgeDuration = body.Config != null ? body.Config.DodgeDuration : 0.5f;
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
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    // 处理传递到底层的命令：垫步期间吞掉所有命令（不可打断）
    public override bool HandleCommand(ICommand cmd)
    {
        return true;
    }

    // 受击拦截（M1/M17）：
    //   突刺危字 + 垫步 → 触发识破（踩刀），拦截伤害
    //   普通攻击 → 垫步无敌帧（M4 细化，暂不拦截）
    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous && hit.perilousType == PerilousType.Thrust)
        {
            parent.SubStateMachine.ChangeState(new MikiriCounterState(body, parent, hit));
            return true;
        }
        return false;
    }
}
