using UnityEngine;

using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.Player;
namespace ARPG.FrameWork.States.Ground
{

    // 用根运动驱动移动，未锁定时转向移动方向，锁定时面向战斗目标并更新四向动画参数。
    // 可播放起步过渡，Boss 的 PreferFastWalk 选择快走或近身慢走循环。
    public class MoveState : BaseState
    {
        private HierarchicalState parent;
        private readonly string enterAnim;
        private readonly float rotationSpeed;
        private float enterTimer;
        private float enterDuration = 0.45f;
        private bool inEnterTransition;
        private bool wasLocked;
        private bool wasFastWalk;

        public MoveState(CharacterBody body, HierarchicalState parent, string enterAnim = "IdleToWalk") : base(body)
        {
            this.parent = parent;
            this.enterAnim = enterAnim;
            rotationSpeed = body.Config.RotationSpeed;
        }

        public override void OnEnter()
        {
            enterTimer = 0f;
            // 没有起步 Clip（Boss 没有 IdleToWalk）就直接循环走，避免 CrossFade 静默失败站着滑
            inEnterTransition = AnimUtil.HasState(body.Animator, enterAnim);
            wasLocked = IsLockedOnTarget();
            wasFastWalk = body.PreferFastWalk;
            UpdateStrafeParams(instant: true);
            AnimUtil.TryCrossFade(body.Animator, inEnterTransition ? enterAnim : LoopAnim, 0.1f);
        }

        public override void OnUpdate()
        {
            bool locked = IsLockedOnTarget();
            UpdateStrafeParams(instant: false);

            if (locked != wasLocked || body.PreferFastWalk != wasFastWalk)
            {
                wasLocked = locked;
                wasFastWalk = body.PreferFastWalk;
                inEnterTransition = false;
                AnimUtil.TryCrossFade(body.Animator, LoopAnim, 0.1f);
            }

            if (inEnterTransition)
            {
                enterTimer += Time.deltaTime;
                var info = body.Animator.GetCurrentAnimatorStateInfo(0);
                if ((AnimUtil.IsPlaying(info, enterAnim) && info.normalizedTime >= 0.95f) || enterTimer >= enterDuration)
                {
                    inEnterTransition = false;
                    AnimUtil.TryCrossFade(body.Animator, LoopAnim, 0.08f);
                }
            }

            Vector2 inputDir = body.MoveDirection;
            if (inputDir.sqrMagnitude < 0.01f) return;

            Vector3 moveDir;
            if (locked)
            {
                Transform target = GetCombatTarget();
                Vector3 toTarget = target.position - body.transform.position;
                toTarget.y = 0f;
                moveDir = toTarget.sqrMagnitude > 0.001f ? toTarget.normalized : body.transform.forward;
            }
            else if (body.MoveUsesWorldDir)
            {
                moveDir = new Vector3(inputDir.x, 0f, inputDir.y);
                if (moveDir.sqrMagnitude > 0.001f) moveDir.Normalize();
            }
            else
            {
                moveDir = body.InputToWorldDir(inputDir);
            }

            body.RotateYaw(moveDir, rotationSpeed);
        }

        // Walk_Strafe = 锁定/近身慢走绕圈；Walk = 未锁定或 Boss 远距离快跑拉近。
        private string LoopAnim => body.PreferFastWalk || !IsLockedOnTarget() ? "Walk" : "Walk_Strafe";

        // 将世界移动方向投影到目标相对的左右与前后轴，供四向融合树使用。
        private void UpdateStrafeParams(bool instant)
        {
            Transform target = GetCombatTarget();
            if (target == null) return;

            Vector2 input = body.MoveDirection;
            Vector3 world = body.MoveUsesWorldDir
                ? new Vector3(input.x, 0f, input.y)
                : body.InputToWorldDir(input);
            Vector3 toTarget = target.position - body.transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.001f)
            {
                SetStrafe(0f, 0f, instant);
                return;
            }

            toTarget.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toTarget);
            SetStrafe(Vector3.Dot(world, right), Vector3.Dot(world, toTarget), instant);
        }

        private void SetStrafe(float x, float z, bool instant)
        {
            body.SetMoveStrafe(x, z, instant);
        }

        private bool IsLockedOnTarget()
        {
            return GetCombatTarget() != null;
        }

        private Transform GetCombatTarget()
        {
            if (body.CombatTarget != null)
                return body.CombatTarget;

            if (LockOnManager.Instance != null &&
                LockOnManager.Instance.IsLockedOn)
            {
                return LockOnManager.Instance.Target;
            }
            return null;
        }

        public override void OnExit() { }

        public override bool HandleCommand(ICommand cmd)
        {
            if (cmd is MoveCommand moveCmd)
            {
                if (moveCmd.Direction.sqrMagnitude < 0.01f)
                {
                    body.MoveDirection = Vector2.zero;
                    parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                }
                else
                {
                    body.MoveDirection = moveCmd.Direction;
                }
                return true;
            }

            if (cmd is AttackCommand)
            {
                parent.SubStateMachine.ChangeState(new AttackState(body, parent, body.GetAttackConfig()));
                return true;
            }

            if (cmd is DeflectCommand deflectCmd)
            {
                parent.SubStateMachine.ChangeState(
                    new DeflectState(body, parent, pressId: deflectCmd.PressId));
                return true;
            }

            if (cmd is DodgeCommand)
            {
                parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
                return true;
            }
            return false;
        }
    }

}
