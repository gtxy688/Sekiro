using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 无状态命中结算。从 CombatManager 里分出来的那一半——也是原本就写对的那一半。
    //
    // 判断依据：这三个 Report 方法里，attacker 与 target 全部取自 hitbox/hurtbox.Owner，
    // 从头到尾没有一次问过"谁是玩家、谁是 Boss"。这正是中间层该有的样子，
    // 所以多 Boss / 复战对它没有任何影响，一行都不用改。
    //
    // 明确不做：不触发顿帧。顿帧改的是全局 Time.timeScale，属于时间表现层，
    // 结算器不该有这个权力（详见 CombatManager.HitStop 上的注释）。
    public class CombatResolver : MonoBehaviour
    {
        public static CombatResolver Instance { get; private set; }

        [Header("拼刀参数")]
        [Tooltip("拼刀时双方架势增长系数（默认 1 = 按对方招式 PostureDamage 全额涨）")]
        public float clashPostureMultiplier = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"场景中有多个 CombatResolver（{Instance.name} 与 {name}），已销毁后者。", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // A 的武器扫到 B 的 Hurtbox
        public void ReportHit(Hitbox hitbox, Hurtbox hurtbox, Vector3 hitPoint)
        {
            if (hitbox == null || hurtbox == null) return;

            CharacterBody attacker = hitbox.Owner;
            CharacterBody target = hurtbox.Owner;

            // 排除打到自己（双保险，Hitbox 侧已过滤）
            if (attacker == null || target == null || attacker == target) return;

            // 全局规则扩展位（后续：减伤 Buff、全场无敌、友军伤害开关等）

            // 伤害数据来自 AttackConfig（SO），这里只做转发（含危字标记 M17 / 击退 / 受击等级）
            if (hitbox.Config == null) return;
            target.ReceiveHit(attacker, hitbox.Config.BaseDamage, hitbox.Config.PostureDamage, hitPoint,
                              hitbox.Config.Perilous != PerilousType.None, hitbox.Config.Perilous,
                              hitbox.Config.Knockback, hitbox.Config.HitGrade, false);
        }

        // 箭扫到 Hurtbox。伤害由调用方从招式表解析，不读 Hitbox.Config。
        public void ReportProjectileHit(
            CharacterBody attacker,
            Hurtbox hurtbox,
            Vector3 hitPoint,
            int healthDmg,
            float postureDmg,
            float knockback,
            HitGrade hitGrade)
        {
            CharacterBody target = hurtbox != null ? hurtbox.Owner : null;
            if (attacker == null || target == null || attacker == target) return;

            target.ReceiveHit(attacker, healthDmg, postureDmg, hitPoint,
                false, PerilousType.None, knockback, hitGrade, true);
        }

        // 双方 Hitbox 相交 → 拼刀：只狼里拼刀双方都涨架势，不打伤害
        public void ReportClash(Hitbox a, Hitbox b, Vector3 point)
        {
            if (a == null || b == null) return;
            if (a.Owner == null || b.Owner == null || a.Owner == b.Owner) return;

            // 各按对方招式的架势伤害涨架势（乘以拼刀系数）
            if (b.Config != null)
                a.Owner.AccumulatePosture(b.Config.PostureDamage * clashPostureMultiplier);
            if (a.Config != null)
                b.Owner.AccumulatePosture(a.Config.PostureDamage * clashPostureMultiplier);

            // 表现层事件：打铁音效/火花（M13/M15 订阅）
            CombatEventBus.TriggerWeaponDeflected(
                CombatFxPoint.BetweenHitboxes(a, b, point), DeflectType.Normal);
        }
    }
}
