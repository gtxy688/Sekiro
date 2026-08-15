using System.Collections.Generic;
using UnityEngine;

// 武器 Hitbox（挂在武器骨骼/剑刃空物体上）
// 框架红线：不用 OnTrigger。用动画事件开启/关闭判定 + 每帧 SphereCast 扫描
// （武器上一帧位置 → 当前帧位置），防止高速挥砍穿透。
// 伤害数据全部来自 AttackConfig（SO），这里不硬编码任何数值。
public class Hitbox : MonoBehaviour
{
    [Header("判定参数")]
    public float castRadius = 0.1f;   // 扫描球半径（大约等于武器"粗细"）
    public LayerMask targetLayers;    // 能打到的层：对方 Hurtbox / 对方 Hitbox（拼刀）

    private CharacterBody owner;
    private AttackConfig config;          // 当前招式的伤害配置
    private Vector3 lastCastPos;          // 上一帧的武器位置（扫描起点）
    private bool isActive;                // 由动画事件 Enable/Disable

    // 已命中记录：防止一次挥砍对同一目标多次结算
    private readonly HashSet<CharacterBody> hitTargets = new HashSet<CharacterBody>();

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

    // 动画事件（M8）：开启判定，并记录起点位置
    public void Enable()
    {
        if (owner == null) return;
        isActive = true;
        lastCastPos = transform.position;
        hitTargets.Clear();
    }

    // 动画事件（M8）：关闭判定
    public void Disable()
    {
        isActive = false;
        hitTargets.Clear();
    }

    private void FixedUpdate()
    {
        if (!isActive || owner == null) return;

        Vector3 currentPos = transform.position;
        Vector3 delta = currentPos - lastCastPos;
        float distance = delta.magnitude;

        // 武器有位移 → 用"上一帧→当前帧"一段扫描，避免高速挥砍穿透
        if (distance > 0.0001f)
        {
            Vector3 direction = delta / distance;
            RaycastHit[] hits = Physics.SphereCastAll(lastCastPos, castRadius, direction, distance, targetLayers);
            ProcessHits(hits);
        }

        // 武器没动（例如站桩挥砍）→ 也扫当前点一次，避免漏判
        if (distance <= 0.01f)
        {
            Collider[] colliders = Physics.OverlapSphere(currentPos, castRadius, targetLayers);
            if (colliders != null && colliders.Length > 0)
            {
                for (int i = 0; i < colliders.Length; i++)
                {
                    // 位置扫描拿不到命中点，用球心作为近似命中点
                    ProcessCollider(colliders[i], currentPos);
                }
            }
        }

        lastCastPos = currentPos;
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
        // 1. 扫到对方 Hurtbox → 报告 CombatManager 结算伤害
        Hurtbox hurtbox = col.GetComponentInParent<Hurtbox>();
        if (hurtbox != null && hurtbox.Owner != owner)
        {
            if (hitTargets.Add(hurtbox.Owner)) // 去重：同一目标一次挥砍只结算一次
            {
                CombatManager.Instance?.ReportHit(this, hurtbox, hitPoint);
            }
            return;
        }

        // 2. 扫到对方 Hitbox → 双方武器相交 → 拼刀
        Hitbox other = col.GetComponentInParent<Hitbox>();
        if (other != null && other != this && other.Owner != owner)
        {
            CombatManager.Instance?.ReportClash(this, other, hitPoint);
        }
    }
}
