using UnityEngine;

namespace ARPG.Boss
{

    // Boss 共用的招式与选招参数；冷却和当前执行进度保存在各 Boss 实例上。
    [CreateAssetMenu(fileName = "BossMoveTable", menuName = "Combat/Boss Move Table")]
    public class BossMoveTable : ScriptableObject
    {
        [Tooltip("交锋抽招最大距离；超过则 NoAction")]
        public float kengekiMaxRange = 2.5f;

        [Tooltip("累计架势达到该阈值时放行 PostureLow 条件；<=0 时使用角色最大架势的一半")]
        public float postureLowThreshold = 0f;

        [Range(0f, 1f)]
        public float air5HeavyInterruptChance = 0.5f;

        public BossMoveEntry[] moves;

        public BossMoveEntry FindById(string id)
        {
            if (moves == null || string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < moves.Length; i++)
            {
                if (moves[i] != null && moves[i].id == id)
                    return moves[i];
            }
            return null;
        }
    }

}
