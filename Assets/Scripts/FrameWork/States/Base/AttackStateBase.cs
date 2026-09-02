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

    // 攻击状态基类：地面 AttackState 与空中 AirAttackState 共用同一套
    // 出招/动画时钟/多段脉冲判定/音效/出箭/转向/霸体/连招窗口逻辑，
    // 子类只保留四处差异：
    //   1. 退出目标（地面回 Idle / 空中回 AirIdle 续滞空）
    //   2. NextCombo 派生出的下一段是哪个子类
    //   3. 专属命令：地面可格挡/垫步取消、攻击接移动；空中可 Jump2 连段
    //   4. 空中落地立刻离空（WantsImmediateLand），地面恒 false
    public abstract class AttackStateBase : BaseState
    {
        protected readonly HierarchicalState parent;
        protected readonly AttackConfig config; // 当前动作的全部数值与窗口均来自 SO
        protected float animTime;

        private bool weaponHitEnabled;
        private int activePulseIndex = -1;
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

        // 本招正常结束/空配置/校验失败时退到哪个状态
        protected abstract void ExitToIdle();

        // NextCombo 接下一段时派生同款状态（地面/空中各自 new）
        protected abstract BaseState NewSelf(AttackConfig next);

        public override void OnEnter()
        {
            OnAttackEnter();

            // 空配置保护：没有 AttackConfig 的攻击状态无意义，立刻退回待机
            if (config == null)
            {
                ExitToIdle();
                return;
            }

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

            // 招式在 _Attack/_Bow/_Clash/_Danger 子状态机里，必须走 Parent.State。
            if (!AnimUtil.TryBeginAttackAnim(body.Animator, config, body.CurrentMoveEntry))
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

            // 前摇不开判定：贴身时刀还在蓄力就会扫到。到 HitStartTime / 第一段 pulse 再开。
            weaponHitEnabled = false;
            ApplyHitbox();

            // Boss AI 反制判定标记（M7 用，避免查状态类型）
            body.IsAttacking = true;
            body.AttackUninterruptible = IsUninterruptibleAttack(config, body.CurrentMoveEntry);

            // 危字攻击：发事件 → UI 弹"危"字提示（M17）
            if (config.Perilous != PerilousType.None)
            {
                CombatEventBus.TriggerPerilousAttack(config.Perilous);
            }
        }

        public override void OnUpdate()
        {
            // 空中刀落地由 AirState.LeaveAir 切回地面；落地帧不再推进本刀（地面恒 false）
            if (WantsImmediateLand) return;

            if (config == null) return;

            animTime = AttackAnimClock.ReadSeconds(body.Animator, config.AnimName);
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

            // 动作彻底结束：stateDuration 到点，或（无连招的招）动画实际播完且判定段已全部关闭。
            // 后一条专治"stateDuration 大于实际 clip 长度"的末帧冻结罚站——射箭/收弓段最容易踩：
            // 动画播完停在最后一帧，animTime 却永远达不到 stateDuration，角色僵在原地等时长。
            bool noMelee = !AttackWindowSync.CanMeleeHit(
                config.HitStartTime, config.RecoveryWindowStart, config.hitPulses);
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
            body.DisableWeaponHit();
            body.ActiveHitPulseIndex = -1;
            body.IsAttacking = false;
            body.IsAttackRecoveryOpen = false;
            body.AttackUninterruptible = false;
            body.SetSuppressRootYaw(false);
        }

        public override bool HandleCommand(ICommand cmd)
        {
            if (config == null) return false;

            // 前摇可以主动取消；命中段锁定；进入后摇后重新开放所有非攻击行为。
            bool canCancel = animTime < config.HitStartTime ||
                             animTime >= config.RecoveryWindowStart;

            if (cmd is DeflectCommand) return CancelToDeflect(canCancel);
            if (cmd is DodgeCommand) return CancelToDodge(canCancel);
            if (cmd is AttackCommand) return HandleAttackCommand();
            if (cmd is JumpCommand) return HandleJumpCommand(canCancel);
            if (cmd is MoveCommand moveCmd) return HandleMoveCommand(moveCmd);
            return false;
        }

        // ===== 命令钩子（子类按需覆盖）=====

        // 地面：可切入格挡态；空中：不可格挡取消
        protected virtual bool CancelToDeflect(bool canCancel) { return false; }

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
            if (CombatManager.Instance != null &&
                CombatManager.Instance.TryExecuteAvailableFinisher(body))
            {
                return true;
            }

            // 崩解窗口把攻击键留给处决，不能接 NextCombo。
            // 「是不是玩家」查阵营；「当前对手崩没崩」仍是找对象，多 Boss 时要改为查任一对手。
            if (body.Faction == Faction.Player &&
                CombatManager.Instance != null &&
                CombatManager.Instance.BossRef != null &&
                CombatManager.Instance.BossRef.IsPostureBroken)
            {
                return false;
            }

            if (config.NextCombo != null &&
                animTime >= config.RecoveryWindowStart &&
                animTime <= config.ComboWindowEnd)
            {
                parent.SubStateMachine.ChangeState(NewSelf(config.NextCombo));
                return true;
            }
            return false;
        }

        // ===== 共享判定/表现逻辑 =====

        // 危字整段、飞舟、JumpThrust / Jump_Danger 全段抓前摇打不断。
        private static bool IsUninterruptibleAttack(AttackConfig cfg, BossMoveEntry entry)
        {
            if (cfg != null && cfg.Perilous != PerilousType.None)
                return true;
            if (entry != null && (entry.id == "Boat" || entry.id == "Boat_Full"
                || entry.id == "JumpThrust" || entry.id == "Jump_Danger"))
                return true;
            if (cfg != null && !string.IsNullOrEmpty(cfg.AnimName)
                && cfg.AnimName.StartsWith("Boat"))
                return true;
            return false;
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

        // 有 hitPulses：按段脉冲开关，每段 Enable 会清 hitTargets，所以每刀只打一次。
        // 相邻两刀无空隙时 pulseIndex 变化也要重开，才能换伤害。
        // 无 hitPulses：沿用 HitStartTime → RecoveryWindowStart 一对开关。
        // NoHit / 0.01s 假红条：CanMeleeHit 为假，全程不开刀。
        private void ApplyHitbox()
        {
            int pulseIndex = -1;
            bool wantOn = false;
            if (AttackWindowSync.CanMeleeHit(config.HitStartTime, config.RecoveryWindowStart, config.hitPulses))
            {
                if (HasHitPulses())
                {
                    pulseIndex = CurrentPulseIndex();
                    wantOn = pulseIndex >= 0;
                }
                else
                {
                    wantOn = animTime >= config.HitStartTime && animTime < config.RecoveryWindowStart;
                }
            }

            bool pulseChanged = HasHitPulses() && wantOn && pulseIndex != activePulseIndex;
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

        // 弓段 NoHit 的 rotateEnd 只有 0.15~0.35s，拉弓瞄准会停转、身体朝向和出箭对不上。
        // 有出箭点、或长无近战窗（拉弓）时整段跟着玩家转。
        private bool ShouldSteerTowardTarget()
        {
            if (HasArrowCues()) return true;
            bool noMelee = !AttackWindowSync.CanMeleeHit(
                config.HitStartTime, config.RecoveryWindowStart, config.hitPulses);
            if (noMelee && config.StateDuration > 0.8f) return true;
            // 多段连刀（飞舟等）在最后一刀结束前都对准玩家。Hits() 默认 rotateEnd=0.35，
            // 飞舟第一刀约 1.85s，转向早停就会打空气。
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
