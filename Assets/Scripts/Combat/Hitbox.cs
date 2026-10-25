using System.Collections.Generic;
using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 由攻击状态开关，用 SphereCast / Overlap 查询近战命中与拼刀，统一交给 CombatManager。
    // 当前伤害参数读取绑定的 AttackConfig，同一判定窗内对角色和拼刀对象分别去重。
    public class Hitbox : MonoBehaviour
    {
        [Header("判定参数")]
        public float castRadius = 0.1f;   // 扫描球半径（大约等于武器"粗细"）
        [Tooltip("柄到尖的刀身半径。贴身时刀尖在人外侧，只扫尖会漏")]
        public float shaftRadius = 0.3f;
        public LayerMask targetLayers;    // 能打到的层：对方 Hurtbox

        private CharacterBody owner;
        private AttackConfig config;          // 当前招式的伤害配置
        private Vector3 lastCastPos;          // 上一帧的武器位置（扫描起点）
        private bool isActive;                // 由 AttackState Enable/Disable

        // 已命中记录：防止一次挥砍对同一目标多次结算
        private readonly HashSet<CharacterBody> hitTargets = new HashSet<CharacterBody>();
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

        // 攻击状态为每个有效脉冲绑定伤害配置，并清除上一刀的命中记录。
        public void SetConfig(AttackConfig config)
        {
            this.config = config;
            hitTargets.Clear();
        }

        public void Enable()
        {
            // 无有效近战窗口的弓段或占位脉冲不启用扫描。
            if (!AttackWindowSync.CanMeleeHit(config.hitPulses))
                return;
            isActive = true;
            Physics.SyncTransforms();
            lastCastPos = transform.position;
            hitTargets.Clear();
            blockedUntilExit.Clear();
            // 有效脉冲开启时已经重叠的目标，也在本窗口内判定命中。
        }

        public void Disable()
        {
            isActive = false;
            config = null;
            hitTargets.Clear();
            blockedUntilExit.Clear();
        }

        // 动画在 Update 写骨头，受击胶囊在物理步进才同步。LateUpdate 里先 Sync 再扫。
        private void LateUpdate()
        {
            if (!isActive) return;

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
            }
        }

        private void TryHit(CharacterBody target, Vector3 hitPoint)
        {
            if (target == null || target == owner) return;
            if (blockedUntilExit.Contains(target)) return;
            if (!hitTargets.Add(target)) return;

            Hurtbox hurtbox = target.GetComponentInChildren<Hurtbox>();
            CombatManager.Instance.ReportHit(this, hurtbox, hitPoint);
        }
    }

}
