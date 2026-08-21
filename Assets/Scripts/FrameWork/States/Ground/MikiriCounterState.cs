using UnityEngine;

// 识破（踩刀）状态（M17）：突刺危字攻击 + 玩家垫步 → 触发识破
// 播踩刀动画 → 大幅涨攻击者架势 → 回待机
// 叶子状态，位于 GroundedState.SubStateMachine
public class MikiriCounterState : BaseState
{
    private HierarchicalState parent;
    private CharacterBody attacker;  // 突刺的攻击者（识破反噬对象）
    private float timer;
    private float duration;
    private float postureGain;
    private bool finisherWindow;

    public MikiriCounterState(CharacterBody body, HierarchicalState parent, HitData hit) : base(body)
    {
        this.parent = parent;
        this.attacker = hit.attacker;
        // 时长/架势收益从 Config 读（SO 数值，禁止硬编码）
        if (body.Config != null)
        {
            duration = body.Config.MikiriDuration;
            postureGain = body.Config.MikiriPostureGain;
        }
        else
        {
            duration = 0.8f;
            postureGain = 30f;
        }
    }

    public override void OnEnter()
    {
        timer = 0f;

        // 播踩刀动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Mikiri", 0.05f);

        // 识破成功：大幅涨攻击者架势（只狼核心：踩刀反制）
        finisherWindow = false;
        if (attacker != null)
        {
            finisherWindow = attacker.AccumulatePosture(
                postureGain,
                allowBreak: true,
                source: PostureBreakSource.Mikiri);
        }

        // 表现：打铁音效/火花（Perfect 级别）
        CombatEventBus.TriggerWeaponDeflected(body.transform.position, DeflectType.Perfect);
    }

    public override void OnUpdate()
    {
        timer += Time.deltaTime;

        if (timer >= duration)
        {
            if (finisherWindow && attacker != null && attacker.IsPostureBroken)
            {
                attacker.RecoverFromBreak(0.8f);
            }
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    // 识破期间吞掉所有命令（不可打断）
    public override bool HandleCommand(ICommand cmd)
    {
        if (finisherWindow && cmd is AttackCommand)
        {
            CombatManager.Instance?.TryExecuteFinisher(body, FinisherKind.Mikiri);
        }
        return true;
    }
}
