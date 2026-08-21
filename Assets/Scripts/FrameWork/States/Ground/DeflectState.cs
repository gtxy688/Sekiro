using UnityEngine;

// 防御状态（M4）：短按/长按都是格挡。
//   待机按下 → Deflect_Begin（抬刀）→ 静止 Deflect_Guard / 移动 Deflect_Walk|Deflect_Strafe
//   走着按下 → 跳过抬刀，直接进移动格挡（对齐当前步伐，避免根运动被掐断抽搐）
//   格挡中再按 → Deflect_Repeat（抖刀），刷新弹反窗口；没有 Repeat 状态则回退抬刀
//   窗口内挡住 → Deflect_Slash；窗口外挡住 → 普通格挡受击
//   长按松手 → Deflect_Cancel（收刀）；短按松手姿态不变，窗口结束回待机
public class DeflectState : BaseState
{
    private HierarchicalState parent;
    private readonly bool remash;
    private float enterTime;
    private float window;
    private bool hasReleased;
    private float guardFlinchTimer;
    private float beginTimer;
    private float beginFailsafe = 0.55f;
    private bool inBegin;
    private string raiseAnim;
    private const float RaiseBlend = 0.22f; // 走着进格挡：用固定时长融合做出抬刀，不播原地 Begin
    private float cancelTimer;
    private float cancelDuration = 0.3f;
    private bool canceling;
    private string currentLoopAnim;
    private float rotationSpeed = 720f;

    public DeflectState(CharacterBody body, HierarchicalState parent, bool remash = false) : base(body)
    {
        this.parent = parent;
        this.remash = remash;
        if (body.Config != null) rotationSpeed = body.Config.RotationSpeed;
    }

    public override void OnEnter()
    {
        enterTime = Time.time;
        hasReleased = false;
        guardFlinchTimer = 0f;
        beginTimer = 0f;
        cancelTimer = 0f;
        canceling = false;
        currentLoopAnim = null;

        body.RegisterDeflectPress();
        window = body.GetDeflectWindow();
        body.IsGuarding = true;

        // 格挡中再按：抖刀，不要重播抬刀。没有 Repeat Clip（Boss）则走原来的抬刀/走路融合
        if (remash && AnimUtil.HasState(body.Animator, "Deflect_Repeat"))
        {
            inBegin = true;
            raiseAnim = "Deflect_Repeat";
            body.Animator.CrossFadeInFixedTime("Deflect_Repeat", 0.05f);
        }
        else if (IsPlayingLocomotion())
        {
            // 走着进格挡：不播原地抬刀（会掐步伐），用较长融合把走路姿势接到举刀走
            inBegin = false;
            raiseAnim = null;
            UpdateStrafeParams(instant: true);
            PlayGuardLoop(force: true, matchCycle: true, blendSeconds: RaiseBlend);
        }
        else
        {
            inBegin = true;
            raiseAnim = "Deflect_Begin";
            body.Animator.CrossFadeInFixedTime("Deflect_Begin", 0.12f);
        }
    }

    public override void OnUpdate()
    {
        if (canceling)
        {
            cancelTimer += Time.deltaTime;
            var cancelInfo = body.Animator.GetCurrentAnimatorStateInfo(0);
            if ((AnimUtil.IsPlaying(cancelInfo, "Deflect_Cancel") && cancelInfo.normalizedTime >= 0.95f)
                || cancelTimer >= cancelDuration)
            {
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            }
            return;
        }

        if (inBegin)
        {
            beginTimer += Time.deltaTime;
            var beginInfo = body.Animator.GetCurrentAnimatorStateInfo(0);
            if ((raiseAnim != null && AnimUtil.IsPlaying(beginInfo, raiseAnim) && beginInfo.normalizedTime >= 0.92f)
                || beginTimer >= beginFailsafe)
            {
                inBegin = false;
                PlayGuardLoop(force: true);
            }
        }

        UpdateStrafeParams(instant: false);

        if (guardFlinchTimer > 0f)
        {
            guardFlinchTimer -= Time.deltaTime;
            if (guardFlinchTimer <= 0f && !hasReleased && !inBegin)
            {
                PlayGuardLoop(force: true);
            }
        }
        else if (!inBegin && !hasReleased)
        {
            PlayGuardLoop(force: false);
            RotateIfMoving();
        }

        if (hasReleased && Time.time - enterTime >= window)
        {
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

    public override void OnExit()
    {
        body.IsGuarding = false;
    }

    public override bool HandleCommand(ICommand cmd)
    {
        // 格挡全程可被再格挡（刷新窗口 + 抖刀计数）或垫步取消，含抬刀/举刀/弹刀成功/收刀
        if (cmd is DeflectCommand)
        {
            parent.SubStateMachine.ChangeState(new DeflectState(body, parent, remash: true));
            return true;
        }

        if (cmd is DodgeCommand)
        {
            parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
            return true;
        }

        if (canceling) return true;

        if (cmd is MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            return true;
        }

        if (cmd is IdleCommand)
        {
            StartCancel();
            return true;
        }

        return true;
    }

    public override bool OnHitReceived(HitData hit)
    {
        if (hit.isPerilous) return false;
        if (canceling) return false;

        float elapsed = Time.time - enterTime;

        if (elapsed <= window)
        {
            bool brokeAttackerPosture = false;
            if (hit.attacker != null)
            {
                float gain = body.Config != null ? body.Config.DeflectPostureGain : 30f;
                brokeAttackerPosture = hit.attacker.AccumulatePosture(
                    gain,
                    allowBreak: true,
                    source: PostureBreakSource.Deflect);
                if (!brokeAttackerPosture)
                {
                    hit.attacker.ForceParryStun();
                }
            }

            float self = hit.postureDmg * (body.Config != null ? body.Config.DeflectSelfPostureFactor : 0.3f);
            body.AccumulatePosture(self, allowBreak: false);

            CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Perfect);
            CombatEventBus.TriggerCameraShake(0.3f);
            CombatManager.Instance?.HitStop();

            if (brokeAttackerPosture)
            {
                parent.SubStateMachine.ChangeState(
                    new FinisherReadyState(body, parent, hit.attacker));
                return true;
            }

            inBegin = false;
            currentLoopAnim = null;
            string deflectAnim = hit.knockback > 0f
                ? "Deflect_HeavySlash"
                : "Deflect_Slash";
            if (!AnimUtil.HasState(body.Animator, deflectAnim))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少弹反状态：{deflectAnim}");
            }
            else
            {
                body.Animator.CrossFade(deflectAnim, 0.05f);
            }
            guardFlinchTimer = 0.25f;
            return true;
        }

        float posture = hit.postureDmg * (body.Config != null ? body.Config.GuardPostureFactor : 0.5f);
        body.AccumulatePosture(posture);

        inBegin = false;
        currentLoopAnim = null;
        HurtContext guardHurt = hit.knockback > 0f ? HurtContext.GuardHeavy : HurtContext.Guard;
        body.Animator.CrossFade(body.ResolveHurtAnim(guardHurt), 0.03f);
        guardFlinchTimer = 0.25f;

        CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Normal);
        return true;
    }

    private void StartCancel()
    {
        if (canceling) return;

        canceling = true;
        inBegin = false;
        cancelTimer = 0f;
        currentLoopAnim = null;
        body.Animator.CrossFade("Deflect_Cancel", 0.05f);
    }

    private void PlayGuardLoop(bool force, bool matchCycle = false, float blendSeconds = 0.08f)
    {
        bool moving = body.MoveDirection.sqrMagnitude > 0.01f;
        string want = moving
            ? (IsLockedOnTarget() ? "Deflect_Strafe" : "Deflect_Walk")
            : "Deflect_Guard";
        if (!force && want == currentLoopAnim) return;

        float startAt = 0f;
        if (matchCycle)
        {
            var info = body.Animator.GetCurrentAnimatorStateInfo(0);
            startAt = (info.normalizedTime % 1f) * info.length;
        }

        currentLoopAnim = want;
        body.Animator.CrossFadeInFixedTime(want, blendSeconds, 0, startAt);
    }

    private void RotateIfMoving()
    {
        Vector2 inputDir = body.MoveDirection;
        if (inputDir.sqrMagnitude < 0.01f) return;

        Vector3 moveDir;
        if (IsLockedOnTarget())
        {
            Vector3 toBoss = LockOnManager.Instance.Target.position - body.transform.position;
            toBoss.y = 0f;
            moveDir = toBoss.sqrMagnitude > 0.001f ? toBoss.normalized : body.transform.forward;
        }
        else
        {
            moveDir = body.InputToWorldDir(inputDir);
        }

        body.RotateYaw(moveDir, rotationSpeed);
    }

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

    private bool IsPlayingLocomotion()
    {
        var anim = body.Animator;
        if (IsLocomotion(anim.GetCurrentAnimatorStateInfo(0))) return true;
        return anim.IsInTransition(0) && IsLocomotion(anim.GetNextAnimatorStateInfo(0));
    }

    private static bool IsLocomotion(AnimatorStateInfo info)
    {
        return AnimUtil.IsPlaying(info, "Walk")
            || AnimUtil.IsPlaying(info, "Walk_Strafe")
            || AnimUtil.IsPlaying(info, "IdleToWalk")
            || AnimUtil.IsPlaying(info, "IdleToStrafe")
            || AnimUtil.IsPlaying(info, "DodgeToWalk");
    }

    private static bool IsLockedOnTarget()
    {
        return LockOnManager.Instance != null && LockOnManager.Instance.IsLockedOn
            && LockOnManager.Instance.Target != null;
    }
}
