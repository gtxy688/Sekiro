using System.Collections.Generic;

namespace ARPG.Boss.BehaviourTree
{

    // 单个 Boss 的行为树共享数据；冷却直接用招式 ID 和 float 时间戳，避免逐帧装箱或拼接键。
    public class Blackboard
    {
        // 招式冷却：key = 招式 id（来自 SO），value = 上次执行的时间戳
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        // 出招后强制走位间隔的时长（ArmRoamGap 写入，IsRoamGapActive 读取）
        public float ActiveGapDuration { get; set; }

        // 从未执行过的招式应当立即可用，不能把缺失值 0 当成开局时间戳。
        public bool IsOnCooldown(string moveId, float cooldown)
        {
            return cooldowns.TryGetValue(moveId, out float last)
                && UnityEngine.Time.time - last < cooldown;
        }

        public void SetCooldown(string moveId)
        {
            cooldowns[moveId] = UnityEngine.Time.time;
        }

        // 复战时清空现有实例，保留所有节点持有的黑板引用。
        public void Clear()
        {
            cooldowns.Clear();
            ActiveGapDuration = 0f;
        }
    }

}
