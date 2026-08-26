using UnityEngine;

// 移动状态（全权根运动）
//   未锁定：IdleToWalk / DodgeToWalk → Walk，身体转向移动方向
//   锁定：IdleToStrafe → Walk_Strafe，身体朝 Boss，MoveX/MoveZ 驱动四向融合树
public class MoveState : BaseState
{
    private HierarchicalState parent;
    private readonly string enterAnim;
    private float rotationSpeed = 720f;
    private float enterTimer;
    private float enterDuration = 0.45f;
    private bool inEnterTransition;
    private bool wasLocked;

    public MoveState(CharacterBody body, HierarchicalState parent, string enterAnim = "IdleToWalk") : base(body)
    {
        this.parent = parent;
        this.enterAnim = enterAnim;
        if (body.Config != null)
        {
            rotationSpeed = body.Config.RotationSpeed;
        }
    }

    public override void OnEnter()
    {
        enterTimer = 0f;
        // 没有起步 Clip（Boss 没有 IdleToWalk）就直接循环走，避免 CrossFade 静默失败站着滑
        inEnterTransition = AnimUtil.HasState(body.Animator, enterAnim);
        wasLocked = IsLockedOnTarget();
        UpdateStrafeParams(instant: true);
        AnimUtil.TryCrossFade(body.Animator, inEnterTransition ? enterAnim : LoopAnim, 0.1f);
    }

    public override void OnUpdate()
    {
        bool locked = IsLockedOnTarget();
        UpdateStrafeParams(instant: false);

        if (locked != wasLocked)
        {
            wasLocked = locked;
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

    private string LoopAnim => IsLockedOnTarget() ? "Walk_Strafe" : "Walk";

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

        if (cmd is DeflectCommand)
        {
            parent.SubStateMachine.ChangeState(new DeflectState(body, parent));
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
