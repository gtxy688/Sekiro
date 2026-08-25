using UnityEngine;

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
