using System.Collections.Generic;
using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Combat
{
    // 一场战斗的边界。当前场景里只有 1 个；连战时每场一个，由流程层激活。
    //
    // 为什么现在就要：多 Boss 不会让架构自动变好，它只会把 1 个特例复制成 N 个特例。
    // 现在只有 1 场战斗时把边界立起来，成本是几十行；
    // 等 3 个 Boss 都写完再补，要动 41 处调用点，每处都要回归。
    //
    // 职责只有两条：
    //   1. 声明这场战斗的参与者（玩家 + 1~N 个对手），
    //      取代 CombatManager 上写死的两个序列化字段 PlayerRef / BossRef。
    //   2. 收集所有 ICombatResettable，提供一次性 ResetAll()。
    //
    // 明确不做：不负责生成 Boss、不负责连战流程编排、不负责存档。
    // 那些属于流程层，等真要做复战时需求才清楚，现在做就是猜。
    public class EncounterScope : MonoBehaviour
    {
        public static EncounterScope Current { get; private set; }

        [Header("参与者")]
        [Tooltip("本场战斗的玩家。留空则回退到 CombatManager 上手工拖的引用，行为与改动前一致。")]
        public CharacterBody Player;

        [Tooltip("本场战斗的对手。复战换 Boss、连战多 Boss 时在这里增减，CombatManager 不用改。")]
        public List<CharacterBody> Opponents = new List<CharacterBody>();

        // 主对手：单 Boss 语义下等价于旧的 CombatManager.BossRef。
        // 多 Boss 时它只是"列表里第一个"，用于锁定指示这类只有单一目标的地方。
        public CharacterBody PrimaryOpponent =>
            Opponents != null && Opponents.Count > 0 ? Opponents[0] : null;

        // 是否显式声明了参与者。留空 = 未配置，CombatManager 会回退到自己的 PlayerRef/BossRef。
        // 用来区分「有 Scope 且配好了」与「自动兜底出来的空壳」——
        // 否则自动兜底会把「PlayerRef 未绑定」这类配置错误一起吞掉，诊断变差。
        public bool HasParticipants =>
            Player != null || (Opponents != null && Opponents.Count > 0);

        private readonly List<ICombatResettable> resettable = new List<ICombatResettable>();

        // Awake 先于所有 Start，保证别的组件在 Start 里注册时 Current 已经就位。
        private void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning(
                    $"场景中有多个 EncounterScope（{Current.name} 与 {name}），已改用 {name}。" +
                    "连战模式下应由流程层决定激活哪一个。", this);
            }
            Current = this;
        }

        private void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        // 取本场战斗的边界；场景里没有就兜底创建一个空的。
        //
        // 为什么兜底而不是要求手工挂：当前只有一场战斗，手工挂出来的通常也是个空组件
        // （Player/Opponents 留空即回退 PlayerRef/BossRef），那这一步就是纯仪式。
        // 而忘了挂的代价是整条复位契约静默失效——恰恰是几个月后真做复战最难想起来的那类坑。
        // 与其靠一条警告提醒人去点一下，不如让失效模式不存在。
        //
        // ⚠️ 调用时机：只能在 Start 及之后调用，不可在 Awake 里调。
        // Unity 保证「所有 Awake 跑完才进 Start」，所以在 Start 里 Current==null
        // 就一定是场景里真没有，不会误判、不会造出第二个。
        // 在 Awake 里调则相反：跨组件 Awake 顺序不定，会重复创建。
        // （挂在 inactive 物体上的 Scope 其 Awake 不跑，视为不存在——本就不该生效。）
        //
        // 每个注册方都自己调一次，而不是只靠 CombatManager 先建好：
        // 各组件的 Start 顺序同样不定，谁先跑到谁就负责创建，后跑的复用同一个。
        public static EncounterScope Ensure()
        {
            if (Current != null) return Current;

            GameObject host = new GameObject("EncounterScope (Auto)");
            EncounterScope scope = host.AddComponent<EncounterScope>();
            Debug.Log(
                "[EncounterScope] 场景里没有 EncounterScope，已自动创建一个空的。\n" +
                "当前战斗一切正常（Player/Opponents 留空 = 回退 PlayerRef / BossRef）。\n" +
                "将来做多 Boss / 连战时，请在场景里显式挂一个并填好 Player 与 Opponents。",
                scope);
            return scope;
        }

        public void Register(ICombatResettable target)
        {
            if (target == null) return;
            if (!resettable.Contains(target))
                resettable.Add(target);
        }

        public void Unregister(ICombatResettable target)
        {
            if (target == null) return;
            resettable.Remove(target);
        }

        // 复战 / 连战切场时调用。
        //
        // 顺序说明（重要）：这里倒序遍历，但**不要依赖它**。
        // 所有组件都在各自的 Start 里注册，而 Unity 不保证跨组件 Start 的先后顺序，
        // 所以"倒序"只是一条无意义的稳定排序，不等于"AI 先于 CharacterBody 归零"。
        // 要确定性顺序得给每个组件配 [DefaultExecutionOrder] 或给接口加优先级字段——
        // 现在没有这个需求，加了就是维护负担，所以改成一条硬约束：
        //
        //   **ResetForEncounter() 必须顺序无关且可重复调用。**
        //
        // 也就是：不要读别人还没复位的字段，只写自己的状态；
        // 需要"初始值"就从 Config（序列化 SO，整局不变）读，不要从 body 的当前值读。
        // UI 那边已经按这条写的（命数点/回生点读 Config 而非 body），改动时请一并遵守。
        public void ResetAll()
        {
            for (int i = resettable.Count - 1; i >= 0; i--)
            {
                ICombatResettable item = resettable[i];
                if (IsGone(item))
                {
                    resettable.RemoveAt(i);
                    continue;
                }
                item.ResetForEncounter();
            }
        }

        // 接口引用上的 == 不会触发 UnityEngine.Object 的销毁判定，
        // 已销毁的 MonoBehaviour 会以"假非空"形式留在表里，必须显式识别。
        static bool IsGone(ICombatResettable target)
        {
            if (target == null) return true;
            return target is Object unityObj && unityObj == null;
        }
    }
}
