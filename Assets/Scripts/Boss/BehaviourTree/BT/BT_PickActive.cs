using UnityEngine;
using System.Collections.Generic;

using ARPG.Boss;
using ARPG.FrameWork.Body;
namespace ARPG.Boss.BehaviourTree
{

    // 主动层：按距离加权抽招并播完。Busy 时续跑，不因距离微调取消。
    public class BT_PickActive : Node, ISelectorLock
    {
        private readonly CharacterBody body;
        private readonly BossMoveTable table;
        private readonly Transform target;
        private readonly BT_ExecuteMove executor;
        private readonly CharacterBody opponent;
        private readonly HashSet<string> whitelist;
        private readonly float roamAfterAttack;
        private readonly float roamAfterAttackJitter;

        public BT_PickActive(
            CharacterBody body, BossMoveTable table, Transform target, BT_ExecuteMove executor,
            CharacterBody opponent = null, HashSet<string> whitelist = null,
            float roamAfterAttack = 2.5f, float roamAfterAttackJitter = 1.2f)
        {
            this.body = body;
            this.table = table;
            this.target = target;
            this.executor = executor;
            this.opponent = opponent;
            this.whitelist = whitelist;
            this.roamAfterAttack = roamAfterAttack;
            this.roamAfterAttackJitter = roamAfterAttackJitter;
        }

        public override void SetBlackboard(Blackboard bb)
        {
            base.SetBlackboard(bb);
            executor.SetBlackboard(bb);
        }

        public override NodeState Evaluate()
        {
            if (BTUtil.IsTargetIncapacitated(target))
            {
                executor.ResetMove();
                return NodeState.Failure;
            }

            // 先清除硬直中被打断的执行器，避免沿用未完成招式的进度。
            if (body.IsPostureBroken || body.IsParried)
            {
                executor.ResetMove();
                return NodeState.Failure;
            }
            if (executor.IsBusy)
            {
                NodeState busy = executor.Evaluate();
                if (busy != NodeState.Running)
                    ArmRoamGap();
                return busy;
            }

            bool isRoamGapActive = IsRoamGapActive();
            if (isRoamGapActive)
                return NodeState.Failure;

            float dist = Vector3.Distance(body.transform.position, target.position);
            // 距离范围由招式表过滤；没有可用招式时返回失败，让低优先级走位接管。
            BossMoveEntry move = BossMovePicker.Pick(
                table, BossMoveLayer.Active, body, body.Animator, blackboard, dist, opponent, whitelist);
            if (move == null) return NodeState.Failure;
            return executor.Begin(move);
        }

        // 主动招执行结束后留下随机走位间隔，避免每次选招都紧接上一招。
        private void ArmRoamGap()
        {
            float gap = roamAfterAttack + UnityEngine.Random.Range(0f, Mathf.Max(0f, roamAfterAttackJitter));
            blackboard.ActiveGapDuration = gap;
            blackboard.SetCooldown("active_gap");
        }

        private bool IsRoamGapActive()
        {
            float gap = blackboard.ActiveGapDuration;
            if (gap <= 0.01f) gap = roamAfterAttack;
            return blackboard.IsOnCooldown("active_gap", gap);
        }
    }

}
