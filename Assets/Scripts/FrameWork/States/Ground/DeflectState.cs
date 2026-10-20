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

    // 区分主动防御与 Boss 受击时直接触发的被动防御；被动入口不参与主动弹反记账。
    public enum DeflectEntryMode
    {
        Normal,       // 主动防御，真实按下可开启弹反窗口
        PassiveGuard, // 被动防御：本次命中按窗口外处理（普通格挡 Block + 架势涨）
        PerfectParry  // 被动防御升级：强制完美弹反（弹开攻击者，抢回主动权）
    }

    // 处理举刀、格挡受击、弹反与收刀，并安排 Boss 弹反后的反击。
    // 主动弹反资格统一查 Tracker，抬刀动画只影响表现，系统回防不开主动窗口。
    public class DeflectState : BaseState
    {
        private HierarchicalState parent;
        private readonly bool remash;
        private readonly DeflectEntryMode mode;
        // ? 允许没有命中数据；Boss 被动防御时携带刚收到的那次命中，在 OnEnter 当场处理。
        private readonly HitData? pendingHit;
        private readonly int pressId;   // 本次防御对应的物理按下身份；0 = 系统回防
        private int attemptId;          // 主动弹反尝试；0 = 没有主动窗口（系统回防 / 被动防御）
        private float enterTime;
        private bool releasedDuringWindow; // 窗口内已经松手：短点按，到期直接回待机，不走收刀
        private const float PassiveHoldDuration = 0.25f; // 普通格挡姿态保持时长（玩家连打会被连续重进场）
        private const float PerfectHoldMax = 1.1f;    // 完美弹反兜底：弹反挥刀动画缺失/异常时强制收刀
        // 无反击命令时等弹反动画主体播完再收刀。
        private const float PerfectExitNormalized = 0.9f;

        // 缓存 BT_Kengeki 选择的反击，用对手预计恢复时刻与本招前摇倒推起手时间。
        private AttackConfig pendingCounter;
        public bool HasPendingCounter => pendingCounter != null;
        private float counterFireTime;
        private CharacterBody parriedOpponent;
        private float parriedRecoveryTime;
        // 以下计时与标志控制抬刀、受击停顿、收刀等动画阶段；主动弹反资格独立由 Tracker 判定。
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
        private readonly float rotationSpeed;

        public DeflectState(CharacterBody body, HierarchicalState parent, bool remash = false,
            DeflectEntryMode mode = DeflectEntryMode.Normal, HitData? pendingHit = null,
            int pressId = 0) : base(body)
        {
            this.parent = parent;
            this.remash = remash;
            this.mode = mode;
            this.pendingHit = pendingHit;
            this.pressId = pressId;
            rotationSpeed = body.Config.RotationSpeed;
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

            // 供架势回复与 AI 查询防御姿态；主动弹反资格另查 attemptId 对应的时间窗口。
            body.IsGuarding = true;

            // 被动入口直接结算传入的命中，再短暂保持防御姿态，不开主动尝试。
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

            // 真实按下从身体接受防御时开启窗口，预输入等待不计入窗口；系统回防只恢复格挡。
            if (pressId > 0)
            {
                attemptId = body.BeginDeflectAttempt(pressId);

                // 延迟消费时已松开的按下仍算有效点按，保留它的完整窗口。
                if (attemptId != 0 && body.IsDeflectPressReleased(pressId))
                    releasedDuringWindow = true;
            }


            // 再按优先播抖刀，缺少 Repeat 动画时回退到抬刀或移动融合。
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
            // Update 与命中入口都先推进尝试到期，避免调用先后改变窗口外命中的结果。
            body.AdvanceDeflectAttempt();

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

            // 被动防御短暂持刀；完美弹反若已有反击配置，则等待预计起手时刻。
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

            // 短点按松手仍保留弹反窗口，到期后直接回待机。
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
            // 本状态中的 true 也可能只吞掉当前不支持的输入；只有明确切状态/写入意图的分支才执行动作。
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

                // 再按时由 Tracker 先结算旧尝试再开新窗，同一 PressId 的重试不会重复记账。
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

            // Boss 被动弹反后缓存反击招，用对手恢复时间倒推起手；长前摇或多段招不保证统一命中时刻。
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
            // CharacterBody.ReceiveHit 先问当前状态；这里处理成功后返回 true，阻止重复走普通扣血受击。
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
            // 主动尝试先记成功再切状态，避免特殊反制或崩解切换遗漏清罚。
            if (mode == DeflectEntryMode.Normal)
                body.RecordDeflectSuccess(attemptId);


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
                float gain = body.Config.DeflectPostureGain;
                brokeAttackerPosture = hit.attacker.AccumulatePosture(
                    gain,
                    allowBreak: true,
                    source: PostureBreakSource.Deflect);
                // 非霸体招只在末刀被弹时打断，保留中间段的连续弹反。
                // 被弹方不武装交锋，避免它刚失去先手就立即反击。
                if (!brokeAttackerPosture && !hit.attacker.AttackUninterruptible
                    && AttackStateBase.IsLastHit(hit.attacker))
                {
                    hit.attacker.ForceParryStun(armKengeki: false);
                    // 预留玩家标准首刀的反击时间，计划存到被弹的 Boss 上；此处不会替玩家自动出刀。
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
            float minimumDuration = hit.attacker != null
                ? hit.attacker.Config.ParriedDuration
                : body.Config.ParriedDuration;
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

            // 主动弹反打崩对手且存在确认动画时进入忍杀准备，具体发起资格由准备状态检查。
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
                float gain = body.Config.DeflectPostureGain;
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

            float posture = hit.postureDmg * body.Config.GuardPostureFactor;
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
            float posture = hit.postureDmg * body.Config.GuardPostureFactor;
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

        // 持续修正对手恢复预测，再减去本招前摇，得到反击起手时刻。
        private bool RefreshCounterFireTime()
        {
            if (parriedOpponent != null && parriedOpponent.IsParried)
            {
                if (!parriedOpponent.TryGetParriedRecoveryTime(out float recoveryTime))
                    return false;
                parriedRecoveryTime = recoveryTime;
            }
            float hitDelay = body.Config.ParryCounterHitDelay;
            // 希望首个命中段落在“对手恢复 + 延迟”，减去本招前摇就得到起手的绝对时刻。
            // 对手硬直时长可能随动画预测更新，因此等待期间每帧重新计算。
            counterFireTime = parriedRecoveryTime + hitDelay - pendingCounter.HitStartTime;
            return true;
        }

        private void StartCancel()
        {
            if (canceling) return;


            canceling = true;
            inBegin = false;
            cancelTimer = 0f;
            currentLoopAnim = null;
            AnimUtil.TryCrossFade(body.Animator, "Deflect_Cancel", 0.05f);
        }

        // 按移动与锁定选择举刀循环，进入移动格挡时可沿用当前步伐进度。
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
                // normalizedTime 的小数部分是本轮进度；乘动画长度转成固定时间接口需要的秒数。
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
            // 将世界移动方向投影到“面向目标”的左右/前后轴，供锁定移动混合树选择步伐。
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

        // 提前结束重箭动作锁；防御命令的 PressId 必须继续传递，才能开启新的主动窗口。
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
