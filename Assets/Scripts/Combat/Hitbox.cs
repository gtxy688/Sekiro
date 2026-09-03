using System.Collections.Generic;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 武器 Hitbox（挂在武器骨骼/剑刃空物体上）
    // 框架红线：不用 OnTrigger。由 AttackState 开关 + 每帧 SphereCast/Overlap 扫描
    // （武器上一帧位置→当前帧位置，防止高速挥砍穿透）。
    // 伤害数据全部来自 AttackConfig（SO），这里不硬编码任何数值。
    public class Hitbox : MonoBehaviour
    {
        [Header("判定参数")]
        public float castRadius = 0.1f;   // 扫描球半径（大约等于武器"粗细"）
        [Tooltip("柄到尖的刀身半径。贴身时刀尖在人外侧，只扫尖会漏")]
        public float shaftRadius = 0.3f;
        public LayerMask targetLayers;    // 能打到的层：对方 Hurtbox / 对方 Hitbox（拼刀）

        private CharacterBody owner;
        private AttackConfig config;          // 当前招式的伤害配置
        private Vector3 lastCastPos;          // 上一帧的武器位置（扫描起点）
        private bool isActive;                // 由 AttackState Enable/Disable

        // 已命中记录：防止一次挥砍对同一目标多次结算
        private readonly HashSet<CharacterBody> hitTargets = new HashSet<CharacterBody>();
        // 拼刀去重：同一判定窗内对同一 Hitbox 只结算一次；且只让 InstanceID 较小方上报，避免双方各报一次
        private readonly HashSet<Hitbox> clashedPartners = new HashSet<Hitbox>();
        // 开判定时已经叠在刀上的目标：先不算，等离开再扫进来才算新的一刀（连招第二刀）
        private readonly HashSet<CharacterBody> blockedUntilExit = new HashSet<CharacterBody>();
        private readonly HashSet<CharacterBody> overlappingThisFrame = new HashSet<CharacterBody>();
        private readonly Collider[] overlapBuf = new Collider[16];

        public CharacterBody Owner => owner;
        public AttackConfig Config => config;

        private void Awake()
        {
            // 容错：没手动 Initialize 也能在挂到角色子物体时自动找到主人
            if (owner == null)
                owner = GetComponentInParent<CharacterBody>();
        }

        public void Initialize(CharacterBody owner)
        {
            this.owner = owner;
        }

        // 每次攻击前由 AttackState / 动画事件调用：绑定本招式的伤害配置
        public void SetConfig(AttackConfig config)
        {
            this.config = config;
            hitTargets.Clear();
        }

        public void Enable()
        {
            if (owner == null || config == null) return;
            // 弓段 / 0.01 假窗：Clip 上残留的 Enable 事件也不能扫刀。
            if (!AttackWindowSync.CanMeleeHit(config.hitPulses))
                return;
            isActive = true;
            Physics.SyncTransforms();
            lastCastPos = transform.position;
            hitTargets.Clear();
            blockedUntilExit.Clear();
            clashedPartners.Clear();
            // 判定改到 HitStartTime 后才开，此时已经叠在刀上就是这一刀该中的，不再先屏蔽。
        }

        public void Disable()
        {
            isActive = false;
            config = null;
            hitTargets.Clear();
            blockedUntilExit.Clear();
            clashedPartners.Clear();
        }

        // 动画在 Update 写骨头，受击胶囊在物理步进才同步。LateUpdate 里先 Sync 再扫。
        private void LateUpdate()
        {
            if (!isActive || owner == null || config == null) return;

            Physics.SyncTransforms();

            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - lastCastPos;
            float distance = delta.magnitude;

            // 武器有位移 → 扫一段，避免高速挥砍穿透
            if (distance > 0.0001f)
            {
                Vector3 direction = delta / distance;
                RaycastHit[] hits = Physics.SphereCastAll(
                    lastCastPos, castRadius, direction, distance, targetLayers,
                    QueryTriggerInteraction.Collide);
                ProcessHits(hits);
            }

            // 每帧点检：SphereCast 从碰撞体内部出发会漏，连招时刀常还叠在目标里
            overlappingThisFrame.Clear();
            FillOverlapping(overlappingThisFrame);
            foreach (CharacterBody target in overlappingThisFrame)
            {
                TryHit(target, currentPos);
            }

            blockedUntilExit.RemoveWhere(body => !overlappingThisFrame.Contains(body));

            lastCastPos = currentPos;
        }

        private void FillOverlapping(HashSet<CharacterBody> dest)
        {
            AddOverlapping(dest, Physics.OverlapSphereNonAlloc(
                transform.position, castRadius, overlapBuf, targetLayers,
                QueryTriggerInteraction.Collide));

            // 贴身站在挥砍内侧时，刀尖会从身侧刮过，必须连柄到尖一起扫
            Vector3 tip = transform.position;
            Vector3 hilt = transform.parent != null ? transform.parent.position : tip;
            float shaft = Mathf.Max(castRadius, shaftRadius);
            if ((tip - hilt).sqrMagnitude > 0.0001f)
            {
                AddOverlapping(dest, Physics.OverlapCapsuleNonAlloc(
                    hilt, tip, shaft, overlapBuf, targetLayers,
                    QueryTriggerInteraction.Collide));
            }
        }

        private void AddOverlapping(HashSet<CharacterBody> dest, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuf[i];
                if (col == null) continue;
                Hurtbox hurtbox = col.GetComponentInParent<Hurtbox>();
                if (hurtbox != null && hurtbox.Owner != null && hurtbox.Owner != owner)
                    dest.Add(hurtbox.Owner);
            }
        }

        private void ProcessHits(RaycastHit[] hits)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                ProcessCollider(hits[i].collider, hits[i].point);
            }
        }

        private void ProcessCollider(Collider col, Vector3 hitPoint)
        {
            Hurtbox hurtbox = col.GetComponentInParent<Hurtbox>();
            if (hurtbox != null && hurtbox.Owner != owner)
            {
                TryHit(hurtbox.Owner, hitPoint);
                return;
            }

            Hitbox other = col.GetComponentInParent<Hitbox>();
            if (other != null && other != this && other.Owner != owner)
                TryClash(other, hitPoint);
        }

        private void TryClash(Hitbox other, Vector3 hitPoint)
        {
            if (other == null || other.Owner == owner) return;
            if (!clashedPartners.Add(other)) return;
            // 双方 Hitbox 都会扫到对方；只让 InstanceID 较小的一方上报，避免一帧内架势双倍结算
            if (GetInstanceID() > other.GetInstanceID()) return;
            CombatManager.Instance?.ReportClash(this, other, hitPoint);
        }

        private void TryHit(CharacterBody target, Vector3 hitPoint)
        {
            if (target == null || target == owner) return;
            if (blockedUntilExit.Contains(target)) return;
            if (!hitTargets.Add(target)) return;

            Hurtbox hurtbox = target.GetComponentInChildren<Hurtbox>();
            if (hurtbox == null) hurtbox = target.GetComponent<Hurtbox>();
            if (hurtbox == null) return;

            CombatManager.Instance?.ReportHit(this, hurtbox, hitPoint);
        }
    }

}
