using System;
using UnityEngine;
using UnityEngine.Serialization;

using ARPG.Combat;
namespace ARPG.Configs
{

    // 一条动画里的一次出伤开/关。空数组 = 仍用 HitStartTime / RecoveryWindowStart 单窗。
    [Serializable]
    public class HitPulse
    {
        public float start;
        public float end;

        [Tooltip("勾选后本刀用下面的伤害和等级；不勾则继承段覆盖或招默认值")]
        public bool overrideCombat;
        public int baseDamage;
        public float postureDamage;
        public float knockback;
        public HitGrade hitGrade;

        public HitPulse Clone()
        {
            return new HitPulse
            {
                start = start,
                end = end,
                overrideCombat = overrideCombat,
                baseDamage = baseDamage,
                postureDamage = postureDamage,
                knockback = knockback,
                hitGrade = hitGrade
            };
        }
    }

    [Serializable]
    public class AttackSfxCue
    {
        public float time;
        public AudioClip clip;
    }

    [Serializable]
    public class ArrowSpawnCue
    {
        public float time;

        [Tooltip("勾选后本箭用下面的伤害和等级；不勾则继承段覆盖或招默认值")]
        public bool overrideCombat;
        public int baseDamage;
        public float postureDamage;
        public float knockback;
        public HitGrade hitGrade;

        public ArrowSpawnCue Clone()
        {
            return new ArrowSpawnCue
            {
                time = time,
                overrideCombat = overrideCombat,
                baseDamage = baseDamage,
                postureDamage = postureDamage,
                knockback = knockback,
                hitGrade = hitGrade
            };
        }
    }

    // 这个标签让你可以在 Unity 项目的右键菜单里直接创建这个配置文件
    [CreateAssetMenu(fileName = "NewAttackConfig", menuName = "Combat/Attack Configuration")]
    public class AttackConfig : ScriptableObject
    {
        [Header("表现配置")]
        public string AnimName;             // 动画状态机里的名字
        public float TransitionDuration = 0.1f; // 动画过渡时间

        [Header("战斗数值")]
        public int BaseDamage = 10;         // 基础伤害
        public float PostureDamage = 15f;   // 躯干伤害
        public float Knockback = 0f;        // 击退强度（0=普通受击；>0 对方播 Heavy 受击/击飞动画，接口预留）

        [Tooltip("打到玩家时的受击等级。Boss 被打仍看 Knockback。")]
        public HitGrade HitGrade = HitGrade.Light;

        [Header("危字攻击（M17）")]
        // 危字攻击不可被普通防御/弹反抵挡，玩家必须用对应方式应对
        public PerilousType Perilous = PerilousType.None;

        [Header("判定 Hitbox")]
        [Tooltip("本招用哪把采样点。玩家保持 Weapon。Boss 的 Elbow 段（拳头）选 Elbow。")]
        public AttackHitboxSlot HitboxSlot = AttackHitboxSlot.Weapon;

        [Header("取消窗口")]
        [Tooltip("前摇结束时刻：进招后多少秒内可用格挡/垫步取消。只管招式节奏，不开判定——判定一律由下方 hitPulses 决定")]
        public float HitStartTime = 0.2f;

        [Header("连招窗口期")]
        public float StateDuration = 1.633f;  // 这个动作总共持续多久
        [FormerlySerializedAs("ComboWindowStart")]
        [Tooltip("开放连招/取消：到 ComboWindowEnd 可接 NextCombo，也可格挡/垫步/移动。动画仍播到 StateDuration。只管招式节奏，判定结束时刻由 hitPulses 末段决定")]
        public float RecoveryWindowStart = 0f; // 连招/取消开放时刻（不再兼作判定结束）
        public float ComboWindowEnd = 0.33f;   // 多久之后按键无效（错过连招）

        [Header("攻击转向")]
        public bool AllowRotation = true;
        public float RotationSpeed = 720f;
        public float RotationWindowEnd = 0.3f;

        [Tooltip("为 true 时等 AnimName 播完才结束本段（JumpThrust 起跳）。")]
        public bool WaitAnimEnd;

        [Header("连招派生")]
        // 极其关键：指向下一段攻击的配置！如果没有下一段，留空即可
        public AttackConfig NextCombo;

        [Header("出伤脉冲（玩家 1 段 / Boss 多段）")]
        [Tooltip("有元素时按每段 [start,end) 开关刀并清空已命中；空则走上面的单窗。玩家通常 1 段，Boss 可多段")]
        public HitPulse[] hitPulses;

        [Header("出招音效（可选）")]
        [Tooltip("相对本招动画 0 点的秒。与判定窗独立。clip 为空则跳过")]
        public AttackSfxCue[] sfxCues;

        [Header("出箭（可选）")]
        [Tooltip("相对本招动画 0 点的秒。到点调 SpawnArrow。空 = 不出箭")]
        public ArrowSpawnCue[] arrowCues;

        // 判定窗的唯一真相是 hitPulses。HitStartTime / RecoveryWindowStart 已降级为招式级字段
        // （前摇可取消的截止、连招开放的起点），这里从 pulses 反向同步，避免两套数值各说各话。
        //
        // 只做「补」不做「削」：ComboWindowEnd 只会往上抬到 RecoveryWindowStart，
        // 不会往回压，免得手工调好的连招窗口被自动同步吃掉。
        //
        // 生效范围只有磁盘上的 AttackConfig 资产（玩家 10 份 + bossAtk 模板）。
        // BossAttackBaker 运行时烤出来的实例不触发 OnValidate，而且那边是刻意不同步的：
        // 招式表 window 的 hitStartTime / recoverStart 带着 Boss 自己的连招节奏，不能被 pulses 覆盖。
        private void OnValidate()
        {
            if (hitPulses == null || hitPulses.Length == 0) return;
            HitPulse first = hitPulses[0];
            HitPulse last = hitPulses[hitPulses.Length - 1];
            if (first == null || last == null) return;

            HitStartTime = first.start;
            RecoveryWindowStart = last.end;
            if (ComboWindowEnd < RecoveryWindowStart)
                ComboWindowEnd = RecoveryWindowStart;
        }
    }
}
