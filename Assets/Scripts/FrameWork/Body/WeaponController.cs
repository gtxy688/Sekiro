using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 武器模块（CharacterBody 重构）：Hitbox 槽位解析 + 武器判定开关 + 弓箭发射。
    // 约束：
    // - Inspector 字段（weaponHitbox/arrowPrefab 等 10 个）留在 CharacterBody（prefab 已保存），模块只读；
    // - Weapon/ActiveHitbox 运行时状态收在本类内部，Façade 转发；
    // - 伤害数值全部来自 AttackConfig / 招式表（SO），本类不硬编码任何数值；
    // - 判定开启仍受 AttackWindowSync 总闸约束（弓段 / 0.01s 假窗不开刀）。
    public class WeaponController
    {
        private readonly CharacterBody body;

        public WeaponController(CharacterBody body)
        {
            this.body = body;
            InitHitboxes();
        }

        // 武器的碰撞盒（M3：BoxCast 版 Hitbox，不再用 OnTrigger）
        public Hitbox Weapon { get; private set; }
        public Hitbox ActiveHitbox { get; private set; }

        // 开启武器判定（M3/M8）：攻击状态/动画事件调用。绑定本招式的伤害配置
        public void EnableWeaponHit(AttackConfig config)
        {
            if (config == null) return;
            // 弓段 / 假红条：即使动画事件误调也不开刀。
            if (!AttackWindowSync.CanMeleeHit(config.HitStartTime, config.RecoveryWindowStart, config.hitPulses))
                return;
            Hitbox target = ResolveHitbox(config.HitboxSlot);
            if (target == null) return;

            if (ActiveHitbox != null && ActiveHitbox != target)
                ActiveHitbox.Disable();

            target.SetConfig(config);
            target.Enable();
            ActiveHitbox = target;
            CombatEventBus.TriggerAttackSwingStart(body);
        }

        // 关闭武器判定（M3/M8）
        public void DisableWeaponHit()
        {
            Hitbox target = ActiveHitbox != null ? ActiveHitbox : Weapon;
            if (target == null) return;
            target.Disable();
            ActiveHitbox = null;
            CombatEventBus.TriggerAttackSwingEnd(body);
        }

        // 时间轴 arrowCues 到点由 AttackState 调用。伤害读招式表该支出箭，不读烘焙 AttackConfig。
        public void SpawnArrow(int cueIndex = 0)
        {
            if (body.arrowPrefab == null || body.arrowSpawn == null)
            {
                Debug.LogWarning($"{body.name} 缺少 arrowPrefab 或 arrowSpawn，不出箭。");
                return;
            }

            if (body.CurrentMoveEntry == null)
            {
                Debug.LogWarning($"{body.name} SpawnArrow 时没有当前招式表行。");
                return;
            }

            ArrowSpawnCue cue = null;
            if (body.CurrentMoveWindow != null && body.CurrentMoveWindow.arrowCues != null
                && cueIndex >= 0 && cueIndex < body.CurrentMoveWindow.arrowCues.Length)
                cue = body.CurrentMoveWindow.arrowCues[cueIndex];

            AttackCombatResolve.Resolve(
                body.CurrentMoveEntry, body.CurrentMoveWindow, cue,
                out int damage, out float posture, out float knockback, out HitGrade grade);

            Vector3 origin = body.arrowSpawn.position;
            Vector3 aim = ResolveProjectileAimPoint();
            Vector3 dir = aim - origin;
            if (dir.sqrMagnitude < 0.0001f)
                dir = body.arrowSpawn.forward.sqrMagnitude > 0.0001f ? body.arrowSpawn.forward : body.transform.forward;
            dir.Normalize();

            // 出射点沿瞄准方向略前移，避免从弓身侧面穿出；方向以瞄准为准。
            origin += dir * 0.35f;

            LayerMask layers = body.arrowTargetLayers;
            if (layers == 0 && Weapon != null)
                layers = Weapon.targetLayers;
            if (layers == 0)
                Debug.LogWarning($"{body.name} arrowTargetLayers 未设，箭扫不到人。");

            ArrowProjectile arrow = Object.Instantiate(body.arrowPrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
            arrow.Fire(body, dir, body.arrowSpeed, body.arrowCastRadius, layers, body.arrowLifetime,
                damage, posture, knockback, grade);
            CombatEventBus.TriggerArrowReleased(body);
        }

        public Vector3 GetProjectileAimPoint()
        {
            if (body.projectileAimPoint != null)
                return body.projectileAimPoint.position;

            // Hurtbox 常挂在根上，transform.position 是脚底——箭会朝地飞。
            // 优先用碰撞体中心（胸口附近），再退到根上方。
            Hurtbox hurtbox = body.GetComponentInChildren<Hurtbox>();
            if (hurtbox != null)
            {
                Collider col = hurtbox.GetComponent<Collider>();
                if (col == null)
                    col = hurtbox.GetComponentInChildren<Collider>();
                if (col != null)
                    return col.bounds.center;
            }

            Collider bodyCol = body.GetComponent<Collider>();
            if (bodyCol != null)
                return bodyCol.bounds.center;

            return body.transform.position + Vector3.up * 1.2f;
        }

        Vector3 ResolveProjectileAimPoint()
        {
            if (body.CombatTarget == null)
                return body.transform.position + body.transform.forward * 8f + Vector3.up * 1.2f;

            CharacterBody targetBody = body.CombatTarget.GetComponent<CharacterBody>();
            if (targetBody == null)
                targetBody = body.CombatTarget.GetComponentInParent<CharacterBody>();
            if (targetBody != null)
                return targetBody.GetProjectileAimPoint();

            return body.CombatTarget.position + Vector3.up * 1.2f;
        }

        void InitHitboxes()
        {
            Weapon = body.weaponHitbox != null ? body.weaponHitbox : FindDefaultWeaponHitbox();
            InitHitbox(Weapon);
            InitHitbox(body.elbowHitbox);
            InitHitbox(body.kickHitbox);
        }

        void InitHitbox(Hitbox hitbox)
        {
            if (hitbox != null)
                hitbox.Initialize(body);
        }

        Hitbox FindDefaultWeaponHitbox()
        {
            Hitbox[] all = body.GetComponentsInChildren<Hitbox>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Hitbox h = all[i];
                if (h == null || h == body.elbowHitbox || h == body.kickHitbox)
                    continue;
                return h;
            }
            return null;
        }

        Hitbox ResolveHitbox(AttackHitboxSlot slot)
        {
            if (slot == AttackHitboxSlot.Elbow)
            {
                if (body.elbowHitbox != null)
                    return body.elbowHitbox;
                Debug.LogWarning($"{body.name} 未指定 Elbow Hitbox，回退到刀");
                return Weapon;
            }

            if (slot == AttackHitboxSlot.Kick)
            {
                if (body.kickHitbox != null)
                    return body.kickHitbox;
                Debug.LogWarning($"{body.name} 未指定 Kick Hitbox，回退到刀");
                return Weapon;
            }

            return Weapon;
        }
    }

}
