using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 角色的武器模块：绑定 Hitbox、控制判定开关、生成箭矢。
    // Inspector 引用保留在 CharacterBody，当前使用的 Hitbox 由本类管理。
    // 近战命中窗口由攻击状态推进，实际扫描和上报由 Hitbox 完成。
    public class WeaponController
    {
        private readonly CharacterBody body;

        public WeaponController(CharacterBody body)
        {
            this.body = body;
            InitHitboxes();
        }

        // Weapon 是默认刀；ActiveHitbox 是当前攻击选中的刀、肘击或踢击判定框。
        public Hitbox Weapon { get; private set; }
        public Hitbox ActiveHitbox { get; private set; }

        // 为选中的 Hitbox 绑定本招配置并开启扫描；弓段等无近战窗口的配置直接跳过。
        public void EnableWeaponHit(AttackConfig config)
        {
            // 弓段 / 假红条：即使动画事件误调也不开刀。
            if (!AttackWindowSync.CanMeleeHit(config.hitPulses))
                return;
            Hitbox target = ResolveHitbox(config.HitboxSlot);

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
            target.Disable();
            ActiveHitbox = null;
            CombatEventBus.TriggerAttackSwingEnd(body);
        }

        // 时间轴 arrowCues 到点由 AttackState 调用。伤害读招式表该支出箭，不读烘焙 AttackConfig。
        public void SpawnArrow(int cueIndex = 0)
        {
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

            ArrowProjectile arrow = Object.Instantiate(body.arrowPrefab, origin, Quaternion.LookRotation(dir, Vector3.up));
            arrow.Fire(body, dir, body.arrowSpeed, body.arrowCastRadius, body.arrowTargetLayers, body.arrowLifetime,
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
                return hurtbox.GetComponent<Collider>().bounds.center;
            }

            return body.GetComponent<Collider>().bounds.center;
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

        // 优先使用手工指定的刀，未指定才搜索；肘击和踢击使用各自的显式引用。
        void InitHitboxes()
        {
            Weapon = body.weaponHitbox != null ? body.weaponHitbox : FindDefaultWeaponHitbox();
            InitHitbox(Weapon);
            InitHitbox(body.elbowHitbox);
            InitHitbox(body.kickHitbox);
        }

        // 这里只建立所属角色引用；开场不启用命中扫描，扫描要等攻击窗口开启。
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
                return body.elbowHitbox;

            if (slot == AttackHitboxSlot.Kick)
                return body.kickHitbox;

            return Weapon;
        }
    }

}
