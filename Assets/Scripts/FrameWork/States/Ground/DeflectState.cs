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
    private const float PerfectHoldMax = 1.1f;    // 完美弹反兜底：弹反挥刀动画缺失/异常时强制收刀
    // 弹反收刀阈值：动画主体基本播完才收刀——弹反的"弹开表现"必须完整呈现，
    // 直接砍掉动画立刻反手会显得 Boss"莫名其妙就打人"，且玩家失去格挡窗口。
    private const float PerfectExitNormalized = 0.9f;

    // 完美弹反后收到的反击攻击配置：BT_Kengeki 在弹反瞬间抽好，先存下，
    // 反击的发起时刻按"目标命中时刻 - 该招 HitStartTime"倒推（counterFireTime），
    // 保证不管抽到哪招，命中都落在"被弹方硬直结束 + ParryCounterHitDelay"附近：
    // 玩家恢复瞬间的刀（前摇 ~0.15s）永远晚于反击命中 → 贪刀必被罚；
    // 弹反动画正常播放作为表现，到点直接切入反击攻击（弹开表现已完整）。
    private AttackConfig pendingCounter;
    private float counterFireTime;
    private float parriedDurationOnHit; // 弹反瞬间记录的被弹方硬直（为准就算 hit.attacker.Config）
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
    private bool arrowHeavyGuard; // 箭 Heavy 普通格挡：播完 Stagger_Broken 才能回举刀
    private bool arrowHeavyDeflect; // 箭 Heavy 完美弹反：播完 Deflect_HeavyArrow 才能回举刀
    private float arrowHeavyLockTimer;
    private bool raiseSpamGuard; // 连续举刀放刀：窗口内也只算普通格挡
    private bool guardStable;    // 已进入举刀循环，抖刀 remash 不算 spam
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
        // 受击取消进格挡时父节点还是 null，进场时顶层已经是 GroundedState。
        if (parent == null)
            parent = body.MainStateMachine.CurrentState as HierarchicalState;

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
        arrowHeavyGuard = false;
        arrowHeavyDeflect = false;
        arrowHeavyLockTimer = 0f;
        raiseSpamGuard = false;
        guardStable = remash;
        pendingCounter = null;
        counterFireTime = 0f;
        parriedDurationOnHit = 0f;

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

        float mashWindow = body.Config != null ? body.Config.DeflectMashWindow : 0.5f;
        if (!remash && !IsPlayingLocomotion()
            && Time.time - body.LastDeflectCancelTime < mashWindow)
        {
            raiseSpamGuard = true;
        }

        // 格挡中再按：抖刀，不要重播抬刀。没有 Repeat Clip（Boss）则走原来的抬刀/走路融合
        if (remash && AnimUtil.HasState(body.Animator, "Deflect_Repeat"))
        {
            inBegin = true;
            raiseAnim = "Deflect_Repeat";
            AnimUtil.TryCrossFadeInFixedTime(body.Animator, "Deflect_Repeat", 0.05f);
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
            AnimUtil.TryCrossFadeInFixedTime(body.Animator, "Deflect_Begin", 0.12f);
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
                // 弹反动画正常播放作为表现（弹开火花完整呈现）；反击发起时刻由
                // counterFireTime 决定（命中对齐被弹方硬直结束 + 补偿，与动画时长无关）。
                // 动画播完但没到发起时刻 → 保持防御姿态稍作停顿，等点发起；
                // 到点直接切入反击攻击（此时弹反动画关键表现已播完，剪尾无碍观感）。
                if (pendingCounter != null && Time.time >= counterFireTime)
                {
                    parent.SubStateMachine.ChangeState(
                        new AttackState(body, parent, pendingCounter));
                    return;
                }

                // 无反击命令（BT 抽招失败等）：动画播完收刀回 Idle，交给 BT 常规路径
                var info = body.Animator.GetCurrentAnimatorStateInfo(0);
                bool deflectAnimDone = AnimUtil.IsPlaying(info, "Deflect_Slash")
                    || AnimUtil.IsPlaying(info, "Deflect_HeavySlash");
                if ((deflectAnimDone && info.normalizedTime >= PerfectExitNormalized)
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

        if ((arrowHeavyGuard || arrowHeavyDeflect) && waitingGuardHurt)
            arrowHeavyLockTimer += Time.deltaTime;

        if (waitingGuardHurt)
        {
            UpdateGuardHurt();
            if (!waitingGuardHurt && (arrowHeavyGuard || arrowHeavyDeflect))
            {
                arrowHeavyGuard = false;
                arrowHeavyDeflect = false;
                if (hasReleased)
                {
                    parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                    return;
                }
                PlayGuardLoop(force: true);
            }
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

        // 箭 Heavy 格挡/弹反动画未结束时不能因松手提前回 Idle。
        if (hasReleased && Time.time - enterTime >= window
            && !((arrowHeavyGuard || arrowHeavyDeflect) && waitingGuardHurt))
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
        // 箭 Heavy 特殊格挡/弹反动画：默认必须播完；到达 ArrowHeavyDeflectDodgeOpenTime 后可提前格挡/垫步。
        if ((arrowHeavyGuard || arrowHeavyDeflect) && waitingGuardHurt)
        {
            if (cmd is IdleCommand)
            {
                hasReleased = true;
                return true;
            }

            if (cmd is DeflectCommand || cmd is DodgeCommand)
            {
                if (CanArrowHeavyDeflectDodgeCancel())
                {
                    FinishArrowHeavyLockEarly(cmd is DeflectCommand);
                    return true;
                }
                return false;
            }

            return true;
        }

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

        // M7 完美弹反后的反击命令：BT_Kengeki（KengekiArmed 已置位）在弹反瞬间抽好反击招下发。
        // 不立即出招——按"目标命中时刻 - 该招 HitStartTime"倒推出发起时刻，
        // 让反击无论抽到哪招都在被弹方硬直结束 + ParryCounterHitDelay 附近命中。
        // 玩家恢复瞬间的刀（前摇 ~0.15s）永远晚于反击命中 → 贪刀必被罚。
        // 只对被动完美弹反（Boss）生效；玩家主动弹反走 Normal 模式不受影响。
        if (mode == DeflectEntryMode.PerfectParry && cmd is AttackCommand)
        {
            if (body.ActiveAttack != null)
            {
                float hitDelay = body.Config != null ? body.Config.ParryCounterHitDelay : 0.1f;
                float parryDur = parriedDurationOnHit > 0f ? parriedDurationOnHit
                    : (body.Config != null ? body.Config.ParriedDuration : 1.0f);
                // 目标命中 = 弹反开始 + 被弹方硬直 + 补偿；发起 = 命中 - 本招判定前摇
                counterFireTime = enterTime + parryDur + hitDelay - body.ActiveAttack.HitStartTime;
                pendingCounter = body.ActiveAttack;
                return true;
            }
            // 反击招未就绪（BT 抽招失败/距离超展开）：直接收刀回 Idle，交给 BT 常规路径
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
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
        // 危字：普通格挡等于没防。横扫连弹反窗口也不吃；其余危字只有弹反窗口内能弹开。
        if (hit.isPerilous)
        {
            if (hit.perilousType == PerilousType.Sweep || canceling)
                return false;
            float perilousElapsed = Time.time - enterTime;
            if (perilousElapsed <= window || inBegin)
                return HandlePerfectParry(hit);
            return false;
        }
        if (canceling) return false;

        float elapsed = Time.time - enterTime;
        bool inParryWindow = elapsed <= window || inBegin;
        bool multiHit = HitReactionUtil.IsMultiHitParryException(hit);

        if (inParryWindow && (!raiseSpamGuard || multiHit))
            return HandlePerfectParry(hit);

        return HandleGuardHit(hit);
    }

    // 完美弹反：弹开攻击者 + 打铁表现。玩家弹反 Boss 且 Boss 架势崩时给玩家处决确认窗口。
    private bool HandlePerfectParry(HitData hit)
    {
        bool brokeAttackerPosture = false;
        // 箭不是刀刃相撞：弹开只挡伤害，不涨攻击者架势、不把 Boss 弹进硬直。
        if (hit.attacker != null && !hit.isProjectile)
        {
            float gain = body.Config != null ? body.Config.DeflectPostureGain : 30f;
            brokeAttackerPosture = hit.attacker.AccumulatePosture(
                gain,
                allowBreak: true,
                source: PostureBreakSource.Deflect);
            // 玩家被弹开进 ParriedState。Boss 连段（飞舟等）被弹不打断，才能连续弹反。
            if (!brokeAttackerPosture && HitReactionUtil.IsPlayer(hit.attacker))
                hit.attacker.ForceParryStun();
        }

        // 弹反成功方获得优先反击权（回合制）：
        // 只有 Boss 弹反玩家才武装交锋；玩家弹反 Boss 不该给玩家挂 KengekiArmed。
        if (!hit.isProjectile && !HitReactionUtil.IsPlayer(body))
            body.KengekiArmed = true;
        // 玩家近战弹开 Boss：累计连续弹开次数，供主动层 JumpThrust（≠ 交锋 3062）。
        if (!hit.isProjectile && HitReactionUtil.IsPlayer(body) && hit.attacker != null)
            hit.attacker.NotifyPerfectlyParried();
        // 精确记录被弹方硬直：反击命中时刻基准是"被弹方恢复"，不是弹反方自己的配置
        parriedDurationOnHit = hit.attacker != null && hit.attacker.Config != null
            ? hit.attacker.Config.ParriedDuration
            : (body.Config != null ? body.Config.ParriedDuration : 1.0f);

        CombatEventBus.TriggerWeaponDeflected(
            CombatFxPoint.BetweenWeapons(hit.attacker, body, hit.hitPoint), DeflectType.Perfect);
        CombatEventBus.TriggerCameraShake(0.3f);
        CombatManager.Instance?.HitStop();

        // 重箭弹反：弹开箭矢威力太大，玩家会借力后滑，镜头跟随下压后拉（普通近战弹反不动）
        if (HitReactionUtil.IsPlayer(body) && HitReactionUtil.IsArrowHeavyGuard(hit))
            CombatEventBus.TriggerHeavyArrowDefended(body, perfect: true);

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
        string deflectAnim = HitReactionUtil.IsPlayer(body)
            ? HitReactionUtil.PerfectParryAnim(hit)
            : (hit.knockback > 0f ? "Deflect_HeavySlash" : "Deflect_Slash");
        if (!AnimUtil.HasState(body.Animator, deflectAnim))
        {
            Debug.LogError($"{body.name} 的 Animator 缺少弹反状态：{deflectAnim}");
        }
        else
        {
            AnimUtil.TryCrossFade(body.Animator, deflectAnim, 0.05f);
        }

        // 箭 Heavy 完美弹反必须播完 Deflect_HeavyArrow，不能走 0.25s 硬直后立刻举刀。
        if (HitReactionUtil.IsPlayer(body) && HitReactionUtil.IsArrowHeavyGuard(hit))
        {
            guardHurtAnim = deflectAnim;
            waitingGuardHurt = true;
            hasSeenGuardHurt = false;
            arrowHeavyDeflect = true;
            guardFlinchTimer = 0f;
            arrowHeavyLockTimer = 0f;
        }
        else
        {
            guardFlinchTimer = 0.25f;
        }
        return true;
    }

        // 窗口外挡住：普通格挡受击（GuardHurt 动画 + 架势上涨，格挡系数削弱架势伤害）
    private bool HandleGuardHit(HitData hit)
    {
        if (HitReactionUtil.IsPlayer(body))
        {
            if (HitReactionUtil.IsMeleeHeavyPierce(hit))
                return false;
            if (HitReactionUtil.IsArrowHeavyGuard(hit))
                return PlayArrowHeavyGuard(hit);
            // 普通格挡打断「连续弹开」：JumpThrust 只认连弹，不认举盾挨打。
            if (hit.attacker != null && !hit.isProjectile)
                hit.attacker.ResetConsecutiveTimesParried();
        }

        float posture = hit.postureDmg * (body.Config != null ? body.Config.GuardPostureFactor : 0.5f);
        if (body.AccumulatePosture(posture))
        {
            // 格挡把架势打满已经切崩解，不能再覆盖成 Hurt_Guard。
            return true;
        }

        inBegin = false;
        currentLoopAnim = null;
        HurtContext guardHurt;
        if (HitReactionUtil.IsPlayer(body))
            guardHurt = HurtContext.Guard;
        else
            guardHurt = hit.knockback > 0f ? HurtContext.GuardHeavy : HurtContext.Guard;
        guardHurtAnim = body.ResolveHurtAnim(guardHurt);
        waitingGuardHurt = true;
        hasSeenGuardHurt = false;
        arrowHeavyGuard = false;
        guardFlinchTimer = 0f;
        AnimUtil.TryCrossFade(body.Animator, guardHurtAnim, 0.03f);

        CombatEventBus.TriggerWeaponDeflected(
            CombatFxPoint.BetweenWeapons(hit.attacker, body, hit.hitPoint), DeflectType.Normal);
        return true;
    }

    // 箭 Heavy：共用崩解动画，但不是真崩架势。必须播完才能回举刀。
    private bool PlayArrowHeavyGuard(HitData hit)
    {
        float posture = hit.postureDmg * (body.Config != null ? body.Config.GuardPostureFactor : 0.5f);
        if (body.AccumulatePosture(posture))
            return true;

        inBegin = false;
        currentLoopAnim = null;
        guardHurtAnim = HitReactionUtil.BrokenAnim(body);
        waitingGuardHurt = true;
        hasSeenGuardHurt = false;
        arrowHeavyGuard = true;
        guardFlinchTimer = 0f;
        arrowHeavyLockTimer = 0f;
        AnimUtil.TryCrossFade(body.Animator, guardHurtAnim, 0.05f);
        CombatEventBus.TriggerWeaponDeflected(
            CombatFxPoint.BetweenWeapons(hit.attacker, body, hit.hitPoint), DeflectType.Normal);
        // 箭 Heavy 格挡：架势顶不住会往后滑，镜头跟随下压后拉
        CombatEventBus.TriggerHeavyArrowDefended(body, perfect: false);
        return true;
    }

    private void StartCancel()
    {
        if (canceling) return;

        body.NotifyDeflectCancel();
        canceling = true;
        inBegin = false;
        cancelTimer = 0f;
        currentLoopAnim = null;
        AnimUtil.TryCrossFade(body.Animator, "Deflect_Cancel", 0.05f);
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
        if (!inBegin)
        {
            guardStable = true;
            raiseSpamGuard = false;
        }
        AnimUtil.TryCrossFadeInFixedTime(body.Animator, want, blendSeconds, 0, startAt);
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

    bool CanArrowHeavyDeflectDodgeCancel()
    {
        float open = body.ArrowHeavyDeflectDodgeOpenTime;
        return open > 0f && arrowHeavyLockTimer >= open;
    }

    void FinishArrowHeavyLockEarly(bool toDeflect)
    {
        waitingGuardHurt = false;
        arrowHeavyGuard = false;
        arrowHeavyDeflect = false;
        guardHurtAnim = null;
        hasSeenGuardHurt = false;
        inBegin = false;
        currentLoopAnim = null;
        guardFlinchTimer = 0f;

        if (toDeflect)
        {
            if (hasReleased)
                parent.SubStateMachine.ChangeState(new IdleState(body, parent));
            else
                parent.SubStateMachine.ChangeState(new DeflectState(body, parent, remash: true));
        }
        else
        {
            parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
        }
    }
}
