using UnityEngine;

using ARPG.Configs;
using ARPG.FrameWork.Body;
namespace ARPG.Combat
{

    // 匀速飞行，用 SphereCast 和落点重叠检测上报命中，不需要 Hitbox 或 Collider。
    // 命中、寿命结束或复战复位时自行销毁，避免旧箭影响下一场。
    public class ArrowProjectile : MonoBehaviour, ICombatResettable
    {
        [SerializeField] private Transform visualRoot;
        [Tooltip("模型本地轴与飞行 +Z 的偏差。弦一郎箭模沿 +X，默认绕 Y -90°")]
        [SerializeField] private Vector3 visualLocalEuler = new Vector3(0f, -90f, 0f);
        [SerializeField] private float visualScale = 2.4f;

        [Header("可见性")]
        [SerializeField] private bool enableTrail = true;
        [SerializeField] private float trailTime = 0.22f;
        [SerializeField] private float trailStartWidth = 0.12f;
        [SerializeField] private float trailEndWidth = 0.02f;
        [SerializeField] private Color trailColor = new Color(1.4f, 1.1f, 0.55f, 1f);

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
        private TrailRenderer trail;
        private Material trailMat;

        void Awake()
        {
            if (visualRoot == null)
                visualRoot = transform.GetChild(0);
            ApplyVisualPose();
            EnsureTrail();
        }

        // 注册复位能力；Ensure 兼容箭先于其他注册方初始化的情况。
        void Start()
        {
            EncounterScope.Ensure().Register(this);
        }

        void OnDestroy()
        {
            if (trailMat != null)
                Destroy(trailMat);

            EncounterScope.Current?.Unregister(this);
        }

        // 复战时清除上一场发射的箭。
        public void ResetForEncounter()
        {
            Destroy(gameObject);
        }

        void ApplyVisualPose()
        {
            visualRoot.localRotation = Quaternion.Euler(visualLocalEuler);
            visualRoot.localScale = Vector3.one * visualScale;
        }

        // 配置拖尾并创建本箭独占的材质，销毁时一并释放。
        void EnsureTrail()
        {
            if (!enableTrail) return;

            trail = GetComponent<TrailRenderer>();
            if (trail == null)
                trail = gameObject.AddComponent<TrailRenderer>();

            trail.time = trailTime;
            trail.minVertexDistance = 0.04f;
            trail.numCapVertices = 2;
            trail.numCornerVertices = 2;
            trail.alignment = LineAlignment.View;
            trail.textureMode = LineTextureMode.Stretch;
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
            trail.widthMultiplier = 1f;
            trail.widthCurve = new AnimationCurve(
                new Keyframe(0f, trailStartWidth),
                new Keyframe(1f, trailEndWidth));

            Gradient g = new Gradient();
            Color c = trailColor;
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(c, 0f),
                    new GradientColorKey(c, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f),
                });
            trail.colorGradient = g;

            Shader shader = Shader.Find("ARPG/FX/AdditiveSpark");
            if (trailMat != null)
                Destroy(trailMat);
            trailMat = new Material(shader);
            trailMat.SetColor("_Color", c);
            trail.material = trailMat;

            trail.emitting = true;
            trail.Clear();
        }

        // 设置发射方向、查询参数与伤害快照，同时清除旧拖尾和命中状态。
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
            EnsureTrail();
            transform.rotation = Quaternion.LookRotation(this.direction, Vector3.up);
            lastPos = transform.position;
            spent = false;
            if (trail != null)
            {
                trail.Clear();
                trail.emitting = true;
            }
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

            // 先同步动画驱动的受击盒，再扫描本帧飞行轨迹。
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

            // 补查终点重叠，覆盖 SphereCast 从碰撞体内部出发的漏检。
            int overlap = Physics.OverlapSphereNonAlloc(
                current, castRadius, overlapBuf, targetLayers, QueryTriggerInteraction.Collide);
            for (int i = 0; i < overlap; i++)
            {
                if (TryReport(overlapBuf[i], current))
                    return;
            }

            lastPos = current;
        }

        // 只消费首个有效角色命中，结算统一交给 CombatManager。
        private bool TryReport(Collider col, Vector3 hitPoint)
        {
            if (col == null) return false;
            Hurtbox hurtbox = col.GetComponentInParent<Hurtbox>();
            if (hurtbox == null || hurtbox.Owner == null || hurtbox.Owner == owner)
                return false;

            spent = true;
            if (trail != null)
                trail.emitting = false;
            CombatManager.Instance.ReportProjectileHit(
                owner, hurtbox, hitPoint, healthDmg, postureDmg, knockback, grade);
            Destroy(gameObject);
            return true;
        }
    }

}
