using UnityEngine;
public class AttackState : BaseState
{
    private readonly HierarchicalState parent;
    private readonly AttackConfig config; // 当前动作的全部数值与窗口均来自 SO

    private float stateTimer;

    // 构造函数只接收一个光盘（配置）
    public AttackState(CharacterBody body, HierarchicalState parent, AttackConfig config) : base(body)
    {
        this.parent = parent;
        this.config = config;
    }

    public override void OnEnter()
    {
        // 空配置保护：没有 AttackConfig 的 AttackState 无意义，立刻退回 Idle
        if (config == null)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            return;
        }

        stateTimer = 0f;
        body.IsAttackRecoveryOpen = false;

        // ActiveAttack 是 Brain 对“下一次攻击”的一次性选择，状态取得后立即清空。
        if (body.ActiveAttack == config)
        {
            body.ActiveAttack = null;
        }

        if (!ValidateWindows())
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            return;
        }

        body.Animator.CrossFade(config.AnimName, config.TransitionDuration);

        // 进攻击就开判定，刀碰到就算；退出时 OnExit 关
        body.EnableWeaponHit(config);

        // Boss AI 反制判定标记（M7 用，避免查状态类型）
        body.IsAttacking = true;

        // 危字攻击：发事件 → UI 弹"危"字提示（M17）
        if (config.Perilous != PerilousType.None)
        {
            CombatEventBus.TriggerPerilousAttack(config.Perilous);
        }
    }

    public override void OnUpdate()
    {
        if (config == null) return;

        stateTimer += Time.deltaTime;
        body.IsAttackRecoveryOpen = stateTimer >= config.RecoveryWindowStart;

        if (config.AllowRotation && stateTimer <= config.RotationWindowEnd)
        {
            RotateDuringAttack();
        }

        // 位移不在此处理：突进/前移完全由攻击动画的 Root 曲线驱动（全权根运动），
        // 代码只负责状态时长与连招窗口判定

        // 动作彻底结束
        if (stateTimer >= config.StateDuration)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    public override void OnExit()
    {
        body.DisableWeaponHit();
        body.IsAttacking = false;
        body.IsAttackRecoveryOpen = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        if (config == null) return false;

        // 前摇可以主动取消；命中段锁定；进入后摇后重新开放所有非攻击行为。
        bool canCancel = stateTimer < config.HitStartTime ||
                         stateTimer >= config.RecoveryWindowStart;

        if (cmd is DeflectCommand)
        {
            if (canCancel)
            {
                parent.SubStateMachine.ChangeState(new DeflectState(body, parent));
                return true;
            }
            return false;
        }

        if (cmd is DodgeCommand)
        {
            if (canCancel)
            {
                parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
                return true;
            }
            return false;
        }

        if (cmd is AttackCommand)
        {
            if (config.NextCombo != null &&
                stateTimer >= config.RecoveryWindowStart &&
                stateTimer <= config.ComboWindowEnd)
            {
                parent.SubStateMachine.ChangeState(
                    new AttackState(body, parent, config.NextCombo));
                return true;
            }
            return false;
        }

        if (cmd is MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            if (stateTimer >= config.RecoveryWindowStart &&
                moveCmd.Direction.sqrMagnitude >= 0.01f)
            {
                // 锁定攻击接移动时直接进入四向循环。
                // 若走默认 IdleToWalk，其前向根运动会让角色额外朝目标冲出一段。
                string enterAnim = HasCombatTarget() ? null : "IdleToWalk";
                parent.SubStateMachine.ChangeState(
                    new MoveState(body, parent, enterAnim));
            }
            return true;
        }

        return false;
    }

    private bool ValidateWindows()
    {
        bool valid =
            config.HitStartTime >= 0f &&
            config.HitStartTime <= config.RecoveryWindowStart &&
            config.RecoveryWindowStart <= config.ComboWindowEnd &&
            config.ComboWindowEnd <= config.StateDuration &&
            config.RotationWindowEnd >= 0f &&
            config.RotationWindowEnd <= config.StateDuration;

        if (!valid)
        {
            Debug.LogError(
                $"{config.name} 的攻击窗口非法，必须满足 " +
                "0 <= HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration，" +
                "且 RotationWindowEnd 位于动作时长内。");
        }
        return valid;
    }

    private void RotateDuringAttack()
    {
        Vector3 direction;
        Transform target = ResolveCombatTarget();

        if (target != null)
        {
            direction = target.position - body.transform.position;
        }
        else if (body.MoveUsesWorldDir)
        {
            Vector2 input = body.MoveDirection;
            direction = new Vector3(input.x, 0f, input.y);
        }
        else
        {
            direction = body.InputToWorldDir(body.MoveDirection);
        }

        direction.y = 0f;
        body.RotateYaw(direction, config.RotationSpeed);
    }

    private bool HasCombatTarget()
    {
        return ResolveCombatTarget() != null;
    }

    private Transform ResolveCombatTarget()
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
}
