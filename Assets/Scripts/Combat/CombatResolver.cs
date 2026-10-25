using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork;
using ARPG.FrameWork.Body;
using ARPG.FrameWork.States;
using ARPG.Mgr;
namespace ARPG.Combat
{
    // 按 Hitbox / Hurtbox 所属角色转发命中，不依赖玩家或 Boss 的场景引用。
    public class CombatResolver : MonoBehaviour
    {
        public static CombatResolver Instance { get; private set; }

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
            CharacterBody attacker = hitbox.Owner;
            CharacterBody target = hurtbox.Owner;

            // 排除打到自己（双保险，Hitbox 侧已过滤）
            if (attacker == null || target == null || attacker == target) return;

            // 近战伤害与受击参数读当前 AttackConfig，防御和扣血由目标 ReceiveHit 继续处理。
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
    }
}
