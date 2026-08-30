using UnityEngine;
using System.Collections.Generic;

namespace ARPG.Boss
{

    [CreateAssetMenu(fileName = "BossMoveTable", menuName = "Combat/Boss Move Table")]
    public class BossMoveTable : ScriptableObject
    {
        [Tooltip("交锋抽招最大距离；超过则 NoAction")]
        public float kengekiMaxRange = 2.5f;

        [Tooltip("<=0 则用 Config.MaxPosture * 0.5（本项目 MaxPosture 默认 100，不能照抄原作 360）")]
        public float postureLowThreshold = 0f;

        [Range(0f, 1f)]
        public float air5HeavyInterruptChance = 0.5f;

        public BossMoveEntry[] moves;

        // 运行时调试白名单（不序列化进资产）：非空时抽招只允许名单内的招式 id。
        // BTBrain.MeleeOnly 用它把 Boss 限制成"只近战普通攻击 + 格挡"，屏蔽弓/后跳/特殊招。
        [System.NonSerialized]
        public HashSet<string> moveWhitelist;

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
