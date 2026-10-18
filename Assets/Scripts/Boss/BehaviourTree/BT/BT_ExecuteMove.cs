using UnityEngine;

using ARPG.Boss;
using ARPG.Configs;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States.Ground;
using ARPG.Mgr;
namespace ARPG.Boss.BehaviourTree
{

    // 按表行顺序播完各段动画。五连垫步结束后有概率改打重箭。
    public class BT_ExecuteMove : Node, ISelectorLock
    {
        private readonly CharacterBody body;
        private readonly BossMoveTable table;

        private BossMoveEntry entry;
        private BossAnimSequence sequence;
        private int segment;
        private bool started;
        private bool waitingAttack;
        private bool allowInterrupt;
        private bool jumpThrustCameraActive;
        private float jumpThrustBossBaseY;
        private bool waitingForStart;
        // 首段已计算过起手约束时，还需等普通格挡退出；此标记参与派发条件。
        private bool scheduleLogged;

        public bool IsBusy => started;
        public bool IsWaitingForStart => started && waitingForStart;

        public BT_ExecuteMove(CharacterBody body, BossMoveTable table)
        {
            this.body = body;
            this.table = table;
        }

        // 清除分段进度和角色上的招式上下文，并成对关闭招式镜头。
        public void ResetMove()
        {
            ReleaseJumpThrustCamera();
            if (started)
            {
                body.CurrentMoveEntry = null;
                body.CurrentMoveWindow = null;
            }
            started = false;
            waitingAttack = false;
            waitingForStart = false;
            scheduleLogged = false;
            allowInterrupt = false;
            entry = null;
            sequence = null;
            segment = 0;
        }

        // 选定一条可播放的动画分支，再尝试派发首段；等待起手也算执行中。
        public NodeState Begin(BossMoveEntry move, bool interruptCurrent = false)
        {
            ResetMove();
            if (move == null) return NodeState.Failure;
            sequence = BossMovePicker.ChooseSequence(move, body.Animator, body, table);
            if (sequence == null) return NodeState.Failure;
            entry = move;
            started = true;
            segment = 0;
            allowInterrupt = interruptCurrent;
            return FireCurrentSegment();
        }

        public override NodeState Evaluate()
        {
            if (!started) return NodeState.Failure;

            if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
            {
                ResetMove();
                return NodeState.Failure;
            }

            // 等待期间 Boss 弹回玩家时，交还交锋层，不继续上次预选的主动招。
            if (waitingForStart && body.KengekiArmed)
            {
                ResetMove();
                return NodeState.Failure;
            }

            if (waitingAttack)
            {
                // 反击已接收但尚在弹反态等起手，也算本段未完成。
                if (body.IsAttacking || body.IsParryCounterPending) return NodeState.Running;
                waitingAttack = false;
                segment++;
                TryInterruptAir5();
                // 起跳段结束时关闭跟随镜头，落地突刺沿用普通战斗镜头。
                if (jumpThrustCameraActive && segment > 0)
                    ReleaseJumpThrustCamera();
                if (segment >= sequence.states.Length)
                {
                    blackboard.SetCooldown(entry.id);
                    ResetMove();
                    return NodeState.Success;
                }
                return FireCurrentSegment();
            }

            return FireCurrentSegment();
        }

        // 五连箭的后撤段结束后，可按表中概率换成重箭，并结算原招冷却。
        private void TryInterruptAir5()
        {
            if (entry.id != "Bow_Air5" && entry.id != "Kengeki_Air5") return;
            if (segment <= 0) return;
            float chance = table.air5HeavyInterruptChance;
            if (Random.value > chance) return;
            BossMoveEntry heavy = table.FindById("Bow_Heavy");
            if (heavy == null) return;
            if (blackboard.IsOnCooldown(heavy.id, heavy.cooldown)) return;
            if (!BossMovePicker.AnySequencePlayable(heavy, body.Animator)) return;

            blackboard.SetCooldown(entry.id);
            entry = heavy;
            sequence = BossMovePicker.ChooseSequence(heavy, body.Animator, body, table);
            segment = 0;
        }

        // 先等待首段起手条件，再烘焙配置交给角色攻击状态执行。
        private NodeState FireCurrentSegment()
        {
            string anim = sequence.states[segment];
            BossMoveWindow w = BossMovePicker.WindowFor(entry, segment, sequence);
            if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
            {
                ResetMove();
                return NodeState.Failure;
            }

            if (segment == 0 && body.AttackHitNotBeforeTime > 0f)
            {
                // 从首个真实近战判定/出箭点倒推起手，不使用 NoHit 段的占位 HitStartTime。
                float firstCue = FirstOffensiveCueTime(w);
                float earliestStart = body.AttackHitNotBeforeTime - firstCue;
                waitingForStart = Time.time < earliestStart;
                scheduleLogged = true;
                if (waitingForStart) return NodeState.Running;
            }
            // 普通格挡中 StartAttack 会吞指令却不出招；等防御态退出再派发。
            if (segment == 0 && scheduleLogged && body.IsGuarding)
            {
                waitingForStart = true;
                return NodeState.Running;
            }
            waitingForStart = false;

            body.CurrentMoveEntry = entry;
            body.CurrentMoveWindow = w;
            AttackConfig baked = BossAttackBaker.Bake(entry, anim, w);
            baked.RotationSpeed = body.Config.RotationSpeed;
            if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
            {
                ResetMove();
                return NodeState.Failure;
            }
            body.ActiveAttack = baked;
            // 第二段起直接切 AttackState，避免先进 Idle 再 CrossFade 导致跨子状态机读不到动画时间。
            // 首段（segment == 0）与非地面态一律走 StartAttack 常规通道。
            bool startedMove = segment > 0
                && body.TryChangeGroundedSubState(g => new AttackState(body, g, baked));
            if (!startedMove && !body.StartAttack(baked, allowInterrupt))
            {
                ResetMove();
                return NodeState.Failure;
            }
            if (segment == 0)
            {
                body.ClearPlayerParryAttackWindow();
                // 延后起手时不能提前开启起跳镜头或清掉连弹计数。
                if (entry.id == "JumpThrust")
                {
                    jumpThrustCameraActive = true;
                    jumpThrustBossBaseY = body.GetJumpFollowWorldY();
                    CombatEventBus.TriggerJumpThrustCamera(true, body, jumpThrustBossBaseY);
                }
                if (entry.extra == BossMoveExtra.ConsecutiveParry2)
                    body.ResetConsecutiveTimesParried();
            }
            waitingAttack = true;
            return NodeState.Running;
        }

        // 首段没有攻击事件时保守取 0：等保护窗口结束再做位移段，避免猜测后续段时长。
        private static float FirstOffensiveCueTime(BossMoveWindow window)
        {
            float first = float.PositiveInfinity;
            if (window != null && window.hitPulses != null)
            {
                foreach (var pulse in window.hitPulses)
                {
                    if (AttackWindowSync.PulseIsMelee(pulse)) first = Mathf.Min(first, pulse.start);
                }
            }
            if (window != null && window.arrowCues != null)
            {
                foreach (var cue in window.arrowCues)
                {
                    if (cue != null) first = Mathf.Min(first, cue.time);
                }
            }
            return float.IsPositiveInfinity(first) ? 0f : Mathf.Max(0f, first);
        }

        void ReleaseJumpThrustCamera()
        {
            if (!jumpThrustCameraActive) return;
            CombatEventBus.TriggerJumpThrustCamera(false);
            jumpThrustCameraActive = false;
        }
    }

}
