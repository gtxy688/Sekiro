using UnityEngine;

public class AirIdleState : BaseState
{
    private const float MinAirTime = 0.08f;
    private const float TakeoffFailsafe = 0.25f;

    private enum Phase
    {
        Takeoff,  // Jump：起跳
        Airborne, // Jumping：空中持续
        Jump2,    // 空中二段（踩头 / 无踩只播片）
        Landing   // Fall：落地
    }

    private readonly HierarchicalState parent;
    private readonly bool startInJump2;
    private readonly bool resumeAirborne;

    private Phase phase;
    private float enterTime;
    private float landingStartTime;

    // AirState 落地后要等 Fall 播完才回地面
    public bool CanLeaveAir { get; private set; }

    public AirIdleState(CharacterBody body, HierarchicalState parent, bool startInJump2 = false, bool resumeAirborne = false) : base(body)
    {
        this.parent = parent;
        this.startInJump2 = startInJump2;
        this.resumeAirborne = resumeAirborne;
    }

    public override void OnEnter()
    {
        // Jump2 已由 TryAirJump2 CrossFade；再走起跳会盖掉 Jump2 播 Jump
        if (startInJump2)
        {
            enterTime = Time.time;
            CanLeaveAir = false;
            phase = Phase.Jump2;
            return;
        }

        enterTime = Time.time;
        CanLeaveAir = false;

        if (resumeAirborne)
        {
            // 空中刀结束仍滞空：上升/下落都播 Jumping，不要重播地面起跳 Jump。
            // Fall 仍由 TryStartLanding 在近地时切。
            Play("Jumping", JumpBlend);
            phase = Phase.Airborne;
            return;
        }

        // 踩空：没有起跳，直接空中持续
        if (body.Rb.velocity.y < 0f)
        {
            Play("Jumping", JumpBlend);
            phase = Phase.Airborne;
            return;
        }

        Play("Jump", JumpBlend);
        phase = Phase.Takeoff;
    }

    public override void OnUpdate()
    {
        if (phase == Phase.Takeoff)
        {
            TryEnterAirborne();
        }

        if (phase == Phase.Jump2)
        {
            body.TryApplySweepStomp();
            TryFinishJump2();
        }

        if (phase != Phase.Landing)
        {
            // Jump2 刚切上来时脚可能还贴地，不能按 Fall 落地逻辑掐掉 Jump2
            if (phase != Phase.Jump2)
                TryStartLanding();
        }
        else
        {
            TryFinishLanding();
        }

        if (phase == Phase.Landing) return;

        Vector2 inputDir = body.MoveDirection;
        if (inputDir.sqrMagnitude > 0.01f && body.Config != null)
        {
            Vector3 lookDirection = body.InputToWorldDir(inputDir);
            body.RotateYaw(lookDirection, body.Config.RotationSpeed);
        }
    }

    public override bool HandleCommand(ICommand cmd)
    {
        // Fall 期间仍在 AirState，指令留给缓冲，落地后再执行
        if (phase == Phase.Landing) return false;

        if (cmd is JumpCommand)
        {
            body.TryAirJump2(out bool played);
            if (played)
            {
                // 同态切 Jump2 不会走 OnEnter；不刷新 enterTime 的话
                // TakeoffFailsafe / 过顶点会立刻把 Jump2 切成 Jumping
                enterTime = Time.time;
                phase = Phase.Jump2;
            }
            return true;
        }

        if (cmd is AttackCommand)
        {
            AttackConfig air = body.AirAttack;
            if (air == null || string.IsNullOrEmpty(air.AnimName))
            {
                Debug.LogError($"{body.name} 未配置 AirAttack，空中平 A 无效。");
                return true;
            }
            if (!AnimUtil.HasState(body.Animator, air.AnimName))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少空中攻击状态：{air.AnimName}");
                return true;
            }
            // 空中平 A 走 AirAttack 槽；清掉 Brain 长按可能写入的突刺 ActiveAttack
            body.ActiveAttack = null;
            parent.SubStateMachine.ChangeState(new AirAttackState(body, parent, air));
            return true;
        }

        if (cmd is MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            return true;
        }

        return false;
    }

    public override bool OnHitReceived(HitData hit)
    {
        // 空中挨扫不再自动涨 Boss 架势 / ForceParryStun；裸受击交给上层 ReceiveHit
        return false;
    }

    private float JumpToJumpingNorm
    {
        get
        {
            float v = body.Config != null ? body.Config.JumpToJumpingNormalized : 0.55f;
            return Mathf.Clamp(v, 0.1f, 1f);
        }
    }

    private bool SwitchAtApex => body.Config == null || body.Config.SwitchJumpingAtApex;

    private float LandEarlyHeight => body.Config != null ? Mathf.Max(0f, body.Config.LandEarlyHeight) : 0.35f;

    private float JumpBlend => body.Config != null ? Mathf.Max(0f, body.Config.JumpAnimBlend) : 0.08f;

    private float LandBlend => body.Config != null ? Mathf.Max(0f, body.Config.LandAnimBlend) : 0.05f;

    private void TryEnterAirborne()
    {
        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        bool jumpFinished = AnimUtil.IsPlaying(info, "Jump")
            && info.normalizedTime >= JumpToJumpingNorm
            && !body.Animator.IsInTransition(0);
        bool atApex = SwitchAtApex
            && body.Rb.velocity.y < 0f
            && Time.time - enterTime >= MinAirTime;
        // Jump 已经切走，或起跳播太久：进空中持续
        bool jumpGone = !AnimUtil.IsPlaying(info, "Jump")
            && Time.time - enterTime >= TakeoffFailsafe;

        if (!jumpFinished && !atApex && !jumpGone) return;

        Play("Jumping", JumpBlend);
        phase = Phase.Airborne;
    }

    // 对齐 TryEnterAirborne：Jump2 播到阈值 / 过顶点 / 状态已切走超过兜底，再进 Jumping
    private void TryFinishJump2()
    {
        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        bool jump2Done = AnimUtil.IsPlaying(info, "Jump2")
            && info.normalizedTime >= JumpToJumpingNorm
            && !body.Animator.IsInTransition(0);
        bool atApex = SwitchAtApex
            && body.Rb.velocity.y < 0f
            && Time.time - enterTime >= MinAirTime;
        bool gone = !AnimUtil.IsPlaying(info, "Jump2")
            && Time.time - enterTime >= TakeoffFailsafe;

        if (!jump2Done && !atApex && !gone) return;

        Play("Jumping", JumpBlend);
        phase = Phase.Airborne;
    }

    private void TryStartLanding()
    {
        bool rising = body.Rb.velocity.y > 0.1f;
        if (Time.time - enterTime < MinAirTime || rising) return;
        if (!body.IsGrounded && !IsNearGround()) return;

        // 崩解中跳走：落地直接回倒地，不播 Fall
        if (body.IsPostureBroken)
        {
            CanLeaveAir = true;
            return;
        }

        landingStartTime = Time.time;
        Play("Fall", LandBlend);
        phase = Phase.Landing;
    }

    private bool IsNearGround()
    {
        float early = LandEarlyHeight;
        if (early <= 0.01f) return false;

        Transform origin = body.groundCheckPoint != null ? body.groundCheckPoint : body.transform;
        float radius = body.groundCheckRadius > 0f ? body.groundCheckRadius : 0.2f;
        return Physics.SphereCast(
            origin.position + Vector3.up * 0.05f,
            radius,
            Vector3.down,
            out _,
            early,
            body.groundLayer,
            QueryTriggerInteraction.Ignore);
    }

    private void TryFinishLanding()
    {
        AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
        bool fallDone = AnimUtil.IsPlaying(info, "Fall")
            && info.normalizedTime >= 0.95f
            && !body.Animator.IsInTransition(0);
        bool timeout = Time.time - landingStartTime >= Mathf.Max(0.4f, info.length + 0.1f);

        if (fallDone || timeout)
        {
            CanLeaveAir = true;
        }
    }

    private void Play(string stateName, float blend)
    {
        AnimUtil.TryCrossFade(body.Animator, stateName, blend);
    }
}
