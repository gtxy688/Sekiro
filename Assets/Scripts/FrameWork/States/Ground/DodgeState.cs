using UnityEngine;

// 闪避（垫步）状态：全权根运动，位移由垫步动画 Root 曲线驱动，代码只计时退出
// 无敌帧（M4）：前 DodgeIFrame 秒内普通攻击打不中（危字突刺除外 → 识破优先）
// 锁定：按相对 Boss 的输入切四向一次性垫步，不用融合树（斜向混合会搅乱 Root）
public class DodgeState : BaseState
{
    private HierarchicalState parent;
    private float dodgeTimer;
    private float dodgeDuration;
    private float iFrameDuration;
    private float rotationSpeed = 720f;
    private bool lockedDodge;

    public DodgeState(CharacterBody body, HierarchicalState parent) : base(body)
    {
        this.parent = parent;
        dodgeDuration = body.Config != null ? body.Config.DodgeDuration : 0.5f;
        iFrameDuration = body.Config != null ? body.Config.DodgeIFrame : 0.3f;
        if (body.Config != null)
            rotationSpeed = body.Config.RotationSpeed;
    }

    public override void OnEnter()
    {
        dodgeTimer = 0f;
        lockedDodge = IsLockedOnTarget();

        if (lockedDodge)
        {
            FaceTargetInstant();
            body.Animator.CrossFade(ResolveLockedDodgeAnim(), 0.05f);
        }
        else
        {
            body.Animator.CrossFade("Dodge", 0.05f);
        }
    }

    public override void OnUpdate()
    {
        dodgeTimer += Time.deltaTime;

        // 锁定垫步保持朝向 Boss，让左右/后垫的 Root 始终在角色空间生效
        if (lockedDodge && IsLockedOnTarget())
            FaceTarget();

        if (dodgeTimer >= dodgeDuration)
        {
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

    // 受击拦截（M1/M17/M4）：
    //   突刺危字 + 垫步 → 触发识破（踩刀），拦截伤害（优先于无敌帧）
    //   普通攻击 + 无敌帧内 → 躲过（拦截伤害）
    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous && hit.perilousType == PerilousType.Thrust)
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

    // 相对 Boss：+Z 前、−Z 后、−X 左、+X 右。无输入默认后垫。斜向取绝对值更大的轴。
    private string ResolveLockedDodgeAnim()
    {
        Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
        toBoss.y = 0f;

        Vector3 world = body.InputToWorldDir(body.MoveDirection);
        if (toBoss.sqrMagnitude < 0.001f || world.sqrMagnitude < 0.01f)
            return "Dodge_Back";

        toBoss.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toBoss);
        float x = Vector3.Dot(world, right);
        float z = Vector3.Dot(world, toBoss);

        if (Mathf.Abs(x) > Mathf.Abs(z))
            return x < 0f ? "Dodge_Left" : "Dodge_Right";
        return z < 0f ? "Dodge_Back" : "Dodge_Forward";
    }

    private void FaceTargetInstant()
    {
        Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
        toBoss.y = 0f;
        if (toBoss.sqrMagnitude < 0.001f) return;
        body.transform.rotation = Quaternion.LookRotation(toBoss.normalized, Vector3.up);
    }

    private void FaceTarget()
    {
        Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
        toBoss.y = 0f;
        if (toBoss.sqrMagnitude < 0.001f) return;
        body.RotateYaw(toBoss.normalized, rotationSpeed);
    }

    private static bool IsLockedOnTarget()
    {
        return LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
            && LockOnManager.Instance.Target != null;
    }
}
