using System;
using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
namespace ARPG.Boss
{

    // 单段动画的节奏、判定和事件配置，由 BossAttackBaker 转为运行时 AttackConfig。
    // 近战只由 hitPulses 决定；关闭近战的工具会同步将节奏窗口收至段末。
    [Serializable]
    public class BossMoveWindow
    {
        public float hitStartTime = 0.2f;
        public float recoverStart = 0.8f;
        public float comboWindowEnd = 0.9f;
        public float stateDuration = 1.8f;
        public float rotateEnd = 0.3f;
        [Tooltip("切到本段动画的 CrossFade 时长（秒）。JumpThrust 落地突刺改第二段；越小切得越干脆。")]
        public float transitionDuration = 0.1f;

        [Tooltip("段级危字标记（优先于招式的 entry.perilous）。用于一招多段中仅某段是危字的情况，如 Slash_SpinElbow 的 Elbow 段 = Grab。段级与招式级都未标 = 非危字")]
        public PerilousType perilous = PerilousType.None;

        [Tooltip("本段用哪把 Hitbox。默认刀；Elbow 段（拳头）选 Elbow。角色必须绑定对应槽位的 Hitbox。")]
        public AttackHitboxSlot hitboxSlot = AttackHitboxSlot.Weapon;

        [Tooltip("一条动画内的近战判定区间；空数组表示本段无近战判定")]
        public HitPulse[] hitPulses;

        [Tooltip("相对本段动画 0 点。clip 为空则跳过")]
        public AttackSfxCue[] sfxCues;

        [Tooltip("相对本段动画 0 点出箭。空 = 本段不出箭。Bow_Air5 动画 4 箭插 4 条")]
        public ArrowSpawnCue[] arrowCues;

        [Tooltip("勾选后本段用下面的伤害和等级；不勾则用招默认值。段内某刀还可再覆盖")]
        public bool overrideCombat;
        public int baseDamage;
        public float postureDamage;
        public float knockback;
        public HitGrade hitGrade;

        [Tooltip("本段必须等 Animator 播完才结束（JumpThrust 起跳段）。StateDuration 仅作兜底上限。")]
        public bool waitAnimEnd;
    }

}
