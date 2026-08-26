using UnityEngine;

public class AirIdleState : BaseState
{
    private const float MinAirTime = 0.08f;
    private const float TakeoffFailsafe = 0.25f;

    private enum Phase
    {
        Takeoff,  // Jump：起跳
        Airborne, // Jumping：空中持续
        Landing   // Fall：落地
    }

    private Phase phase;
    private float enterTime;
    private float landingStartTime;

    // AirState 落地后要等 Fall 播完才回地面
    public bool CanLeaveAir { get; private set; }

    public AirIdleState(CharacterBody body, HierarchicalState parent) : base(body)
    {
    }

    public override void OnEnter()
    {
        enterTime = Time.time;
        CanLeaveAir = false;

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

        if (phase != Phase.Landing)
        {
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
        // 空中攻击/格挡已移除。落地动画期间仍在 AirState，指令留给缓冲，落地后再执行。
        return false;
    }

    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous && hit.perilousType == PerilousType.Sweep)
        {
            if (hit.attacker != null)
            {
                float gain = body.Config != null ? body.Config.MikiriPostureGain : 30f;
                hit.attacker.AccumulatePosture(gain);
                hit.attacker.ForceParryStun();
            }
            CombatEventBus.TriggerWeaponDeflected(
                CombatFxPoint.BetweenWeapons(hit.attacker, body, hit.hitPoint), DeflectType.Perfect);
            CombatManager.Instance?.HitStop();
            return true;
        }
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
