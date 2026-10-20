using System;
using UnityEngine;
using UnityEngine.Serialization;

using ARPG.Combat;
namespace ARPG.Configs
{

    // 一条攻击动画内的一次近战判定区间；start/end 都是相对动画起点的秒数，使用 [start, end)。
    // 一段动画可以配置多刀，每换一个有效脉冲会重新开启 Hitbox 并清除上一刀的命中记录。
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
            // 新建独立脉冲对象，避免临时烘焙/修改配置时与原数组共用同一项引用。
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

    // 相对攻击动画起点的音效事件，与近战判定区间独立。
    [Serializable]
    public class AttackSfxCue
    {
        public float time;
        public AudioClip clip;
    }

    // 相对攻击动画起点的出箭事件，可覆盖该箭的战斗数值。
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

    // 攻击状态读取的动画、节奏和命中数据；玩家使用资产，Boss 按招式段临时烘焙。
    // 配置回答“这次出哪招、什么时间允许什么行为”；攻击状态读取它并实际推进动作。
    [CreateAssetMenu(fileName = "NewAttackConfig", menuName = "Combat/Attack Configuration")]
    public class AttackConfig : ScriptableObject
    {
        [Header("表现配置")]
        public string AnimName;             // 动画状态机里的名字
        public float TransitionDuration = 0.1f; // 传给 Animator.CrossFade 的归一化过渡长度，不是固定秒数。

        [Header("战斗数值")]
        public int BaseDamage = 10;         // 基础伤害
        public float PostureDamage = 15f;   // 躯干伤害
        public float Knockback = 0f;        // 击退强度（0=普通受击；>0 对方播 Heavy 受击/击飞动画，接口预留）

        [Tooltip("打到玩家时的受击等级。Boss 被打仍看 Knockback。")]
        public HitGrade HitGrade = HitGrade.Light;

        [Header("危字攻击")]
        // 危字类型交给战斗结算决定可用的防御和反制方式。
        public PerilousType Perilous = PerilousType.None;

        [Header("判定 Hitbox")]
        [Tooltip("本招用哪把采样点。玩家保持 Weapon。Boss 的 Elbow 段（拳头）选 Elbow。")]
        public AttackHitboxSlot HitboxSlot = AttackHitboxSlot.Weapon;

        [Header("取消窗口")]
        // 下列节奏时刻都从本招动画 0 点算起：前摇可取消 → 中段锁定 → 后摇开放取消/连招。
        [Tooltip("前摇结束时刻：进招后多少秒内可用格挡/垫步取消。只管招式节奏，不开判定——判定一律由下方 hitPulses 决定")]
        public float HitStartTime = 0.2f;

        [Header("连招窗口期")]
        public float StateDuration = 1.633f;  // 这个动作总共持续多久
        [FormerlySerializedAs("ComboWindowStart")]
        [Tooltip("开放连招/取消：到 ComboWindowEnd 可接 NextCombo，也可格挡/垫步/移动。动画仍播到 StateDuration。只管招式节奏，判定结束时刻由 hitPulses 末段决定")]
        public float RecoveryWindowStart = 0f; // 连招/取消开放时刻（不再兼作判定结束）
        public float ComboWindowEnd = 0.33f;   // 超过此动画内时刻，AttackCommand 不再接 NextCombo。

        [Header("攻击转向")]
        public bool AllowRotation = true;
        // AllowRotation 允许代码转向，RotationWindowEnd 限制通常的追踪时段；位移仍取动画根运动。
        public float RotationSpeed = 720f;
        public float RotationWindowEnd = 0.3f;

        [Tooltip("为 true 时等 AnimName 播完才结束本段（JumpThrust 起跳）。")]
        public bool WaitAnimEnd;

        [Header("连招派生")]
        // 连招窗口内再收到 AttackCommand 才接续，不会自动连播；末段留空。
        public AttackConfig NextCombo;

        [Header("出伤脉冲（玩家 1 段 / Boss 多段）")]
        [Tooltip("按各段 [start,end) 开关近战判定并清空已命中目标；空数组表示无近战判定。玩家通常 1 段，Boss 可多段")]
        public HitPulse[] hitPulses;

        [Header("出招音效（可选）")]
        [Tooltip("相对本招动画 0 点的秒。与判定窗独立。clip 为空则跳过")]
        public AttackSfxCue[] sfxCues;

        [Header("出箭（可选）")]
        [Tooltip("相对本招动画 0 点的秒。到点调 SpawnArrow。空 = 不出箭")]
        public ArrowSpawnCue[] arrowCues;

        // 编辑资产时用首末脉冲同步取消/连招起点，仅向后扩展连招终点，保留手调余量。
        // Boss 运行时烘焙保留招式表的独立节奏，不通过此入口同步。
        private void OnValidate()
        {
            // Unity 编辑配置资产时同步辅助字段；运行时命中检测仍直接读取 hitPulses。
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
