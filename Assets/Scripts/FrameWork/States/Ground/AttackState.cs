using UnityEngine;
public class AttackState : BaseState
{
    private readonly HierarchicalState parent;
    private readonly AttackConfig config; // 当前动作的全部数值与窗口均来自 SO

    private float animTime;
    private bool weaponHitEnabled;
    private bool[] sfxFired;

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

        animTime = 0f;
        sfxFired = null;
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

        // 招式在 _Attack/_Bow/_Clash/_Danger 子状态机里，必须走 Parent.State。
        if (!AnimUtil.TryCrossFade(body.Animator, config.AnimName, config.TransitionDuration))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少攻击状态：{config.AnimName}");
        }

        // 前摇不开判定：贴身时刀还在蓄力就会扫到。到 HitStartTime / 第一段 pulse 再开。
        weaponHitEnabled = false;
        ApplyHitbox();

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

        animTime = AttackAnimClock.ReadSeconds(body.Animator, config.AnimName);
        body.IsAttackRecoveryOpen = animTime >= config.RecoveryWindowStart;

        ApplyHitbox();
        ApplySfx();

        if (config.AllowRotation && animTime <= config.RotationWindowEnd)
        {
            RotateDuringAttack();
        }

        // 位移不在此处理：突进/前移完全由攻击动画的 Root 曲线驱动（全权根运动），
        // 代码只负责状态时长与连招窗口判定

        // 动作彻底结束：stateDuration 到点，或（无连招的招）动画实际播完且判定段已全部关闭。
        // 后一条专治"stateDuration 大于实际 clip 长度"的末帧冻结罚站——射箭/收弓段最容易踩：
        // 动画播完停在最后一帧，animTime 却永远达不到 stateDuration，角色僵在原地等时长。
        bool animPlayedOut = config.NextCombo == null
            && animTime >= LastHitWindowEnd()
            && IsAnimFinished(config.AnimName);
        if (animTime >= config.StateDuration || animPlayedOut)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    // 最后一个判定窗结束时间：多段脉冲取末段 end，单段取 RecoveryWindowStart
    private float LastHitWindowEnd()
    {
        if (HasHitPulses())
        {
            HitPulse[] pulses = config.hitPulses;
            float last = config.RecoveryWindowStart;
            for (int i = 0; i < pulses.Length; i++)
            {
                if (pulses[i] != null && pulses[i].end > last) last = pulses[i].end;
            }
            return last;
        }
        return config.RecoveryWindowStart;
    }

    // 动画是否已真正播完（当前状态就是本招且不在过渡中、进度到 1）
    private bool IsAnimFinished(string animName)
    {
        Animator animator = body.Animator;
        if (animator.IsInTransition(0)) return false;
        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        return current.shortNameHash == Animator.StringToHash(animName)
            && current.normalizedTime >= 1f;
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
        bool canCancel = animTime < config.HitStartTime ||
                         animTime >= config.RecoveryWindowStart;

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
            if (CombatManager.Instance != null &&
                CombatManager.Instance.TryExecuteAvailableFinisher(body))
            {
                return true;
            }

            // 崩解窗口把攻击键留给处决，不能接 NextCombo。
            if (CombatManager.Instance != null &&
                body == CombatManager.Instance.PlayerRef &&
                CombatManager.Instance.BossRef != null &&
                CombatManager.Instance.BossRef.IsPostureBroken)
            {
                return false;
            }

            if (config.NextCombo != null &&
                animTime >= config.RecoveryWindowStart &&
                animTime <= config.ComboWindowEnd)
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
            if (animTime >= config.RecoveryWindowStart &&
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

        if (valid && HasHitPulses())
            valid = ValidateHitPulses();

        if (!valid)
        {
            Debug.LogError(
                $"{config.name}（Anim={config.AnimName}）的攻击窗口非法：" +
                $"HitStart={config.HitStartTime}, Recover={config.RecoveryWindowStart}, " +
                $"ComboEnd={config.ComboWindowEnd}, Duration={config.StateDuration}, " +
                $"RotateEnd={config.RotationWindowEnd}。" +
                "必须满足 0 <= HitStartTime <= RecoveryWindowStart <= ComboWindowEnd <= StateDuration，" +
                "且 RotationWindowEnd 位于动作时长内。多段判定的 start<end 且落在时长内。");
        }
        return valid;
    }

    private bool HasHitPulses()
    {
        return config.hitPulses != null && config.hitPulses.Length > 0;
    }

    private bool IsInHitPulse()
    {
        HitPulse[] pulses = config.hitPulses;
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (!AttackWindowSync.PulseIsMelee(p)) continue;
            if (animTime >= p.start && animTime < p.end)
                return true;
        }
        return false;
    }

    private bool ValidateHitPulses()
    {
        HitPulse[] pulses = config.hitPulses;
        for (int i = 0; i < pulses.Length; i++)
        {
            HitPulse p = pulses[i];
            if (p == null) return false;
            if (p.start < 0f || p.end > config.StateDuration || p.start >= p.end)
                return false;
        }
        return true;
    }

    // 有 hitPulses：按段脉冲开关，每段 Enable 会清 hitTargets，所以每刀只打一次。
    // 无 hitPulses：沿用 HitStartTime → RecoveryWindowStart 一对开关。
    // NoHit / 0.01s 假红条：CanMeleeHit 为假，全程不开刀。
    private void ApplyHitbox()
    {
        bool wantOn = false;
        if (AttackWindowSync.CanMeleeHit(config.HitStartTime, config.RecoveryWindowStart, config.hitPulses))
        {
            wantOn = HasHitPulses()
                ? IsInHitPulse()
                : (animTime >= config.HitStartTime && animTime < config.RecoveryWindowStart);
        }

        if (wantOn && !weaponHitEnabled)
        {
            body.EnableWeaponHit(config);
            weaponHitEnabled = true;
        }
        else if (!wantOn && weaponHitEnabled)
        {
            body.DisableWeaponHit();
            weaponHitEnabled = false;
        }
    }

    private void ApplySfx()
    {
        AttackSfxCue[] cues = config.sfxCues;
        if (cues == null || cues.Length == 0) return;
        if (sfxFired == null || sfxFired.Length != cues.Length)
            sfxFired = new bool[cues.Length];

        for (int i = 0; i < cues.Length; i++)
        {
            if (sfxFired[i]) continue;
            AttackSfxCue cue = cues[i];
            if (cue == null || cue.clip == null) continue;
            if (animTime < cue.time) continue;
            sfxFired[i] = true;
            CombatEventBus.TriggerAttackSfx(cue.clip, body.transform.position);
        }
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
