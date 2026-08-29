using UnityEngine;

// 行为树节点共享的静态工具：把 Transform 目标解析成 CharacterBody 并查询状态。
// 三个出招节点（BT_Kengeki / BT_MoveToTarget / BT_PickActive）原来各有一份
// IsTargetIncapacitated 复制品，统一收敛到这里。
public static class BTUtil
{
    // 目标（玩家）是否处于"Boss 不应继续进攻"的状态（倒地 / 回生中）
    public static bool IsTargetIncapacitated(Transform target)
    {
        if (target == null) return false;
        CharacterBody player = target.GetComponent<CharacterBody>();
        if (player == null) player = target.GetComponentInParent<CharacterBody>();
        return player != null && player.IsIncapacitatedForBoss;
    }
}