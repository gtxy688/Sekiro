using System.Collections.Generic;
using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.Combat
{
    // 本场战斗的范围：声明玩家和对手，收集 ICombatResettable 供统一复位。
    // 参与者来自场景引用；本类不负责生成角色或编排连战流程。
    public class EncounterScope : MonoBehaviour
    {
        public static EncounterScope Current { get; private set; }

        [Header("参与者")]
        [Tooltip("本场战斗的玩家。留空则回退到 CombatManager 上手工拖的引用，行为与改动前一致。")]
        public CharacterBody Player;

        [Tooltip("本场战斗的对手。复战换 Boss、连战多 Boss 时在这里增减，CombatManager 不用改。")]
        public List<CharacterBody> Opponents = new List<CharacterBody>();

        // 主对手固定取列表首项，用于仍只处理一个 Boss 的接口；不是运行时选敌逻辑。
        public CharacterBody PrimaryOpponent =>
            Opponents != null && Opponents.Count > 0 ? Opponents[0] : null;

        // 区分已配置的范围与自动创建的空范围，供 CombatManager 决定是否检查回退引用。
        public bool HasParticipants =>
            Player != null || (Opponents != null && Opponents.Count > 0);

        private readonly List<ICombatResettable> resettable = new List<ICombatResettable>();

        // 初始场景中的活动对象先完成 Awake，其他组件在 Start 注册时即可取得 Current。
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

        // 在 Start 或之后取得范围对象，缺失时创建空范围供后续注册方复用。
        // 避免在 Awake 调用，否则可能先于场景 Scope 初始化而误建重复对象。
        public static EncounterScope Ensure()
        {
            if (Current != null) return Current;

            GameObject host = new GameObject("EncounterScope (Auto)");
            EncounterScope scope = host.AddComponent<EncounterScope>();
            return scope;
        }

        // 登记复位能力，不要求组件按某个固定顺序完成 Start；重复登记同一对象会被忽略。
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

        // 各组件自行复位且不得依赖调用顺序；倒序仅用于安全移除已销毁的登记项。
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
            if (target == null) 
                return true;
            return target is Object unityObj && unityObj == null;
        }
    }
}
