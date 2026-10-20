using UnityEngine;

using ARPG.Combat;
using ARPG.Configs;
using ARPG.Mgr;
namespace ARPG.FrameWork.Body
{

    // 绑定武器、肘击与踢击 Hitbox，按攻击状态请求开关判定，并按出箭 cue 生成箭矢。
    // Inspector 引用读取 CharacterBody，实际近战扫描和上报由 Hitbox 完成。
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

            // 刀、肘和脚不能残留同时判定；切槽时先关上一槽，再把本招配置交给新槽。
            if (ActiveHitbox != null && ActiveHitbox != target)
                ActiveHitbox.Disable();

            target.SetConfig(config);
            target.Enable();
            ActiveHitbox = target;
            CombatEventBus.TriggerAttackSwingStart(body);
        }

        // 关闭攻击扫描并通知表现层收尾；不是销毁或隐藏武器模型。
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
            // 生成后把飞行方向、碰撞层和伤害交给箭自身，此模块不逐帧推进箭的运动。
            arrow.Fire(body, dir, body.arrowSpeed, body.arrowCastRadius, body.arrowTargetLayers, body.arrowLifetime,
                damage, posture, knockback, grade);
            CombatEventBus.TriggerArrowReleased(body);
        }

        public Vector3 GetProjectileAimPoint()
        {
            // 返回“本角色被别人射击时”的瞄准点；出箭时再向目标角色查询这个入口。
            if (body.projectileAimPoint != null)
                return body.projectileAimPoint.position;

            // 未指定瞄准点时优先取 Hurtbox 碰撞体中心，其次取角色碰撞体中心。
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
