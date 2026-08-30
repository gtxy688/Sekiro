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

        public bool IsBusy => started;

        public BT_ExecuteMove(CharacterBody body, BossMoveTable table)
        {
            this.body = body;
            this.table = table;
        }

        public void ResetMove()
        {
            ReleaseJumpThrustCamera();
            if (started && body != null)
            {
                body.CurrentMoveEntry = null;
                body.CurrentMoveWindow = null;
            }
            started = false;
            waitingAttack = false;
            allowInterrupt = false;
            entry = null;
            sequence = null;
            segment = 0;
        }

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
            jumpThrustCameraActive = move.id == "JumpThrust";
            if (jumpThrustCameraActive)
            {
                // 跟髋不跟根：跳跃 Clip 常 keepOriginalPositionY，根贴地时视觉已在天上。
                jumpThrustBossBaseY = body.GetJumpFollowWorldY();
                CombatEventBus.TriggerJumpThrustCamera(true, body, jumpThrustBossBaseY);
            }
            NodeState fired = FireCurrentSegment();
            // 起跳成功才清连弹计数，避免 Begin 失败后永远抽不到。
            if (fired != NodeState.Failure && move.extra == BossMoveExtra.ConsecutiveParry2)
                body.ResetConsecutiveTimesParried();
            return fired;
        }

        public override NodeState Evaluate()
        {
            if (!started) return NodeState.Failure;

            if (body.IsParried || body.IsPostureBroken || body.IsFinisherLocked)
            {
                ResetMove();
                return NodeState.Failure;
            }

            if (waitingAttack)
            {
                if (body.IsAttacking) return NodeState.Running;
                waitingAttack = false;
                segment++;
                TryInterruptAir5();
                // Clip 末段已是下落：起跳段一结束就收镜头，不要挂到落地突刺。
                if (jumpThrustCameraActive && segment > 0)
                    ReleaseJumpThrustCamera();
                if (sequence == null || segment >= sequence.states.Length)
                {
                    blackboard?.SetCooldown(entry.id);
                    ResetMove();
                    return NodeState.Success;
                }
                return FireCurrentSegment();
            }

            return FireCurrentSegment();
        }

        private void TryInterruptAir5()
        {
            if (entry == null) return;
            if (entry.id != "Bow_Air5" && entry.id != "Kengeki_Air5") return;
            if (segment <= 0) return;
            float chance = table != null ? table.air5HeavyInterruptChance : 0f;
            if (Random.value > chance) return;
            BossMoveEntry heavy = table != null ? table.FindById("Bow_Heavy") : null;
            if (heavy == null) return;
            if (blackboard != null && blackboard.IsOnCooldown(heavy.id, heavy.cooldown)) return;
            if (!BossMovePicker.AnySequencePlayable(heavy, body.Animator)) return;

            blackboard?.SetCooldown(entry.id);
            entry = heavy;
            sequence = BossMovePicker.ChooseSequence(heavy, body.Animator, body, table);
            segment = 0;
        }

        private NodeState FireCurrentSegment()
        {
            string anim = sequence.states[segment];
            BossMoveWindow w = BossMovePicker.WindowFor(entry, segment, sequence);
            body.CurrentMoveEntry = entry;
            body.CurrentMoveWindow = w;
            AttackConfig baked = BossAttackBaker.Bake(entry, anim, w);
            if (body.Config != null)
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
            waitingAttack = true;
            return NodeState.Running;
        }

        void ReleaseJumpThrustCamera()
        {
            if (!jumpThrustCameraActive) return;
            CombatEventBus.TriggerJumpThrustCamera(false);
            jumpThrustCameraActive = false;
        }
    }

}
