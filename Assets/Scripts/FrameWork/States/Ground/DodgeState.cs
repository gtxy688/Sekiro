using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.Player;
namespace ARPG.FrameWork.States.Ground
{

    // 闪避（垫步）状态：全权根运动，位移由垫步动画 Root 曲线驱动，代码只计时退出
    // 无敌帧（M4）：前 DodgeIFrame 秒内普通攻击打不中
    // 识破：仅无方向垫步踩中突刺；锁定四向垫步不用融合树
    public class DodgeState : BaseState
    {
        private HierarchicalState parent;
        private float dodgeTimer;
        private float dodgeDuration;
        private float iFrameDuration;
        private readonly float entryBlendDuration;
        private bool lockedDodge;
        private bool mikiriEligible;

        public DodgeState(
            CharacterBody body,
            HierarchicalState parent,
            float entryBlendDuration = 0f) : base(body)
        {
            this.parent = parent;
            this.entryBlendDuration = Mathf.Max(0f, entryBlendDuration);
            dodgeDuration = body.Config != null ? body.Config.DodgeDuration : 0.5f;
            iFrameDuration = body.Config != null ? body.Config.DodgeIFrame : 0.3f;
        }

        public override void OnEnter()
        {
            if (parent == null)
                parent = body.MainStateMachine.CurrentState as HierarchicalState;

            dodgeTimer = 0f;
            lockedDodge = IsLockedOnTarget();
            // 进垫步当下有没有方向键：识破只认无方向，不看垫步动画叫前还是后
            mikiriEligible = body.MoveDirection.sqrMagnitude < 0.01f;

            body.ClearSteerYaw();
            if (lockedDodge)
            {
                // 锁定向 Boss 对准后，垫步 Root 只负责位移；Root yaw 与代码转向打架会滑步/拧身
                body.SetSuppressRootYaw(true);
                FaceTargetInstant();
                string anim = ResolveLockedDodgeAnim();
                PlayDodgeAnimation(anim);
            }
            else
            {
                body.SetSuppressRootYaw(false);
                PlayDodgeAnimation("Dodge");
            }
        }

        public override void OnUpdate()
        {
            dodgeTimer += Time.deltaTime;

            // 锁定垫步保持朝向 Boss；RotateYaw 在 suppressRootYaw 下不工作，用 SnapYaw
            if (lockedDodge && IsLockedOnTarget())
                FaceTarget();

            if (dodgeTimer >= dodgeDuration)
            {
                if (parent == null)
                {
                    body.EnterGrounded("dodge: finished without parent");
                    return;
                }
                if (body.MoveDirection.sqrMagnitude > 0.01f)
                {
                    bool locked = IsLockedOnTarget();
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

        public override void OnExit()
        {
            body.SetSuppressRootYaw(false);
        }

        // 受击拦截（M1/M17/M4）：
        //   突刺危字 + 无方向垫步 → 识破（踩刀）
        //   带方向垫步（含后垫）只走无敌帧，不识破，避免后垫踩刀把 Boss 拧成反向
        public override bool OnHitReceived(HitData hit)
        {
            if (mikiriEligible && hit.isPerilous && hit.perilousType == PerilousType.Thrust)
            {
                parent.SubStateMachine.ChangeState(new MikiriCounterState(body, parent, hit));
                return true;
            }

            if (dodgeTimer <= iFrameDuration)
            {
                return true;
            }
            return false;
        }

        // 相对 Boss：+Z 前、−Z 后、−X 左、+X 右。无输入默认前垫（踩刀方向）。斜向取绝对值更大的轴。
        private string ResolveLockedDodgeAnim()
        {
            Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;

            Vector3 world = body.InputToWorldDir(body.MoveDirection);
            if (toBoss.sqrMagnitude < 0.001f || world.sqrMagnitude < 0.01f)
                return "Dodge_Forward";

            toBoss.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, toBoss);
            float x = Vector3.Dot(world, right);
            float z = Vector3.Dot(world, toBoss);

            if (Mathf.Abs(x) > Mathf.Abs(z))
                return x < 0f ? "Dodge_Left" : "Dodge_Right";
            return z < 0f ? "Dodge_Back" : "Dodge_Forward";
        }

        private void PlayDodgeAnimation(string anim)
        {
            // 只有 Mid/Heavy 倒地取消带入固定秒数；普通锁定垫步保留原来的硬切响应。
            if (entryBlendDuration > 0f)
            {
                AnimUtil.TryCrossFadeInFixedTime(body.Animator, anim, entryBlendDuration);
                return;
            }

            if (lockedDodge)
            {
                if (!AnimUtil.TryPlay(body.Animator, anim))
                    AnimUtil.TryCrossFade(body.Animator, anim, 0.05f);
                return;
            }

            AnimUtil.TryCrossFade(body.Animator, anim, 0.05f);
        }

        private void FaceTargetInstant()
        {
            Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.001f) return;
            body.SnapYaw(toBoss);
        }

        private void FaceTarget()
        {
            Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;
            if (toBoss.sqrMagnitude < 0.001f) return;
            body.SnapYaw(toBoss);
        }

        private static bool IsLockedOnTarget()
        {
            return LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
                && LockOnManager.Instance.Target != null;
        }
    }

}
