using UnityEngine;

// 防御状态（M4）：短按/长按都是格挡。
//   待机按下 → Deflect_Begin（抬刀）→ 静止 Deflect_Guard / 移动 Deflect_Walk|Deflect_Strafe
//   走着按下 → 跳过抬刀，直接进移动格挡（对齐当前步伐，避免根运动被掐断抽搐）
//   格挡中再按 → Deflect_Repeat（抖刀），刷新弹反窗口；没有 Repeat 状态则回退抬刀
//   窗口内挡住 → Deflect_Slash；窗口外挡住 → 普通格挡受击
//   长按松手 → Deflect_Cancel（收刀）；短按松手姿态不变，窗口结束回待机
//
// M7 被动防御：只狼模式中 Boss 的格挡不是"AI 按按钮"，而是玩家命中瞬间的
// 防御判定。DeflectEntryMode.PassiveGuard（普通格挡，留下防御姿态）与
// DeflectEntryMode.PerfectParry（强制完美弹反，弹开攻击者）由 CharacterBody
// 的 TryPassiveDeflect 路由进入，不参与抖刀惩罚，处理完锁定中的命中后保持姿态片刻退出。
public enum DeflectEntryMode
{
    Normal,       // 玩家/BT 主动按防御（原 M4 流程）
    PassiveGuard, // 被动防御：本次命中按窗口外处理（普通格挡 Block + 架势涨）
    PerfectParry  // 被动防御升级：强制完美弹反（弹开攻击者，抢回主动权）
}

public class DeflectState : BaseState
{
    private HierarchicalState parent;
    private readonly bool remash;
    private readonly DeflectEntryMode mode;
    private readonly HitData? pendingHit;
    private float enterTime;
    private float window;
    private bool hasReleased;
    private const float PassiveHoldDuration = 0.25f; // 普通格挡姿态保持时长（玩家连打会被连续重进场）
    private const float PerfectHoldMax = 1.2f;    // 完美弹反兜底：弹反挥刀动画缺失/异常时强制收刀
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
    private string guardHurtAnim;
    private bool waitingGuardHurt;
    private bool hasSeenGuardHurt;
    private float rotationSpeed = 720f;

    public DeflectState(CharacterBody body, HierarchicalState parent, bool remash = false,
        DeflectEntryMode mode = DeflectEntryMode.Normal, HitData? pendingHit = null) : base(body)
    {
        this.parent = parent;
        this.remash = remash;
        this.mode = mode;
        this.pendingHit = pendingHit;
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
        guardHurtAnim = null;
        waitingGuardHurt = false;
        hasSeenGuardHurt = false;

        body.IsGuarding = true;

        // M7 被动防御入口：不参与抖刀惩罚、无抬刀动画，直接处理锁定中的命中，
        // 处理完保持防御姿态，由 OnUpdate 的被动退出逻辑按时收刀。
        if (mode != DeflectEntryMode.Normal)
        {
            window = 0f;
            if (pendingHit.HasValue)
            {
                if (mode == DeflectEntryMode.PerfectParry)
                    HandlePerfectParry(pendingHit.Value);
                else
                    HandleGuardHit(pendingHit.Value);
            }
            PlayGuardLoop(force: true);
            return;
        }

        body.RegisterDeflectPress();
        window = body.GetDeflectWindow();

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

        // M7 被动防御：处理完命中后保持防御姿态一段时间即收刀回待机，
        // 玩家连打时每次命中都会重新进场（GetDeflectState），姿态得以延续。
        if (mode != DeflectEntryMode.Normal)
        {
            // 普通格挡受击动画（Hurt_Guard）也要播完，再按持姿态时长收刀
            if (waitingGuardHurt) UpdateGuardHurt();

            if (mode == DeflectEntryMode.PerfectParry)
            {
                // 完美弹反后不滞留防御发呆：弹反挥刀动画播完立即收刀回 Idle，
                // 把反击窗口交给 AI（下帧 BT 立刻抽招），避免白白浪费弹反主动权。
                var info = body.Animator.GetCurrentAnimatorStateInfo(0);
                bool deflectAnimDone = AnimUtil.IsPlaying(info, "Deflect_Slash")
                    || AnimUtil.IsPlaying(info, "Deflect_HeavySlash");
                if ((deflectAnimDone && info.normalizedTime >= 0.95f)
                    || Time.time - enterTime >= PerfectHoldMax)
                {
                    parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                }
                return;
            }

            // PassiveGuard：普通格挡姿态保持 PassiveHoldDuration 后收刀
            if (Time.time - enterTime >= PassiveHoldDuration)
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

        if (waitingGuardHurt)
        {
            UpdateGuardHurt();
        }
        else if (guardFlinchTimer > 0f)
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

    // 格挡受击动画（Hurt_Guard/Hurt_GuardHeavy）播放进度跟踪：播完复位，等待下一击
    private void UpdateGuardHurt()
    {
        AnimatorStateInfo hurtInfo = body.Animator.GetCurrentAnimatorStateInfo(0);
        if (AnimUtil.IsPlaying(hurtInfo, guardHurtAnim))
        {
            hasSeenGuardHurt = true;
            if (hurtInfo.normalizedTime >= 0.95f)
                waitingGuardHurt = false;
        }
        else if (hasSeenGuardHurt && !body.Animator.IsInTransition(0))
        {
            waitingGuardHurt = false;
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
            return HandlePerfectParry(hit);
        }

        return HandleGuardHit(hit);
    }

    // 完美弹反：弹开攻击者 + 打铁表现。玩家弹反 Boss 且 Boss 架势崩时给玩家处决确认窗口。
    private bool HandlePerfectParry(HitData hit)
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

        CombatEventBus.TriggerWeaponDeflected(hit.hitPoint, DeflectType.Perfect);
        CombatEventBus.TriggerCameraShake(0.3f);
        CombatManager.Instance?.HitStop();

        // 弹反忍杀确认窗口只给玩家（DeflectToFinsher）。Boss 被动弹反玩家不进入处决准备；
        // Boss 打崩玩家后继续播弹反挥刀（Normal 且攻击者是玩家时 mode 也是 Normal，此处靠
        // FinisherReadyState 的准入 + 该分支共同约束）。
        if (brokeAttackerPosture && mode == DeflectEntryMode.Normal &&
            AnimUtil.HasState(body.Animator, "DeflectToFinsher"))
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

    // 窗口外挡住：普通格挡受击（GuardHurt 动画 + 架势上涨，格挡系数削弱架势伤害）
    private bool HandleGuardHit(HitData hit)
    {
        float posture = hit.postureDmg * (body.Config != null ? body.Config.GuardPostureFactor : 0.5f);
        if (body.AccumulatePosture(posture))
        {
            // 格挡把架势打满已经切崩解，不能再覆盖成 Hurt_Guard。
            return true;
        }

        inBegin = false;
        currentLoopAnim = null;
        HurtContext guardHurt = hit.knockback > 0f ? HurtContext.GuardHeavy : HurtContext.Guard;
        guardHurtAnim = body.ResolveHurtAnim(guardHurt);
        waitingGuardHurt = true;
        hasSeenGuardHurt = false;
        guardFlinchTimer = 0f;
        body.Animator.CrossFade(guardHurtAnim, 0.03f);

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
        body.SetMoveStrafe(x, z, instant);
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
