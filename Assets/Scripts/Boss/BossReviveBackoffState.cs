using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.FrameWork.States.Ground;
namespace ARPG.Boss
{

    // 玩家回生起身后的礼节垫步：Boss 退后一步再恢复出招（由 BTBrain 切入）。
    public class BossReviveBackoffState : BaseState
    {
        private HierarchicalState parent;
        private float timer;
        private const string BackAnim = "Dodge_Back";
        private const float FallbackDuration = 0.6f;

        public BossReviveBackoffState(CharacterBody body, HierarchicalState parent) : base(body)
        {
            this.parent = parent;
        }

        public override void OnEnter()
        {
            if (parent == null)
                parent = body.MainStateMachine.CurrentState as HierarchicalState;

            timer = 0f;
            body.IsAttacking = false;
            body.ClearSteerYaw();
            body.SetSuppressRootYaw(false);
            FacePlayer();

            if (!AnimUtil.TryCrossFade(body.Animator, BackAnim, 0.08f))
                AnimUtil.TryCrossFade(body.Animator, "Dodge", 0.08f);
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;
            FacePlayer();

            var info = body.Animator.GetCurrentAnimatorStateInfo(0);
            bool animDone = (AnimUtil.IsPlaying(info, BackAnim) || AnimUtil.IsPlaying(info, "Dodge"))
                            && info.normalizedTime >= 0.95f;
            if (animDone || timer >= FallbackDuration)
                ReturnIdle();
        }

        public override bool HandleCommand(ICommand cmd)
        {
            return true;
        }

        private void FacePlayer()
        {
            Transform target = body.CombatTarget;
            if (target == null) return;
            Vector3 to = target.position - body.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 0.001f) return;
            body.SnapYaw(to);
        }

        private void ReturnIdle()
        {
            if (parent != null)
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            else
                body.MainStateMachine.ChangeState(new GroundedState(body));
        }
    }

}
