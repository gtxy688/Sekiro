using UnityEngine;

// Boss 射箭（M7）：占位简化实现——拉弓 0.6s 后用 BowShot 配置发一次攻击
// （真实投射物/箭矢后续补，招式细节暂缓，见 04 架构文档）
public class BT_BowShot : Node
{
    private CharacterBody body;
    private AttackConfig bowConfig;
    private float bowStartTime;
    private bool active;
    private float drawDuration = 0.6f;

    public BT_BowShot(CharacterBody body, AttackConfig bowConfig)
    {
        this.body = body;
        this.bowConfig = bowConfig;
    }

    public override NodeState Evaluate()
    {
        if (!active)
        {
            if (bowConfig == null) return NodeState.Failure;

            // 拉弓动画（占位名，M8 接动画前）
            if (!AnimUtil.TryCrossFade(body.Animator, "Bow_Draw", 0.1f))
            {
                AnimUtil.TryCrossFade(body.Animator, "Bow_Shot", 0.1f);
            }
            bowStartTime = Time.time;
            active = true;
            return NodeState.Running;
        }

        if (Time.time - bowStartTime < drawDuration)
        {
            return NodeState.Running;
        }

        // 放箭：用 BowShot 配置出招（判定近距，投射物后补）
        body.ActiveAttack = bowConfig;
        bool accepted = body.TryExecuteCommand(new AttackCommand());
        active = false;
        blackboard?.SetCooldown("bow");
        return accepted ? NodeState.Success : NodeState.Failure;
    }
}
