using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.FrameWork.States.Dead
{

    // 倒地后等待回生选择，阶段计时结束才开放输入，选择等待不超时。
    // 攻击键回生并播放起身，防御键放弃回生进入真死。
    public class RevivePendingState : BaseState
    {
        private HierarchicalState parent;
        private float fallTimer;
        private float fallDuration = 1.2f;
        private float reviveTimer;
        private float reviveDuration = 1.5f;
        private bool lying;
        // choiceReady 区分“倒地动画还没结束”和“可以选择回生”；reviving 区分已选择后的起身过程。
        private bool reviving;
        private bool choiceReady;

        public RevivePendingState(CharacterBody body, HierarchicalState parent) : base(body)
        {
            this.parent = parent;
        }

        public override void OnEnter()
        {
            fallTimer = 0f;
            reviveTimer = 0f;
            lying = false;
            reviving = false;
            choiceReady = false;
            AnimUtil.TryCrossFade(body.Animator, "Dead", 0.1f);
        }

        public override void OnUpdate()
        {
            if (reviving)
            {
                reviveTimer += Time.deltaTime;
                if (reviveTimer >= reviveDuration)
                {
                    body.SetReviving(false);
                    body.EnterGrounded("revive: animation finished");
                }
                return;
            }

            if (!choiceReady)
                fallTimer += Time.deltaTime;

            if (!lying && IsFallFinished())
            {
                lying = true;
                AnimUtil.TryCrossFade(body.Animator, "Deading", 0.05f);
            }

            // 等倒地时长走完再开选项，避免动画提前结束时变暗期间就能按键
            if (lying && !choiceReady && fallTimer >= fallDuration)
            {
                choiceReady = true;
                CombatEventBus.TriggerReviveChoiceReady(body);
            }
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (reviving || !choiceReady) return true;

            if (cmd is AttackCommand)
            {
                BeginRevive();
                return true;
            }

            if (cmd is DeflectCommand)
            {
                GiveUp();
                return true;
            }

            return true;
        }

        private void BeginRevive()
        {
            // 选择时立即回满 HP 并扣回生次数，但起身动画期间仍置 IsReviving，AI 不应把人当作已恢复操作。
            body.SetReviving(true);
            body.Revive();
            reviving = true;
            reviveTimer = 0f;
            AnimUtil.TryCrossFade(body.Animator, "Revive", 0.1f);
        }

        public override void OnExit()
        {
            body.SetReviving(false);
        }

        private void GiveUp()
        {
            // 选择等待本身不超时；只有主动放弃才从可回生分支切到真死分支。
            CombatEventBus.TriggerDeath(body);
            body.EnterDead(false, alreadyDowned: true, reason: "revive: give up");
        }

        private bool IsFallFinished()
        {
            var info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, "Dead") && info.normalizedTime >= 0.95f) return true;
            return fallTimer >= fallDuration;
        }
    }

}
