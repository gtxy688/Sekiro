using UnityEngine;

namespace Sekiro.Core.Data
{
    /// <summary>
    /// Boss 属性 ScriptableObject 配置，支持两阶段。
    /// 每个 Boss 对应一份独立的 BossStats 资产。
    /// 创建方式：Unity Editor → Create → Combat → BossStats
    /// </summary>
    [CreateAssetMenu(fileName = "BossStats", menuName = "Combat/BossStats")]
    public class BossStats : ScriptableObject
    {
        [Header("一阶段")]

        /// <summary>一阶段最大生命值</summary>
        public float phase1MaxHealth = 1000f;

        /// <summary>一阶段最大架势值</summary>
        public float phase1MaxPosture = 300f;

        /// <summary>一阶段攻击力</summary>
        public float phase1Attack = 100f;

        /// <summary>一阶段可用招式列表</summary>
        public AttackData[] phase1Attacks;

        [Header("二阶段")]

        /// <summary>二阶段最大生命值</summary>
        public float phase2MaxHealth = 1200f;

        /// <summary>二阶段最大架势值</summary>
        public float phase2MaxPosture = 400f;

        /// <summary>二阶段攻击力</summary>
        public float phase2Attack = 120f;

        /// <summary>二阶段可用招式列表</summary>
        public AttackData[] phase2Attacks;

        /// <summary>二阶段攻速倍率</summary>
        public float attackSpeedMultiplier = 1.2f;

        /// <summary>二阶段前摇缩短比例</summary>
        public float startupReduction = 0.85f;

        [Header("阶段转换")]

        /// <summary>触发阶段转换的生命值百分比</summary>
        public float phaseTransitionHealthPercent = 0.5f;
    }
}
