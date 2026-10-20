using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States.Base;
using ARPG.FrameWork.States.Ground;
namespace ARPG.FrameWork.States
{

    // CharacterBody.ReceiveHit 结算命中后，需要播放普通受击时进入的顶层父状态。
    // 玩家按命中的 HitGrade 区分轻伤、中击倒地、重击倒地；Boss 按 HurtContext 区分反应。
    // 本层管输入取消和再次受击，GroundStunnedState 管动画/计时；恢复或取消时离开整个受击分支。
    public class StunnedState : HierarchicalState
    {
        // 两个构造入口决定判定方式；不把 Boss 的动画语境当成玩家的受击等级。
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

        // 虽然名为 GroundStunnedState，这个子状态也负责空中受到普通命中时的受击表现。
        protected override BaseState GetInitialSubState()
        {
            body.IsKnockedDown = false;
            return new GroundStunnedState(body, this, context, useHitGrade, grade);
        }

        // 父层先接收输入命令：通常消费掉，只有需要让输入缓冲稍后重试的垫步返回 false。
        protected override bool OnParentHandleCommand(ICommand cmd)
        {
            GroundStunnedState ground = SubStateMachine.CurrentState as GroundStunnedState;
            if (useHitGrade && cmd is DodgeCommand)
            {
                if (ground != null && ground.CanDodgeCancel)
                {
                    body.IsKnockedDown = false;
                    // 倒地取消单独配置动画融合时间，轻受击则沿用 DodgeState 的默认融合。
                    float blendDuration = ground.IsKnockdown
                        ? body.Config.KnockdownToDodgeBlendDuration
                        : 0f;
                    body.EnterGrounded(new DodgeState(body, null, blendDuration),
                        "stunned: dodge cancel opened");
                    return true;
                }
                // 受击计时未到开放点：返回 false，输入缓冲仍可在有效期内再次尝试。
                return false;
            }
            if (useHitGrade && cmd is DeflectCommand deflectCmd)
            {
                if (ground != null && ground.CanMidToGuard)
                {
                    // Mid 的倒地前段可转入专用起身进防；这不同于倒地播完后的 StandingState。
                    // 这里只传递 PressId，主动弹反窗口要等真正进入 DeflectState 时才申请开启。
                    body.EnterGrounded(new MidToGuardState(body, deflectCmd.PressId),
                        "stunned: mid to guard cancel");
                    return true;
                }
                // 轻受击且已落地时可直接抬刀，不需要经过 Mid 的起身进防动画。
                if (ground != null && ground.CanLightGuardCancel)
                {
                    body.EnterGrounded(new DeflectState(body, null, pressId: deflectCmd.PressId),
                        "stunned: light guard cancel");
                    return true;
                }
            }
            return true;
        }

        // 再次命中已到受击分支，由这里负责结算；返回 true 后 ReceiveHit 不会再扣一次伤害。
        protected override bool OnParentHandleHit(HitData hit)
        {
            // Boss 的 HurtContext 分支直接拦截后续命中，不应用玩家的连续受击刷新规则。
            if (!useHitGrade)
                return true;

            // 投技先扣本次伤害，仍存活才请求 CombatManager 接管双方的成对演出。
            if (hit.perilousType == PerilousType.Grab)
            {
                body.TakeDamage(hit.healthDmg, hit.postureDmg);
                if (body.CurrentHP <= 0) return true;
                CombatManager.Instance.TryStartGrabThrow(hit.attacker, body);
                return true;
            }

            body.TakeDamage(hit.healthDmg, hit.postureDmg);
            // TakeDamage 可能已经切入死亡或架势崩解，不能再用受击动画覆盖新状态。
            if (body.CurrentHP <= 0 || body.IsPostureBroken)
                return true;

            // 伤害结算与动画刷新分开：即使命中不刷新硬直，数值也已在上面结算。
            GroundStunnedState ground = SubStateMachine.CurrentState as GroundStunnedState;
            if (ground != null)
                ground.ReceiveFollowUpHit(hit);
            return true;
        }
    }

}
