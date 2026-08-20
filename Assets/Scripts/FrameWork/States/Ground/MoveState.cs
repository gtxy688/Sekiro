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
        body.Animator.CrossFade(inEnterTransition ? enterAnim : LoopAnim, 0.1f);
    }

    public override void OnUpdate()
    {
        bool locked = IsLockedOnTarget();
        UpdateStrafeParams(instant: false);

        if (locked != wasLocked)
        {
            wasLocked = locked;
            inEnterTransition = false;
            body.Animator.CrossFade(LoopAnim, 0.1f);
        }

        if (inEnterTransition)
        {
            enterTimer += Time.deltaTime;
            var info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if ((AnimUtil.IsPlaying(info, enterAnim) && info.normalizedTime >= 0.95f) || enterTimer >= enterDuration)
            {
                inEnterTransition = false;
                body.Animator.CrossFade(LoopAnim, 0.08f);
            }
        }

        Vector2 inputDir = body.MoveDirection;
        if (inputDir.sqrMagnitude < 0.01f) return;

        Vector3 moveDir;
        if (locked)
        {
            Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;
            moveDir = toBoss.sqrMagnitude > 0.001f ? toBoss.normalized : body.transform.forward;
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
        if (!IsLockedOnTarget()) return;

        Vector3 world = body.InputToWorldDir(body.MoveDirection);
        Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
        toBoss.y = 0f;
        if (toBoss.sqrMagnitude < 0.001f)
        {
            SetStrafe(0f, 0f, instant);
            return;
        }

        toBoss.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, toBoss);
        SetStrafe(Vector3.Dot(world, right), Vector3.Dot(world, toBoss), instant);
    }

    private void SetStrafe(float x, float z, bool instant)
    {
        if (instant)
        {
            body.Animator.SetFloat("MoveX", x);
            body.Animator.SetFloat("MoveZ", z);
        }
        else
        {
            body.Animator.SetFloat("MoveX", x, 0.1f, Time.deltaTime);
            body.Animator.SetFloat("MoveZ", z, 0.1f, Time.deltaTime);
        }
    }

    private bool IsLockedOnTarget()
    {
        return LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
            && LockOnManager.Instance.Target != null;
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
