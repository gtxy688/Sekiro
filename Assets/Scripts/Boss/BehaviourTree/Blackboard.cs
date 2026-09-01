using System.Collections.Generic;

namespace ARPG.Boss.BehaviourTree
{

    // 行为树黑板：Boss AI 的招式冷却与走位间隔记账（挂在树根，所有节点共享）。
    // 陷阱3-2 修复：原 Dictionary<string, object> 存 float 会装箱、IsOnCooldown 每次
    // "cd_" + key 拼接字符串产生分配（BossMovePicker 每帧对每招调用）。
    // 改为专用冷却字典：值类型 float 直存不装箱；key 直接用招式 id——
    // 独立字典无键冲突，不再需要 cd_ 前缀；走位间隔改强类型属性。
    public class Blackboard
    {
        // 招式冷却：key = 招式 id（来自 SO），value = 上次执行的时间戳
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        // 出招后强制走位间隔的时长（ArmRoamGap 写入，IsRoamGapActive 读取）
        public float ActiveGapDuration { get; set; }

        // 冷却工具：招式冷却（M7，参考 sekiro 逆向的 SetCoolTime）
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

        // 复战重置：清空全部招式冷却与走位间隔。
        //
        // 为什么是 Clear 而不是让 BTBrain 重建一个 Blackboard：
        // 每个 BT 节点都持有黑板引用，重建需要重新下发到整棵树，漏掉任何一个节点
        // 它就会继续读旧黑板——这类 bug 极难排查。清空现有实例则不存在这个问题。
        //
        // 不清理的后果：上一场用过的招式冷却会带进新一场，复战开局前若干秒 Boss 不出招。
        public void Clear()
        {
            cooldowns.Clear();
            ActiveGapDuration = 0f;
        }
    }

}
