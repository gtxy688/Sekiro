using UnityEngine;

using ARPG.Boss;
using ARPG.Combat;
using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.Mgr;
using ARPG.Player;
namespace ARPG.FrameWork.States.Base
{

    // 共用地面与空中攻击的动画时钟、命中脉冲、音效、出箭和转向逻辑。
    // 子类决定结束后的去向、连招状态和可接受的取消命令。
    public abstract class AttackStateBase : BaseState
    {
        protected readonly HierarchicalState parent;
        protected readonly AttackConfig config; // 本次攻击配置；Boss 可由招式表烘焙生成
        // 本招动画内已经播放的秒数；与 Time.time 的全局时刻不同，用它对照攻击配置的各窗口。
        protected float animTime;

        private bool weaponHitEnabled;
        private int activePulseIndex = -1;
        // 保存本招基础伤害，避免上一刀的脉冲覆盖值成为下一刀“默认伤害”。
        private int fallbackDamage;
        private float fallbackPosture;
        private float fallbackKnockback;
        private HitGrade fallbackGrade;
        private bool[] sfxFired;
        private bool[] arrowFired;
        private BossMoveEntry moveEntry;
        private BossMoveWindow moveWindow;

        protected AttackStateBase(CharacterBody body, HierarchicalState parent, AttackConfig config) : base(body)
        {
            this.parent = parent;
            this.config = config;
        }

        // 空中刀落地立刻离空（仅 AirAttackState 覆盖为 true；地面恒 false）
        public virtual bool WantsImmediateLand => false;

        // 子类钩子：进入时最先执行（空中记进入时刻）
        protected virtual void OnAttackEnter() { }

        // 本招正常结束/窗口校验失败时退到哪个状态
        protected abstract void ExitToIdle();

        // NextCombo 接下一段时派生同款状态（地面/空中各自 new）
        protected abstract BaseState NewSelf(AttackConfig next);

        public override void OnEnter()
        {
            OnAttackEnter();

            animTime = 0f;
            sfxFired = null;
            arrowFired = null;
            body.IsAttackRecoveryOpen = false;
            fallbackDamage = config.BaseDamage;
            fallbackPosture = config.PostureDamage;
            fallbackKnockback = config.Knockback;
            fallbackGrade = config.HitGrade;
            moveEntry = body.CurrentMoveEntry;
            moveWindow = body.CurrentMoveWindow;
            activePulseIndex = -1;

            // ActiveAttack 是 Brain 对“下一次攻击”的一次性选择，状态取得后立即清空。
            if (body.ActiveAttack == config)
            {
                body.ActiveAttack = null;
            }

            if (!ValidateWindows())
            {
                ExitToIdle();
                return;
            }

            // 统一请求攻击动画，JumpThrust 使用直接播放，其余招式使用融合。
            bool attackAnimStarted = AnimUtil.TryBeginAttackAnim(body.Animator, config, body.CurrentMoveEntry);
            if (!attackAnimStarted)
            {
                Debug.LogError($"{body.name} 的 Animator 缺少攻击状态：{config.AnimName}");
            }


            // 允许转向的招：起手先对准目标，整招丢掉 Clip 根旋转，否则挥砍 Root yaw 会盖掉代码朝向。
            // 识破冻结也在这里解开：下一刀才允许再转向玩家。
            body.ClearCombatYawFrozen();
            body.SetSuppressRootYaw(config.AllowRotation);
            if (config.AllowRotation)
            {
                SnapTowardCombatTarget();
                UpdateAttackSteer();
            }

            // 判定只跟随 hitPulses 的有效近战时间段；HitStartTime 不直接开启 Hitbox。
            weaponHitEnabled = false;
            ApplyHitbox();

            body.AttackUninterruptible = IsUninterruptibleAttack(config, body.CurrentMoveEntry);

            // 危字攻击：发事件 → UI 弹"危"字提示
            if (config.Perilous != PerilousType.None)
            {
                CombatEventBus.TriggerPerilousAttack(config.Perilous);
            }
        }

        public override void OnUpdate()
        {
            // 空中刀落地由 AirState.LeaveAir 切回地面；落地帧不再推进本刀（地面恒 false）
            if (WantsImmediateLand) return;

            animTime = AttackAnimClock.ReadSeconds(body.Animator, config.AnimName);
            // 先取本招的动画时钟，再同步命中、音效、出箭；动画变速时这些节点一起跟随。
            body.IsAttackRecoveryOpen = animTime >= config.RecoveryWindowStart;

            ApplyHitbox();
            ApplySfx();
            ApplyArrows();

            if (config.AllowRotation)
            {
                UpdateAttackSteer();
            }

            // 位移不在此处理：突进/前移完全由攻击动画的 Root 曲线驱动（全权根运动），
            // 代码只负责状态时长与连招窗口判定

            // 按配置时长或动画结束退出；无连招动作播完且判定关闭后可提前结束，避免停在末帧。
            bool noMelee = !AttackWindowSync.CanMeleeHit(config.hitPulses);
            bool animFinished = IsAnimFinished(config.AnimName);
            bool animPlayedOut = config.NextCombo == null
                && animTime >= LastHitWindowEnd()
                && animFinished;
            if (config.WaitAnimEnd)
            {
                if (animFinished)
                    ExitToIdle();
            }
            else if (animTime >= config.StateDuration || animPlayedOut || (noMelee && animFinished))
            {
                ExitToIdle();
            }
        }

        public override void OnExit()
        {
            // 正常结束和被打断都会经过这里，必须关掉判定，避免离开攻击后仍能打中别人。
            body.DisableWeaponHit();
            body.ActiveHitPulseIndex = -1;
            body.IsAttackRecoveryOpen = false;
            body.AttackUninterruptible = false;
            body.SetSuppressRootYaw(false);
        }

        public override bool HandleCommand(ICommand cmd)
        {
            // 前摇可以主动取消；命中段锁定；进入后摇后重新开放所有非攻击行为。
            // 此处只计算能否取消；具体能取消到什么动作，由地面/空中派生类实现。
            bool canCancel = animTime < config.HitStartTime ||
                             animTime >= config.RecoveryWindowStart;

            if (cmd is DeflectCommand deflectCmd) return CancelToDeflect(canCancel, deflectCmd);
            if (cmd is DodgeCommand) return CancelToDodge(canCancel);
            if (cmd is FinisherCommand) return HandleFinisherCommand();
            if (cmd is AttackCommand) return HandleAttackCommand();
            if (cmd is JumpCommand) return HandleJumpCommand(canCancel);
            if (cmd is MoveCommand moveCmd) return HandleMoveCommand(moveCmd);
            return false;
        }

        // ===== 命令钩子（子类按需覆盖）=====

        // 地面：可切入格挡态；空中：不可格挡取消。
        // cmd 带本次按下的身份，子类必须原样透传给防御状态入口。
        protected virtual bool CancelToDeflect(bool canCancel, DeflectCommand cmd) { return false; }

        // 地面：可切入垫步态；空中：不可垫步取消
        protected virtual bool CancelToDodge(bool canCancel) { return false; }

        // 空中：Jump2 连段；地面：无（跳跃由父状态拦）
        protected virtual bool HandleJumpCommand(bool canCancel) { return false; }

        // 空中：只记录移动方向；地面：后摇段直接进入四向循环 MoveState
        protected virtual bool HandleMoveCommand(MoveCommand moveCmd)
        {
            body.MoveDirection = moveCmd.Direction;
            return true;
        }

        private bool HandleAttackCommand()
        {
            // 已出现忍杀机会时，破防前遗留的普通攻击预输入不能接 NextCombo。
            // 真正的忍杀只接受 PlayerBrain 在机会出现后生成的 FinisherCommand。
            if (body.Faction == Faction.Player &&
                CombatManager.Instance.HasAvailableFinisher(body))
            {
                return true;
            }

            if (config.NextCombo != null &&
                animTime >= config.RecoveryWindowStart &&
                animTime <= config.ComboWindowEnd)
            {
                // 新建下一段攻击状态，从头初始化其动画时钟和判定；不是把当前动画继续多播一刀。
                parent.SubStateMachine.ChangeState(NewSelf(config.NextCombo));
                return true;
            }
            return false;
        }

        private bool HandleFinisherCommand()
        {
            if (body.Faction == Faction.Player)
                CombatManager.Instance.TryExecuteAvailableFinisher(body);
            return true;
        }

        // ===== 共享判定/表现逻辑 =====

        // 危字整段、飞舟、JumpThrust / Jump_Danger 全段抓前摇打不断。
        private static bool IsUninterruptibleAttack(AttackConfig cfg, BossMoveEntry entry)
        {
            if (cfg.Perilous != PerilousType.None)
                return true;
            if (entry != null && (entry.id == "Boat" || entry.id == "Boat_Full"
                || entry.id == "JumpThrust" || entry.id == "Jump_Danger"))
                return true;
            if (!string.IsNullOrEmpty(cfg.AnimName)
                && cfg.AnimName.StartsWith("Boat"))
                return true;
            return false;
        }

        // 取有效近战脉冲总数与最长动画序列段数的较大值，兼容共用窗口和单动画多刀。
        // 零长度等占位脉冲不计数，避免把转向或位移段当成额外一刀。
        public static int HitChanceCount(CharacterBody body)
        {
            if (body == null) return 1;

            int pulses = 0;
            BossMoveEntry entry = body.CurrentMoveEntry;
            if (entry != null && entry.windows != null)
            {
                for (int i = 0; i < entry.windows.Length; i++)
                {
                    HitPulse[] wp = entry.windows[i] != null ? entry.windows[i].hitPulses : null;
                    if (wp == null) continue;
                    for (int j = 0; j < wp.Length; j++)
                    {
                        if (AttackWindowSync.PulseIsMelee(wp[j])) pulses++;
                    }
                }
            }

            // 窗口表在招式表上是单份、按段号 Clamp 取用，所以段数要看动画序列本身。
            int states = 0;
            if (entry != null && entry.sequences != null)
            {
                for (int i = 0; i < entry.sequences.Length; i++)
                {
                    string[] st = entry.sequences[i] != null ? entry.sequences[i].states : null;
                    if (st != null && st.Length > states) states = st.Length;
                }
            }

            int total = pulses > states ? pulses : states;
            return total > 1 ? total : 1;
        }

        // 被弹的这一下是不是本招最后一下。单发招恒为 true。
        // 多段连招在中间段被弹不算，招继续 —— 才能连续弹反；打到末段被弹才打断。
        public static bool IsLastHit(CharacterBody body)
        {
            int total = HitChanceCount(body);
            if (total <= 1) return true;
            return body.ActiveHitPulseIndex >= total - 1;
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

        // 校验窗口顺序及脉冲范围，非法配置由进入逻辑退回待机。
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

        private int CurrentPulseIndex()
        {
            // 同一攻击动画可以有多刀；遍历寻找覆盖当前动画时刻的有效近战窗口。
            HitPulse[] pulses = config.hitPulses;
            if (pulses == null) return -1;
            for (int i = 0; i < pulses.Length; i++)
            {
                HitPulse p = pulses[i];
                if (!AttackWindowSync.PulseIsMelee(p)) continue;
                if (animTime >= p.start && animTime < p.end)
                    return i;
            }
            return -1;
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

        // 只按有效 hitPulses 开关判定；脉冲变化也重开，以更新伤害并清除上一刀的命中记录。
        // HitStartTime 和 RecoveryWindowStart 用于取消与连招窗口，不直接开关 Hitbox。
        private void ApplyHitbox()
        {
            int pulseIndex = -1;
            bool wantOn = false;
            if (AttackWindowSync.CanMeleeHit(config.hitPulses))
            {
                pulseIndex = CurrentPulseIndex();
                wantOn = pulseIndex >= 0;
            }

            bool pulseChanged = wantOn && pulseIndex != activePulseIndex;
            if (wantOn && (!weaponHitEnabled || pulseChanged))
            {
                if (weaponHitEnabled)
                    body.DisableWeaponHit();
                ApplyPulseCombat(pulseIndex);
                body.EnableWeaponHit(config);
                weaponHitEnabled = true;
                activePulseIndex = pulseIndex;
            }
            else if (!wantOn && weaponHitEnabled)
            {
                body.DisableWeaponHit();
                weaponHitEnabled = false;
                activePulseIndex = -1;
            }

            body.ActiveHitPulseIndex = wantOn ? pulseIndex : -1;
        }

        private void ApplyPulseCombat(int pulseIndex)
        {
            HitPulse pulse = null;
            if (HasHitPulses() && pulseIndex >= 0 && pulseIndex < config.hitPulses.Length)
                pulse = config.hitPulses[pulseIndex];
            AttackCombatResolve.ApplyPulse(
                config, pulse, fallbackDamage, fallbackPosture, fallbackKnockback, fallbackGrade);
        }

        // cue 是时间轴上的一次事件。每个 cue 单独记 fired，越过触发时刻后只播放一次音效。
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

        // 按出箭 cue 逐次发射，并保留进招时的招式表上下文供伤害解析。
        private void ApplyArrows()
        {
            ArrowSpawnCue[] cues = config.arrowCues;
            if (cues == null || cues.Length == 0) return;
            if (arrowFired == null || arrowFired.Length != cues.Length)
                arrowFired = new bool[cues.Length];

            // BT ResetMove 会清表行；出箭仍用进招时记下的行。
            if (body.CurrentMoveEntry == null && moveEntry != null)
            {
                body.CurrentMoveEntry = moveEntry;
                body.CurrentMoveWindow = moveWindow;
            }

            for (int i = 0; i < cues.Length; i++)
            {
                if (arrowFired[i]) continue;
                ArrowSpawnCue cue = cues[i];
                if (cue == null) continue;
                if (animTime < cue.time) continue;
                arrowFired[i] = true;
                body.SpawnArrow(i);
            }
        }

        private void UpdateAttackSteer()
        {
            if (!ShouldSteerTowardTarget())
            {
                body.ClearSteerYaw();
                return;
            }

            Vector3 direction = ResolveAttackSteerDir();
            direction.y = 0f;
            body.SetSteerYaw(direction, config.RotationSpeed);
        }

        // 出箭或较长的无近战段持续朝向目标，避免常规转向窗结束后瞄准停止。
        private bool ShouldSteerTowardTarget()
        {
            if (HasArrowCues()) return true;
            bool noMelee = !AttackWindowSync.CanMeleeHit(config.hitPulses);
            if (noMelee && config.StateDuration > 0.8f) return true;
            // 多段刀持续转向到后摇开放，避免后续脉冲过早失去目标。
            if (HasHitPulses() && config.hitPulses.Length > 1)
                return animTime <= config.RecoveryWindowStart;
            return animTime <= config.RotationWindowEnd;
        }

        private bool HasArrowCues()
        {
            return config.arrowCues != null && config.arrowCues.Length > 0;
        }

        private void SnapTowardCombatTarget()
        {
            Transform target = ResolveCombatTarget();
            if (target == null) return;
            Vector3 direction = target.position - body.transform.position;
            direction.y = 0f;
            body.SnapYaw(direction);
        }

        private Vector3 ResolveAttackSteerDir()
        {
            Transform target = ResolveCombatTarget();
            if (target != null)
            {
                return target.position - body.transform.position;
            }

            if (body.MoveUsesWorldDir)
            {
                Vector2 input = body.MoveDirection;
                return new Vector3(input.x, 0f, input.y);
            }

            return body.InputToWorldDir(body.MoveDirection);
        }

        protected bool HasCombatTarget()
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
}
