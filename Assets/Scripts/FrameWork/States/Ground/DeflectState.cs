using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
using ARPG.Mgr;
using ARPG.Player;
namespace ARPG.FrameWork.States.Ground
{

    // 防御状态（M4）：短按/长按都是格挡。
    //   待机按下 → Deflect_Begin（抬刀）→ 静止 Deflect_Guard / 移动 Deflect_Walk|Deflect_Strafe
    //   走着按下 → 跳过抬刀，直接进移动格挡（对齐当前步伐，避免根运动被掐断抽搐）
    //   格挡中再按 → Deflect_Repeat（抖刀），开一次新的弹反窗口；没有 Repeat 状态则回退抬刀
    //   窗口内挡住 → Deflect_Slash；窗口外挡住 → 普通格挡受击
    //   长按松手 → Deflect_Cancel（收刀）；短按松手窗口保留到到期，直接回待机
    //
    // 主动弹反只由一个入口判定：DeflectAttemptTracker 里"未结束的尝试 + 固定半开窗口"。
    // 抬刀阶段（inBegin）只管表现，不再放行弹反资格，动画播放速度/长度也就无法延长窗口。
    // 一次真实按下 = 一次尝试；弹中则清罚，到期/被替换/被垫步取消/被打断才记空按。
    // 系统回防（崩解恢复自动回防、UI 选择）pressId = 0，只恢复普通格挡：不开窗口、不计空按。
    //
    // M7 被动防御：只狼模式中 Boss 的格挡不是"AI 按按钮"，而是玩家命中瞬间的
    // 防御判定。DeflectEntryMode.PassiveGuard（普通格挡，留下防御姿态）与
    // DeflectEntryMode.PerfectParry（强制完美弹反，弹开攻击者）由 CharacterBody
    // 的 TryPassiveDeflect 路由进入，不参与玩家主动尝试记账，处理完锁定中的命中后保持姿态片刻退出。
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
        private readonly int pressId;   // 本次防御对应的物理按下身份；0 = 系统回防
        private int attemptId;          // 主动弹反尝试；0 = 没有主动窗口（系统回防 / 被动防御）
        private float enterTime;
        private bool releasedDuringWindow; // 窗口内已经松手：短点按，到期直接回待机，不走收刀
        private const float PassiveHoldDuration = 0.25f; // 普通格挡姿态保持时长（玩家连打会被连续重进场）
        private const float PerfectHoldMax = 1.1f;    // 完美弹反兜底：弹反挥刀动画缺失/异常时强制收刀
        // 弹反收刀阈值：动画主体基本播完才收刀——弹反的"弹开表现"必须完整呈现，
        // 直接砍掉动画立刻反手会显得 Boss"莫名其妙就打人"，且玩家失去格挡窗口。
        private const float PerfectExitNormalized = 0.9f;

        // 完美弹反后收到的反击攻击配置：BT_Kengeki 在弹反瞬间抽好，先存下，
        // 反击的发起时刻按"目标命中时刻 - 该招 HitStartTime"倒推（counterFireTime），
        // 恢复基准由被弹方 ParriedState 的动画进度与最小硬直共同预测。
        // 前摇过长或首段无命中的多段招不能保证按目标时刻命中。
        private AttackConfig pendingCounter;
        public bool HasPendingCounter => pendingCounter != null;
        private float counterFireTime;
        private CharacterBody parriedOpponent;
        private float parriedRecoveryTime;
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
        private float rotationSpeed = 720f;

        public DeflectState(CharacterBody body, HierarchicalState parent, bool remash = false,
            DeflectEntryMode mode = DeflectEntryMode.Normal, HitData? pendingHit = null,
            int pressId = 0) : base(body)
        {
            this.parent = parent;
            this.remash = remash;
            this.mode = mode;
            this.pendingHit = pendingHit;
            this.pressId = pressId;
            if (body.Config != null) rotationSpeed = body.Config.RotationSpeed;
        }

        public override void OnEnter()
        {
            // 受击取消进格挡时父节点还是 null，进场时顶层已经是 GroundedState。
            if (parent == null)
                parent = body.MainStateMachine.CurrentState as HierarchicalState;

            enterTime = Time.time;
            releasedDuringWindow = false;
            attemptId = 0;
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
            pendingCounter = null;
            counterFireTime = 0f;
            parriedOpponent = null;
            parriedRecoveryTime = 0f;

            body.IsGuarding = true;

            // M7 被动防御入口：不参与玩家主动尝试记账、无抬刀动画，直接处理锁定中的命中，
            // 处理完保持防御姿态，由 OnUpdate 的被动退出逻辑按时收刀。
            if (mode != DeflectEntryMode.Normal)
            {
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

            // 主动防御入口：只有真实物理按下才开启尝试。窗口从"身体实际接受并开启防御"的
            // Time.time 起算，预输入时间不作为起点；系统回防只恢复普通格挡。
            if (pressId > 0)
            {
                attemptId = body.BeginDeflectAttempt(pressId);

                // 命令延迟消费、而该 PressId 已经松开：按短点按处理（窗口照开并标记已释放），
                // 不能凭"没按住"就丢掉这一次有效点按。
                if (attemptId != 0 && body.IsDeflectPressReleased(pressId))
                    releasedDuringWindow = true;
            }

            if (body.EnableStateDebugLog)
            {
                Debug.Log(
                    $"[CombatTiming][DeflectEnter] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                    $"pressId={pressId} attempt={attemptId} 窗口={body.CurrentDeflectWindow:F4}s " +
                    $"连续空按={body.DeflectEmptyCount} 入口={mode}", body);
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
                // 走着进格挡：融合本身就是抬刀阶段；按 RaiseBlend 到点结束。
                // 注意融合只吃表现，不参与弹反资格（资格只看 Tracker 的窗口）。
                inBegin = true;
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
            // 状态 Update 与命中入口都要先按当前时间推进尝试到期，
            // 否则两者的执行顺序会改变"窗口外命中"的结果（设计文档 §5）。
            body.AdvanceDeflectAttempt();

            if (canceling)
            {
                cancelTimer += Time.deltaTime;
                var cancelInfo = body.Animator.GetCurrentAnimatorStateInfo(0);
                if ((AnimUtil.IsPlaying(cancelInfo, "Deflect_Cancel") && cancelInfo.normalizedTime >= 0.95f)
                    || cancelTimer >= cancelDuration)
                {
                    if (body.EnableStateDebugLog)
                    {
                        Debug.Log(
                            $"[CombatTiming][DeflectCancelEnd] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                            $"收刀计时={cancelTimer:F4}s，收刀动画进度={cancelInfo.normalizedTime:F3}，" +
                            $"到达超时={cancelTimer >= cancelDuration}，随后进入Idle", body);
                    }
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
                    // 等待期间持续校准恢复时刻，不能被弹反动画结束条件提前收刀。
                    if (pendingCounter != null)
                    {
                        if (RefreshCounterFireTime() && Time.time >= counterFireTime)
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
                    || (raiseAnim == null && beginTimer >= RaiseBlend)
                    || beginTimer >= beginFailsafe)
                {
                    inBegin = false;
                    // 走路分支已在举刀循环中，到点不重播，保留当前步伐。
                    PlayGuardLoop(force: raiseAnim != null);
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
                    if (IsReleased())
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
                if (guardFlinchTimer <= 0f && !IsReleased() && !inBegin)
                {
                    PlayGuardLoop(force: true);
                }
            }
            // 走路抬刀没有独立动画，融合期间仍需响应移动方向与举刀走路切换。
            else if ((!inBegin || raiseAnim == null) && !IsReleased())
            {
                PlayGuardLoop(force: false);
                RotateIfMoving();
            }

            // 窗口内松手：短点按。窗口不因松开而取消，等到期再决定收刀还是继续格挡
            // （本次明确的操作设计调整：旧实现松开立刻 StartCancel，把点按的剩余弹反窗口砍掉了）。
            if (IsParryWindowActive() && IsReleased()) releasedDuringWindow = true;

            // 主动窗口到期后：松开则退出防御，仍按住则继续普通格挡（长按不会自动续窗）。
            // 箭 Heavy 格挡/弹反动画未结束时不能因松手提前回 Idle。
            if (!IsParryWindowActive() && IsReleased()
                && !((arrowHeavyGuard || arrowHeavyDeflect) && waitingGuardHurt))
            {
                if (releasedDuringWindow)
                {
                    // 短点按：抬刀都还没走完就松了，直接回待机，不播收刀。
                    parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                }
                else
                {
                    // 长按 / 系统回防松手：走现有收刀流程。
                    StartCancel();
                }
            }
        }

        // 本次防御是否已松开。物理按下按 PressId 查释放记录（延迟处理旧释放不会关掉新按下）；
        // 系统回防没有按键身份，退回看实时按住状态。
        private bool IsReleased()
        {
            if (pressId > 0) return body.IsDeflectPressReleased(pressId);
            return !body.IsDeflectHeld;
        }

        // 唯一的弹反资格：Tracker 里未结束的尝试 + 固定半开窗口。
        private bool IsParryWindowActive()
        {
            return attemptId != 0 && body.IsDeflectParryActive(attemptId);
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
            // 格挡全程可被再格挡（开一次新窗口）或垫步取消，含抬刀/举刀/弹刀成功/收刀。
            // 箭 Heavy 特殊格挡/弹反动画：默认必须播完；到达 ArrowHeavyDeflectDodgeOpenTime 后可提前格挡/垫步。
            if ((arrowHeavyGuard || arrowHeavyDeflect) && waitingGuardHurt)
            {
                if (cmd is DeflectCommand || cmd is DodgeCommand)
                {
                    if (CanArrowHeavyDeflectDodgeCancel())
                    {
                        FinishArrowHeavyLockEarly(cmd);
                        return true;
                    }
                    // 锁定期间未实际开启窗口：不消耗、不记空按，留缓冲等开放时刻
                    return false;
                }

                return true;
            }

            if (cmd is DeflectCommand deflectCmd)
            {
                // 系统回防：已经有主动窗口时不重建状态，避免把正在进行的一次尝试打断。
                if (!deflectCmd.IsPhysicalPress && IsParryWindowActive()) return true;

                // 再按防御：重新进状态开一次新窗口。Tracker 内部先结算旧尝试
                // （被替换 = 空按一次）再按当前空按次数算新窗口；同一个 PressId 的
                // 缓冲重试或状态重入不会重复开窗，也不会重复记账。
                parent.SubStateMachine.ChangeState(new DeflectState(body, parent, remash: true,
                    pressId: deflectCmd.PressId));
                return true;
            }

            if (cmd is DodgeCommand)
            {
                // 主动垫步离开防御：未成功的尝试提前结束，空按一次。
                body.EndDeflectAttempt(attemptId, DeflectAttemptEndReason.LeftDefense);
                parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
                return true;
            }

            // M7 完美弹反后的反击命令：BT_Kengeki（KengekiArmed 已置位）在弹反瞬间抽好反击招下发。
            // 不立即出招——按"目标命中时刻 - 该招 HitStartTime"倒推出发起时刻，
            // 可及时起手的单段近战以被弹方预计恢复 + ParryCounterHitDelay 为命中目标。
            // 长前摇、多段位移/弓段仍按原招式执行，不承诺统一命中时刻。
            // 只对被动完美弹反（Boss）生效；玩家主动弹反走 Normal 模式不受影响。
            if (mode == DeflectEntryMode.PerfectParry && cmd is AttackCommand)
            {
                if (body.ActiveAttack != null)
                {
                    pendingCounter = body.ActiveAttack;
                    RefreshCounterFireTime();
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

            return true;
        }

        public override bool OnHitReceived(HitData hit)
        {
            // 命中入口先推进到期：同帧的输入接受与命中按真实执行顺序处理，不回滚、不开事后窗口。
            body.AdvanceDeflectAttempt();
            if (canceling) return false;

            bool inParryWindow = IsParryWindowActive();

            // 危字：普通格挡等于没防。突刺/抓取只有弹反窗口内能弹开。横扫已当普通刀，走下面格挡/弹反。
            if (hit.isPerilous)
            {
                if (inParryWindow) return HandlePerfectParry(hit);
                return false;
            }

            if (inParryWindow) return HandlePerfectParry(hit);

            // 窗口外（或本次防御没有主动窗口）：普通格挡。
            // 普通格挡不给已经结束的尝试追加失败，也不清罚。
            return HandleGuardHit(hit);
        }

        // 完美弹反：弹开攻击者 + 打铁表现。玩家弹反 Boss 且 Boss 架势崩时给玩家处决确认窗口。
        private bool HandlePerfectParry(HitData hit)
        {
            // 主动尝试的成功记账必须早于任何状态切换（飞舟末刀互弹、架势崩解转忍杀准备、
            // Boss 交锋反击），否则清罚会丢在切换里。被动入口没有主动尝试（attemptId = 0），
            // 这里天然不会误记到玩家账上。
            if (mode == DeflectEntryMode.Normal)
                body.RecordDeflectSuccess(attemptId);

            if (body.EnableStateDebugLog || (hit.attacker != null && hit.attacker.EnableStateDebugLog))
            {
                Debug.Log(
                    $"[CombatTiming][PerfectParry] t={Time.time:F4}s frame={Time.frameCount} " +
                    $"弹反方={body.name}，攻击方={(hit.attacker != null ? hit.attacker.name : "<none>")}，" +
                    $"箭矢={hit.isProjectile}，入口={mode}",
                    body);
            }

            if (!hit.isProjectile && hit.attacker != null
                && HitReactionUtil.IsPlayer(body)
                && HitReactionUtil.IsBoatFinalPulseParry(hit.attacker))
            {
                return HandleBoatMutualDeflect(hit);
            }

            bool brokeAttackerPosture = false;
            // 箭不是刀刃相撞：弹开只挡伤害，不涨攻击者架势、不把 Boss 弹进硬直。
            if (hit.attacker != null && !hit.isProjectile)
            {
                float gain = body.Config != null ? body.Config.DeflectPostureGain : 30f;
                brokeAttackerPosture = hit.attacker.AccumulatePosture(
                    gain,
                    allowBreak: true,
                    source: PostureBreakSource.Deflect);
                // 被弹断：统一给双方走 ParriedState 硬直，被弹方短暂停顿 → 弹反方拿先手。
                // 两道例外：
                //   1. 打不断的招（危字 / 飞舟 / JumpThrust）维持原样，招继续，保留连续弹反。
                //   2. 多段连招只在末段被弹才断，中间段弹中招继续 —— 否则弹第一下就掐断，
                //      后面几刀砍不出来，连续弹反就没了。
                // armKengeki: false 必须显式传：默认值会给被弹方挂 KengekiArmed，
                // 变成"玩家一弹反 Boss 立刻反击"，刚拿到的先手权当场又没了。
                if (!brokeAttackerPosture && !hit.attacker.AttackUninterruptible
                    && AttackStateBase.IsLastHit(hit.attacker))
                {
                    hit.attacker.ForceParryStun(armKengeki: false);
                    if (mode == DeflectEntryMode.Normal && HitReactionUtil.IsPlayer(body)
                        && !HitReactionUtil.IsPlayer(hit.attacker))
                        hit.attacker.ArmPlayerParryAttackWindow(body.LightAttack);
                }
            }

            // 弹反成功方获得优先反击权（回合制）：
            // 只有 Boss 弹反玩家才武装交锋；玩家弹反 Boss 不该给玩家挂 KengekiArmed。
            if (!hit.isProjectile && !HitReactionUtil.IsPlayer(body))
            {
                // Boss 已弹回玩家，攻防轮次已改变，不沿用上次被玩家弹断的保护窗口。
                body.ClearPlayerParryAttackWindow();
                body.KengekiArmed = true;
            }
            // 玩家近战弹开 Boss：累计连续弹开次数，供主动层 JumpThrust（≠ 交锋 3062）。
            if (!hit.isProjectile && HitReactionUtil.IsPlayer(body) && hit.attacker != null)
                hit.attacker.NotifyPerfectlyParried();
            // 先保存配置下限作缺失动画的兜底；安排反击时再读取真实硬直状态的预测。
            parriedOpponent = hit.attacker;
            float minimumDuration = hit.attacker != null && hit.attacker.Config != null
                ? hit.attacker.Config.ParriedDuration
                : (body.Config != null ? body.Config.ParriedDuration : 1.0f);
            parriedRecoveryTime = Time.time + minimumDuration;

            CombatEventBus.TriggerWeaponDeflected(
                CombatFxPoint.ForDeflect(hit.attacker, body, hit.hitPoint, hit.isProjectile),
                DeflectType.Perfect);
            CombatEventBus.TriggerCameraShake(0.3f);
            TimeScaleController.HitStop();
            body.MarkCombatTime();

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

        // 飞舟最后一刀被完美弹反：Boat（Boat2 末刀）/ Boat_Full（整段末刀）→ 双方播 Deflected_Boat。
        private bool HandleBoatMutualDeflect(HitData hit)
        {
            CharacterBody attacker = hit.attacker;
            if (attacker != null && !hit.isProjectile)
            {
                float gain = body.Config != null ? body.Config.DeflectPostureGain : 30f;
                attacker.AccumulatePosture(gain, allowBreak: true, source: PostureBreakSource.Deflect);
                attacker.ForceParryStun("Deflected_Boat");
            }

            CombatEventBus.TriggerWeaponDeflected(
                CombatFxPoint.ForDeflect(attacker, body, hit.hitPoint, hit.isProjectile),
                DeflectType.Perfect);
            CombatEventBus.TriggerCameraShake(0.3f);
            TimeScaleController.HitStop();
            body.MarkCombatTime();

            if (HitReactionUtil.IsPlayer(body) && attacker != null && !hit.isProjectile)
                attacker.NotifyPerfectlyParried();

            // 玩家也必须走顶层 ForceParryStun：只改子状态会留在原 GroundedState 里，
            // 葫芦/离地切空中都会把 Deflected_Boat 掐掉（Boss UseAirState=false 没有这个问题）。
            body.ForceParryStun("Deflected_Boat", armKengeki: false);
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
                CombatFxPoint.ForDeflect(hit.attacker, body, hit.hitPoint, hit.isProjectile), DeflectType.Normal);
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
                CombatFxPoint.ForDeflect(hit.attacker, body, hit.hitPoint, hit.isProjectile), DeflectType.Normal);
            // 箭 Heavy 格挡：架势顶不住会往后滑，镜头跟随下压后拉
            CombatEventBus.TriggerHeavyArrowDefended(body, perfect: false);
            return true;
        }

        private bool RefreshCounterFireTime()
        {
            if (parriedOpponent != null && parriedOpponent.IsParried)
            {
                if (!parriedOpponent.TryGetParriedRecoveryTime(out float recoveryTime))
                    return false;
                parriedRecoveryTime = recoveryTime;
            }
            float hitDelay = body.Config != null ? body.Config.ParryCounterHitDelay : 0.1f;
            counterFireTime = parriedRecoveryTime + hitDelay - pendingCounter.HitStartTime;
            return true;
        }

        private void StartCancel()
        {
            if (canceling) return;

            if (body.EnableStateDebugLog)
            {
                Debug.Log(
                    $"[CombatTiming][DeflectCancelStart] t={Time.time:F4}s frame={Time.frameCount} {body.name} " +
                    $"状态={body.CurrentStatePath}，入口={mode}，收刀超时={cancelDuration:F3}s", body);
            }

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

        // 箭 Heavy 动画锁定的提前结束。cmd 是触发取消的那条命令：它带着本次按下的身份，
        // 必须原样传给下一个防御状态，否则这一下只会恢复普通格挡，白丢弹反窗口。
        void FinishArrowHeavyLockEarly(ICommand cmd)
        {
            waitingGuardHurt = false;
            arrowHeavyGuard = false;
            arrowHeavyDeflect = false;
            guardHurtAnim = null;
            hasSeenGuardHurt = false;
            inBegin = false;
            currentLoopAnim = null;
            guardFlinchTimer = 0f;

            if (cmd is DeflectCommand deflectCmd)
            {
                bool released = deflectCmd.PressId > 0
                    ? body.IsDeflectPressReleased(deflectCmd.PressId)
                    : !body.IsDeflectHeld;

                if (released)
                {
                    // 已经松手：未成功的尝试提前结束（空按一次），直接回待机，不凭空举刀。
                    body.EndDeflectAttempt(attemptId, DeflectAttemptEndReason.LeftDefense);
                    parent.SubStateMachine.ChangeState(new IdleState(body, parent));
                }
                else
                {
                    // 旧尝试交给 Tracker：新按下开启时按"被替换"结算，重复 PressId 不会重复记账。
                    parent.SubStateMachine.ChangeState(new DeflectState(body, parent, remash: true,
                        pressId: deflectCmd.PressId));
                }
            }
            else
            {
                // 垫步取消：未成功的尝试提前结束，空按一次。
                body.EndDeflectAttempt(attemptId, DeflectAttemptEndReason.LeftDefense);
                parent.SubStateMachine.ChangeState(new DodgeState(body, parent));
            }
        }
    }

}
