using UnityEngine;

using ARPG.Boss;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
namespace ARPG.Boss.BehaviourTree
{

    // 远处追击、近处绕行；不锁定 Selector，使高优先级出招节点每帧都能抢占。
    public class BT_MoveToTarget : Node
    {
        private readonly CharacterBody body;
        private readonly Transform target;
        private readonly float stopDistance;
        private readonly float roamStrafeDuration;   // 多少秒换一次绕圈方向
        private readonly float roamStrafeStrength;    // 侧向切线分量（绕圈强度）
        private readonly float roamApproachStrength;  // 朝玩家逼近分量（<1 避免直接撞上去）
        private float roamTimer;
        private float roamSign = 1f;
        private bool downedOrbitLocked;
        private float downedOrbitSign = 1f;

        public BT_MoveToTarget(CharacterBody body, Transform target, float stopDistance,
            float roamStrafeDuration = 1.2f, float roamStrafeStrength = 0.8f,
            float roamApproachStrength = 0.35f)
        {
            this.body = body;
            this.target = target;
            this.stopDistance = stopDistance;
            this.roamStrafeDuration = roamStrafeDuration;
            this.roamStrafeStrength = roamStrafeStrength;
            this.roamApproachStrength = roamApproachStrength;
        }

        public override NodeState Evaluate()
        {
            // 硬直或朝向冻结时不发移动命令，避免重新切入走位状态并转向。
            if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked || body.IsCombatYawFrozen)
            {
                return NodeState.Running;
            }

            float distance = Vector3.Distance(body.transform.position, target.position);
            bool playerIncapacitated = BTUtil.IsTargetIncapacitated(target);

            if (distance <= stopDistance)
            {
                body.PreferFastWalk = false;
                return RoamAroundTarget(playerIncapacitated ? 0f : roamApproachStrength, playerIncapacitated);
            }

            // 过远用 Walk 快跑拉近，不要用 Walk_Strafe 慢挪。
            body.PreferFastWalk = true;
            Vector3 dir3D = (target.position - body.transform.position).normalized;
            Vector2 moveDir = new Vector2(dir3D.x, dir3D.z);
            body.MoveUsesWorldDir = true;
            body.TryExecuteCommand(new MoveCommand(moveDir));

            return NodeState.Running;
        }

        // 玩家失能时 approach=0，只绕圈不踩上去；方向锁定避免周期性换边像来回走。
        private NodeState RoamAroundTarget(float approach, bool orbitOnly)
        {
            Vector3 toTarget = target.position - body.transform.position;
            toTarget.y = 0f;

            // 走位方向可为侧向，但朝向始终跟随玩家。
            if (toTarget.sqrMagnitude > 0.001f)
            {
                Vector3 face = toTarget.normalized;
                body.RotateYaw(face, body.Config.RotationSpeed);
            }

            Vector3 right = toTarget.sqrMagnitude > 0.001f
                ? Vector3.Cross(Vector3.up, toTarget.normalized)
                : body.transform.right;

            Vector3 roamDir;
            if (orbitOnly)
            {
                if (!downedOrbitLocked)
                {
                    downedOrbitLocked = true;
                    downedOrbitSign = roamSign >= 0f ? 1f : -1f;
                }

                roamDir = right * downedOrbitSign;
            }
            else
            {
                downedOrbitLocked = false;
                roamTimer += Time.deltaTime;
                if (roamTimer >= roamStrafeDuration)
                {
                    roamTimer = 0f;
                    roamSign = -roamSign;
                }

                roamDir = right * (roamSign * roamStrafeStrength)
                    + toTarget.normalized * approach;
            }

            if (roamDir.sqrMagnitude > 0.0001f)
                roamDir.Normalize();

            Vector2 moveDir = new Vector2(roamDir.x, roamDir.z);

            body.MoveUsesWorldDir = true;
            body.TryExecuteCommand(new MoveCommand(moveDir));

            return NodeState.Running;
        }
    }
}
