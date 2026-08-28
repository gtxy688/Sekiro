using UnityEngine;

// 箭：匀速直线 + 上一帧→当前帧 SphereCast。不挂 Hitbox / Collider。
public class ArrowProjectile : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;
    [Tooltip("模型本地轴与飞行 +Z 的偏差。弦一郎箭模沿 +X，默认绕 Y -90°")]
    [SerializeField] private Vector3 visualLocalEuler = new Vector3(0f, -90f, 0f);
    [SerializeField] private float visualScale = 1.35f;

    private CharacterBody owner;
    private Vector3 direction;
    private float speed;
    private float castRadius;
    private LayerMask targetLayers;
    private float lifeRemaining;
    private int healthDmg;
    private float postureDmg;
    private float knockback;
    private HitGrade grade;
    private readonly Collider[] overlapBuf = new Collider[16];
    private Vector3 lastPos;
    private bool spent;

    void Awake()
    {
        if (visualRoot == null && transform.childCount > 0)
            visualRoot = transform.GetChild(0);
        ApplyVisualPose();
    }

    void ApplyVisualPose()
    {
        if (visualRoot == null) return;
        visualRoot.localRotation = Quaternion.Euler(visualLocalEuler);
        visualRoot.localScale = Vector3.one * visualScale;
    }

    public void Fire(
        CharacterBody owner,
        Vector3 direction,
        float speed,
        float castRadius,
        LayerMask targetLayers,
        float lifetime,
        int healthDmg,
        float postureDmg,
        float knockback,
        HitGrade grade)
    {
        this.owner = owner;
        this.direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.forward;
        this.speed = speed;
        this.castRadius = Mathf.Max(0.01f, castRadius);
        this.targetLayers = targetLayers;
        lifeRemaining = Mathf.Max(0.05f, lifetime);
        this.healthDmg = healthDmg;
        this.postureDmg = postureDmg;
        this.knockback = knockback;
        this.grade = grade;
        ApplyVisualPose();
        transform.rotation = Quaternion.LookRotation(this.direction, Vector3.up);
        lastPos = transform.position;
        spent = false;
    }

    private void LateUpdate()
    {
        if (spent) return;

        lifeRemaining -= Time.deltaTime;
        if (lifeRemaining <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 current = lastPos + direction * speed * Time.deltaTime;
        transform.position = current;
        transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

        Physics.SyncTransforms();
        Vector3 delta = current - lastPos;
        float distance = delta.magnitude;
        if (distance > 0.0001f)
        {
            RaycastHit[] hits = Physics.SphereCastAll(
                lastPos, castRadius, delta / distance, distance, targetLayers,
                QueryTriggerInteraction.Collide);
            for (int i = 0; i < hits.Length; i++)
            {
                if (TryReport(hits[i].collider, hits[i].point))
                    return;
            }
        }

        int overlap = Physics.OverlapSphereNonAlloc(
            current, castRadius, overlapBuf, targetLayers, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlap; i++)
        {
            if (TryReport(overlapBuf[i], current))
                return;
        }

        lastPos = current;
    }

    private bool TryReport(Collider col, Vector3 hitPoint)
    {
        if (col == null) return false;
        Hurtbox hurtbox = col.GetComponentInParent<Hurtbox>();
        if (hurtbox == null || hurtbox.Owner == null || hurtbox.Owner == owner)
            return false;

        spent = true;
        CombatManager.Instance?.ReportProjectileHit(
            owner, hurtbox, hitPoint, healthDmg, postureDmg, knockback, grade);
        Destroy(gameObject);
        return true;
    }
}
