using UnityEngine;

// Boss 招架（已废弃，文件保留参考）：
// 旧 M7 三层 AI 之三（防御/招架）：玩家攻击中 + 距离近 → Boss 短按防御（0.12s 后松手）。
// 已被 CharacterBody.TryPassiveDeflect 被动防御替代（命中瞬间判定，无 CD、无概率），
// 本文件与 BT_DeflectIf 保留仅供回滚对比，不再挂树。
public class BT_Deflect : Node
{
    private CharacterBody body;
    private float deflectStartTime;
    private bool active;
    private float holdDuration = 0.12f; // 短按：<0.15s 松手，窗口仍保持到结束

    public bool IsActive => active;

    public BT_Deflect(CharacterBody body)
    {
        this.body = body;
    }

    public override NodeState Evaluate()
    {
        if (!active)
        {
            // 按下防御
            bool accepted = body.TryExecuteCommand(new DeflectCommand());
            if (!accepted) return NodeState.Failure;

            deflectStartTime = Time.time;
            active = true;
            return NodeState.Running;
        }

        // 短按时间到 → 松手（甩刀，弹反窗口保持到 0.3s）
        if (Time.time - deflectStartTime >= holdDuration)
        {
            body.TryExecuteCommand(new IdleCommand());
            active = false;
            blackboard?.SetCooldown("deflect");
            return NodeState.Success;
        }

        return NodeState.Running;
    }
}
