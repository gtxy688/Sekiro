using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // Elbow 投技成对演出：双方播 Elbow_Danger，锁命令，播完由 CombatManager 一起解锁。
    public class GrabThrowState : BaseState
    {
        public const string AnimName = "Elbow_Danger";
        const float FallbackDuration = 4f;

        private bool hasSeenAnim;
        private bool hasCompleted;
        private float timer;

        public GrabThrowState(CharacterBody body) : base(body) { }

        public override void OnEnter()
        {
            hasSeenAnim = false;
            hasCompleted = false;
            timer = 0f;
            body.IsFinisherLocked = true;
            body.IsAttacking = false;
            body.AttackUninterruptible = false;
            body.IsAttackRecoveryOpen = false;
            body.ActiveAttack = null;
            body.CurrentMoveEntry = null;
            body.CurrentMoveWindow = null;
            body.MoveDirection = Vector3.zero;
            body.DisableWeaponHit();
            body.ClearSteerYaw();
            if (body.Rb != null)
            {
                Vector3 v = body.Rb.velocity;
                body.Rb.velocity = new Vector3(0f, v.y, 0f);
            }

            if (!AnimUtil.TryCrossFade(body.Animator, AnimName, 0.05f)
                && !AnimUtil.TryPlay(body.Animator, AnimName))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少投技状态：{AnimName}");
            }
        }

        public override void OnExit()
        {
            body.IsFinisherLocked = false;
        }

        public override void OnUpdate()
        {
            if (hasCompleted) return;

            body.MoveDirection = Vector3.zero;
            timer += Time.deltaTime;

            if (body.Animator != null)
            {
                AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if (AnimUtil.IsPlaying(info, AnimName))
                {
                    hasSeenAnim = true;
                    if (info.normalizedTime < 0.98f && timer < FallbackDuration)
                        return;
                }
                else if (!hasSeenAnim)
                {
                    if (timer < FallbackDuration) return;
                }
                else if (body.Animator.IsInTransition(0) && timer < FallbackDuration)
                {
                    return;
                }
            }
            else if (timer < FallbackDuration)
            {
                return;
            }

            hasCompleted = true;
            if (CombatManager.Instance != null)
                CombatManager.Instance.CompleteGrabThrow(body);
            else
                body.MainStateMachine.ChangeState(new GroundedState(body));
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

}
