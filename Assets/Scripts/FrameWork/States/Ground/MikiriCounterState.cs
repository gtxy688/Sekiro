using UnityEngine;

using ARPG.Combat;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.FrameWork.States.Base;
namespace ARPG.FrameWork.States.Ground
{

    // 无方向垫步识破危字突刺后播放踩刀，并增加攻击者架势、打断其攻击。
    // 若造成崩解，踩刀剩余动画作为忍杀确认窗口。
    // DodgeState 命中检查识别到可识破的突刺后进入；传入 HitData 只用于取得这次攻击者。
    public class MikiriCounterState : BaseState
    {
        private const string MikiriAnim = "Mikiri";
        private const float FallbackDuration = 2.5f;

        private HierarchicalState parent;
        private CharacterBody attacker;
        private float timer;
        private float postureGain;
        private bool finisherWindow;
        private bool hasSeenMikiri;

        public MikiriCounterState(CharacterBody body, HierarchicalState parent, HitData hit) : base(body)
        {
            this.parent = parent;
            this.attacker = hit.attacker;
            postureGain = body.Config.MikiriPostureGain;
        }

        public override void OnEnter()
        {
            timer = 0f;
            hasSeenMikiri = false;

            if (!AnimUtil.TryPlay(body.Animator, MikiriAnim))
            {
                Debug.LogError($"{body.name} 的 Animator 缺少识破状态：{MikiriAnim}");
            }

            finisherWindow = false;
            if (attacker != null)
            {
                finisherWindow = attacker.AccumulatePosture(
                    postureGain,
                    allowBreak: true,
                    source: PostureBreakSource.Mikiri);
                // 没打崩也要打断挥刀，播被识破硬直（只狼：踩刀把这一招废掉）。
                if (!finisherWindow)
                {
                    attacker.ForceMikiriStun();
                }
            }

            // 识破是踩刀，不走格挡/弹反的打铁音效与火花（OnWeaponDeflected）。
        }

        public override void OnUpdate()
        {
            timer += Time.deltaTime;

            AnimatorStateInfo info = body.Animator.GetCurrentAnimatorStateInfo(0);
            if (AnimUtil.IsPlaying(info, MikiriAnim))
            {
                hasSeenMikiri = true;
                if (info.normalizedTime >= 0.95f)
                {
                    ExpireWindow();
                }
                return;
            }

            if (hasSeenMikiri && !body.Animator.IsInTransition(0))
            {
                ExpireWindow();
                return;
            }

            // 没切到 Clip 时才用兜底，不能用 0.8s 配置把确认窗口提前掐掉。
            if (!hasSeenMikiri && timer >= FallbackDuration)
            {
                ExpireWindow();
            }
        }

        public override bool HandleCommand(ICommand cmd)
        {
            // 只用机会出现后新生成的 FinisherCommand 确认忍杀；普通攻击预输入和其他命令均消费掉。
            if (finisherWindow && cmd is FinisherCommand)
            {
                CombatManager.Instance.TryExecuteFinisher(body, FinisherKind.Mikiri);
            }
            return true;
        }

        private void ExpireWindow()
        {
            if (finisherWindow && attacker != null && attacker.IsPostureBroken)
            {
                // 确认窗口过期后解除对手崩解，架势保留为上限的 80%，不是恢复 80% HP。
                attacker.RecoverFromBreak(0.8f);
            }
            parent.SubStateMachine.ChangeState(new IdleState(body, parent));
        }
    }

}
