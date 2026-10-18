using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Boss
{

    // 行为树共用的目标状态查询，统一玩家倒地和回生期间的停攻条件。
    public static class BTUtil
    {
        // 目标（玩家）是否处于"Boss 不应继续进攻"的状态（倒地 / 回生中）
        public static bool IsTargetIncapacitated(Transform target)
        {
            if (target == null) return false;
            CharacterBody player = target.GetComponentInParent<CharacterBody>();
            return player.IsIncapacitatedForBoss;
        }
    }
}
