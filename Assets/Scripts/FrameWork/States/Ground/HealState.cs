using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.Player;
namespace ARPG.FrameWork.States.Ground
{

    // 移动喝药：Base Layer 用慢走根运动，UpperBody Layer 播 Drink。
    // 只放行移动意图；受击仍会打断，OnExit 负责清理上半身层。
    public class HealState : BaseState
    {
        private const string UpperLayerName = "UpperBody";
        private const string DrinkState = "Drink_UpperBody";
        private const string EmptyState = "UpperBody_Empty";
        private const string SlowWalkState = "Walk_Slow_Strafe";

        private readonly HierarchicalState parent;
        private int upperLayerIndex = -1;
        private bool drinkAnimationSeen;
        private string currentBaseState;
        private readonly float rotationSpeed;

        public HealState(CharacterBody body, HierarchicalState parent) : base(body)
        {
            this.parent = parent;
            rotationSpeed = body.Config.RotationSpeed;
        }

        public override void OnEnter()
        {
            drinkAnimationSeen = false;
            currentBaseState = null;

            upperLayerIndex = body.Animator.GetLayerIndex(UpperLayerName);
            if (upperLayerIndex < 0 ||
                !AnimUtil.HasState(body.Animator, DrinkState, upperLayerIndex))
            {
                Debug.LogError(
                    $"{body.name} 的 Animator 缺少 {UpperLayerName}/{DrinkState}，已取消喝药。");
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                return;
            }

            // UseGourd 在入场时就扣次数并加血，不等待动画事件；之后受击打断也不会退还。
            if (!body.UseGourd())
            {
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                return;
            }

            body.IsHealing = true;
            body.Animator.SetLayerWeight(upperLayerIndex, 1f);
            // 默认态可能已经停在喝药末帧，CrossFade 同状态不会重播。
            AnimUtil.TryPlay(body.Animator, DrinkState, upperLayerIndex);
            UpdateStrafeParams(instant: true);
            PlayBaseLocomotion(force: true);
        }

        public override void OnUpdate()
        {
            UpdateFacingAndMovement();
            PlayBaseLocomotion(force: false);

            AnimatorStateInfo info =
                body.Animator.GetCurrentAnimatorStateInfo(upperLayerIndex);
            if (AnimUtil.IsPlaying(info, DrinkState))
            {
                // 先确认确实进入过喝药动画，再等待播放结束，避免融合期间读到旧状态就立刻退出。
                drinkAnimationSeen = true;
                if (info.normalizedTime >= 0.95f)
                {
                    FinishHeal();
                }
            }
            else if (drinkAnimationSeen &&
                     !body.Animator.IsInTransition(upperLayerIndex))
            {
                FinishHeal();
            }
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is MoveCommand moveCmd)
            {
                body.MoveDirection = moveCmd.Direction;
            }
            return true;
        }

        public override void OnExit()
        {
            // 上半身层权重恢复为 0，否则退出后仍可能覆盖攻击或待机的上半身动作。
            body.IsHealing = false;
            if (upperLayerIndex >= 0 &&
                upperLayerIndex < body.Animator.layerCount)
            {
                if (AnimUtil.HasState(body.Animator, EmptyState, upperLayerIndex))
                {
                    AnimUtil.TryPlay(body.Animator, EmptyState, upperLayerIndex);
                }
                body.Animator.SetLayerWeight(upperLayerIndex, 0f);
            }
        }

        private void FinishHeal()
        {
            if (body.MoveDirection.sqrMagnitude >= 0.01f)
            {
                parent.SubStateMachine.ChangeState(
                    new MoveState(body, parent, null));
            }
            else
            {
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            }
        }

        private void PlayBaseLocomotion(bool force)
        {
            string wanted = body.MoveDirection.sqrMagnitude >= 0.01f
                ? SlowWalkState
                : "Idle";
            if (!force && wanted == currentBaseState) return;

            if (!AnimUtil.HasState(body.Animator, wanted))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少喝药移动状态：{wanted}");
                return;
            }

            currentBaseState = wanted;
            AnimUtil.TryCrossFadeInFixedTime(body.Animator, wanted, 0.08f);
        }

        private void UpdateFacingAndMovement()
        {
            bool locked = IsLockedOnTarget();
            Vector2 input = body.MoveDirection;
            if (input.sqrMagnitude < 0.01f)
            {
                SetStrafe(0f, 0f, instant: false);
                if (locked)
                {
                    FaceTarget();
                }
                return;
            }

            if (locked)
            {
                UpdateStrafeParams(instant: false);
                FaceTarget();
            }
            else
            {
                SetStrafe(0f, input.magnitude, instant: false);
                body.RotateYaw(body.InputToWorldDir(input), rotationSpeed);
            }
        }

        private void UpdateStrafeParams(bool instant)
        {
            if (!IsLockedOnTarget())
            {
                Vector2 input = body.MoveDirection;
                SetStrafe(0f, input.magnitude, instant);
                return;
            }

            Vector3 world = body.InputToWorldDir(body.MoveDirection);
            Vector3 toBoss =
                LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.001f)
            {
                SetStrafe(0f, 0f, instant);
                return;
            }

            toBoss.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toBoss);
            SetStrafe(
                Vector3.Dot(world, right),
                Vector3.Dot(world, toBoss),
                instant);
        }

        private void SetStrafe(float x, float z, bool instant)
        {
            body.SetMoveStrafe(x, z, instant);
        }

        private void FaceTarget()
        {
            Vector3 direction =
                LockOnManager.Instance.Target.position - body.transform.position;
            direction.y = 0f;
            body.RotateYaw(direction, rotationSpeed);
        }

        private static bool IsLockedOnTarget()
        {
            return LockOnManager.Instance != null &&
                   LockOnManager.Instance.IsLockedOn &&
                   LockOnManager.Instance.Target != null;
        }
    }

}
