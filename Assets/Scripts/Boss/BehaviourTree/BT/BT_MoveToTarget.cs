using UnityEngine;

// 走位节点：远距离追击玩家；进入攻击距离后不再立正站桩，改为绕玩家侧向走位
// （缓步逼近 + 周期性换边绕圈），保持面向玩家。选招阶段（PickActive 全冷却/抽不到）
// 由本节点撑起"活人感"。返回 Running 但**不是 ISelectorLock**，Selector 每帧重评，
// PickActive 冷却转好后照样能抢走出招。
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
        float distance = Vector3.Distance(body.transform.position, target.position);

        if (distance <= stopDistance)
        {
            return RoamAroundTarget();
        }

        Vector3 dir3D = (target.position - body.transform.position).normalized;
        Vector2 moveDir = new Vector2(dir3D.x, dir3D.z);
        body.MoveUsesWorldDir = true;
        body.TryExecuteCommand(new MoveCommand(moveDir));

        return NodeState.Running;
    }

    // 近距离走位：始终看着玩家，脚步绕他侧向画圈（周期换边），并带一点前压。
    // 这样选招等待不再是"罚站"，玩家也能明显感到 Boss 一直在试探性地压迫。
    private NodeState RoamAroundTarget()
    {
        Vector3 toTarget = target.position - body.transform.position;
        toTarget.y = 0f;

        // 保持面向玩家（侧向走也盯着他，像只狼 Boss 的试探步）
        if (toTarget.sqrMagnitude > 0.001f)
        {
            Vector3 face = toTarget.normalized;
            body.RotateYaw(face, body.Config != null ? body.Config.RotationSpeed : 720f);
        }

        // 绕圈方向周期性换边，避免绕着一个方向转圈僵化
        roamTimer += Time.deltaTime;
        if (roamTimer >= roamStrafeDuration)
        {
            roamTimer = 0f;
            roamSign = -roamSign;
        }

        Vector3 right = toTarget.sqrMagnitude > 0.001f
            ? Vector3.Cross(Vector3.up, toTarget.normalized)
            : body.transform.right;

        Vector3 roamDir = right * (roamSign * roamStrafeStrength)
            + toTarget.normalized * roamApproachStrength;
        Vector2 moveDir = new Vector2(roamDir.x, roamDir.z);

        body.MoveUsesWorldDir = true;
        body.TryExecuteCommand(new MoveCommand(moveDir));

        return NodeState.Running;
    }
}