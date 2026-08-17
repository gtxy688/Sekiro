using UnityEngine;

// Boss 近战连段（M7 主动计划）：按间隔逐刀发 AttackCommand，招式从 body.AttackSet（SO 数组）取
// 连段结束后写黑板冷却。AttackCommand 不携带配置（用户决策 9），
// 出招前设置 body.ActiveAttack，AttackState 优先读它
public class BT_Combo : Node
{
    private CharacterBody body;
    private int maxAttacks;         // 本次连段最多几刀
    private int attackCount;        // 已砍几刀
    private float lastSlashTime;    // 上一刀时间
    private float slashInterval = 0.4f;

    public BT_Combo(CharacterBody body, int maxAttacks = 3)
    {
        this.body = body;
        this.maxAttacks = maxAttacks;
    }

    public override NodeState Evaluate()
    {
        // 连段完成
        if (attackCount >= maxAttacks)
        {
            attackCount = 0;
            blackboard?.SetCooldown("combo");
            return NodeState.Success;
        }

        // 刀与刀之间的间隔未到 → 继续跑
        if (Time.time - lastSlashTime < slashInterval)
        {
            return NodeState.Running;
        }

        // 选招：AttackSet 按刀序取（越界回最后一招），没有则回退 LightAttack
        if (body.AttackSet != null && body.AttackSet.Length > 0)
        {
            int idx = Mathf.Min(attackCount, body.AttackSet.Length - 1);
            body.ActiveAttack = body.AttackSet[idx];
        }
        else
        {
            body.ActiveAttack = body.LightAttack;
        }

        bool accepted = body.TryExecuteCommand(new AttackCommand());
        if (!accepted)
        {
            // 硬直/不可出手：连段失败，交上层重新决策
            attackCount = 0;
            return NodeState.Failure;
        }

        attackCount++;
        lastSlashTime = Time.time;
        return NodeState.Running;
    }
}
