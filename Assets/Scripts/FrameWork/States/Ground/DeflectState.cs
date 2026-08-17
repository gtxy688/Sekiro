using UnityEngine;

// 防御/弹反状态（M4，方案 B：只狼式输入）
//   短按（<0.15s）= 弹反：进入即开始弹反窗口（默认 0.3s，抖刀惩罚可缩短），松手播甩刀动画
//   长按（≥0.15s）= 格挡：持续举刀防御，不扣血只涨架势（×GuardPostureFactor）
//   完美弹反：攻击者涨架势（DeflectPostureGain）+ 被弹开硬直（ForceParryStun）；
//             自己涨少量架势（×DeflectSelfPostureFactor）但永不因此崩防
//   危字攻击：防御/弹反无效，放行硬吃（策划案 4.5）
public class DeflectState : BaseState
{
    private HierarchicalState parent;
    private float enterTime;        // 进入时间（弹反窗口起点）
    private float window;           // 有效弹反窗口（含抖刀惩罚）
    private bool hasReleased;       // 短按后已松手：甩刀动画播完窗口结束就回待机
    private float guardFlinchTimer; // >0 正在播格挡受击动画，结束后回格挡姿态

    public DeflectState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
    }

    public override void OnEnter()
    {
        enterTime = Time.time;
        hasReleased = false;
        guardFlinchTimer = 0f;

        // 抖刀惩罚登记（0.5s 内连点 ≥3 次 → 窗口 ×0.75，下限 0.1s），并取当前窗口
        body.RegisterDeflectPress();
        window = body.GetDeflectWindow();

        // 格挡姿态（举刀）动画（占位名，M8 接动画前）
        body.Animator.CrossFade("Deflect_Guard", 0.05f);

        // 格挡标记：架势回复 ×5（M9）
        body.IsGuarding = true;
    }

    public override void OnUpdate()
    {
        // 格挡受击的顿挫动画播完 → 回格挡姿态
        if (guardFlinchTimer > 0f)
        {
            guardFlinchTimer -= Time.deltaTime;
            if (guardFlinchTimer <= 0f)
            {
                body.Animator.CrossFade("Deflect_Guard", 0.05f);
            }
        }

        // 短按已松手且弹反窗口结束 → 回待机
        if (hasReleased && Time.time - enterTime >= window)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    public override void OnExit()
    {
        body.IsGuarding = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        // 松手（PlayerBrain 在 Deflect.canceled 发 IdleCommand）
        if (cmd is IdleCommand)
        {
            float held = Time.time - enterTime;
            if (held < 0.15f)
            {
                // 短按 → 弹反甩刀动画，窗口保持到结束再回待机
                hasReleased = true;
                body.Animator.CrossFade("Deflect_Slash", 0.05f);
            }
            else
            {
                // 长按松手 → 直接回待机
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            }
            return true;
        }

        // 防御姿态期间吞掉其他所有命令（移动/攻击都不可用）
        return true;
    }

    // 受击拦截（M4 核心）
    public override bool OnHitReceived(HitData hit)
    {
        // 危字攻击（突刺/横扫）不可防御：放行硬吃（策划案 4.5）
        if (hit.isPerilous) return false;

        float elapsed = Time.time - enterTime;

        // ===== 弹反窗口内 → 完美弹反 =====
        if (elapsed <= window)
        {
            // 1. 攻击者涨架势（只狼核心：完美弹反反噬架势）+ 被弹开硬直
            if (hit.attacker != null)
            {
                float gain = body.Config != null ? body.Config.DeflectPostureGain : 30f;
                hit.attacker.AccumulatePosture(gain);
                hit.attacker.ForceParryStun();
            }

            // 2. 自己涨少量架势，但永不因此崩防（allowBreak=false）
            float self = hit.postureDmg * (body.Config != null ? body.Config.DeflectSelfPostureFactor : 0.3f);
            body.AccumulatePosture(self, allowBreak: false);

            // 3. 表现：完美弹反音效/火花 + 顿帧 + 轻震屏
            CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Perfect);
            CombatEventBus.TriggerCameraShake(0.3f);
            CombatManager.Instance?.HitStop();
            return true;
        }

        // ===== 窗口外 → 普通格挡（长按中）=====
        // 不扣血，只涨架势（比例系数），留在防御姿态
        float posture = hit.postureDmg * (body.Config != null ? body.Config.GuardPostureFactor : 0.5f);
        body.AccumulatePosture(posture);

        // 格挡受击顿挫动画：走受击动画映射接口（HurtContext.Guard，留空回退普通受击）
        body.Animator.CrossFade(body.ResolveHurtAnim(HurtContext.Guard), 0.03f);
        guardFlinchTimer = 0.25f;

        CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Normal);
        return true;
    }
}
