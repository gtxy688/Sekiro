using DG.Tweening;
using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.UI
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif

    // 世界空间汉字提示（危 / 治 / 回生）的公共实现。
    //
    // 为什么收：这三个原来各写一份、逐行相同，只差染色与触发时机。
    // 复制品的问题不在于今天多占 300 行，而在于修一个 bug 要记得改三处——
    // 上次给 tween 补 SetLink / DOKill 时就漏了一处，这类遗漏一定会重演。
    //
    // 绘制：直接画 png（白字黑底，把亮度当透明），透明混合，不走加法 Shader
    // （加法会把整块 Quad 烧成一个亮方块）。材质 ZTest Always。
    //
    // 序列化字段为什么放在基类而不是留在子类：子类只留一个 DefaultTint 就够了，
    // 否则每个子类都要重复声明 followTarget / headOffset / glyphRenderer / showDuration。
    // 字段在继承链上平移，Unity 按字段名反序列化，值不会丢；
    // 万一真丢了，ResolvedTint 还有一按 default 兜底的路，不会把汉字显示成白色。
    [ExecuteAlways]
    public abstract class WorldGlyphView : UIView, IFollowTargetView
    {
        protected const float PopDuration = 0.12f;
        protected const float FadeDuration = 0.15f;

        [SerializeField] protected Transform followTarget;
        [SerializeField] protected float headOffset = -0.4f;
        [SerializeField] protected MeshRenderer glyphRenderer;
        [SerializeField] protected Color tint;
        [SerializeField] protected float showDuration = 0.8f;

        // 序列化值缺失时（字段迁移未生效、新建物体未填色）用它兜底，
        // 保证汉字永远看得见，只是颜色可能对——比显示成纯白好排查。
        protected abstract Color DefaultTint { get; }

        private Color ResolvedTint => tint == default ? DefaultTint : tint;

        private float alpha;
        private Tweener alphaTween;
        // 必须写全名：全局命名空间已有行为树 Sequence，会盖住 using DG.Tweening
        private DG.Tweening.Sequence showSeq;
        private MaterialPropertyBlock block;

        public override void OnViewInit()
        {
            BindRefs();
            ApplyAlpha(0f);
            Hide();
        }

        // 连战切场：汉字可能正播到一半（tween 挂在 transform 上），
        // 不 Kill 掉的话新一场开局会看见上一场的字飘在头顶淡出。
        public override void ResetForEncounter()
        {
            KillTweens();
            ApplyAlpha(0f);
            Hide();
        }

        private void OnEnable()
        {
            BindRefs();
            // 编辑态把字摊开，方便摆位置；运行时由事件触发
            if (!Application.isPlaying)
            {
                transform.localScale = Vector3.one;
                ApplyAlpha(1f);
            }
        }

        // target 为空时不改，避免一次误调用把已绑好的目标清掉
        public void BindFollowTarget(CharacterBody target)
        {
            if (target == null) return;
            Transform found = FindDeep(target.transform, "Head")
                ?? FindDeep(target.transform, "Spine1")
                ?? FindDeep(target.transform, "Spine");
            followTarget = found != null ? found : target.transform;
        }

        // 子类各自的触发入口（ShowWarning / ShowHeal / ShowRevive）只管调它
        protected void PlayGlyph()
        {
            BindRefs();
            if (glyphRenderer == null)
                return;

            KillTweens();
            Show();

            transform.localScale = Vector3.zero;
            ApplyAlpha(0f);

            showSeq = DOTween.Sequence();
            showSeq.Append(transform.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutBack));
            showSeq.Join(TweenAlpha(1f, 0.08f));
            float hold = Mathf.Max(0f, showDuration - PopDuration - FadeDuration);
            showSeq.AppendInterval(hold);
            showSeq.Append(transform.DOScale(Vector3.one * 0.8f, FadeDuration).SetEase(Ease.InQuad));
            showSeq.Join(TweenAlpha(0f, FadeDuration));
            showSeq.OnComplete(Hide);
        }

        private void LateUpdate()
        {
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
                return;

            BindRefs();
            FollowWorld();
        }

        private void OnDisable()
        {
            KillTweens();
        }

        private void FollowWorld()
        {
            Vector3 pos = followTarget != null
                ? followTarget.position + Vector3.up * headOffset
                : transform.position;

            UnityEngine.Camera cam = UnityEngine.Camera.main;
    #if UNITY_EDITOR
            if (!Application.isPlaying && SceneView.lastActiveSceneView != null
                && SceneView.lastActiveSceneView.camera != null)
                cam = SceneView.lastActiveSceneView.camera;
    #endif
            if (cam == null) return;

            Vector3 to = pos - cam.transform.position;
            bool behind = Vector3.Dot(cam.transform.forward, to) <= 0f;
            if (glyphRenderer != null) glyphRenderer.enabled = !behind;
            if (behind) return;

            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
        }

        private Tweener TweenAlpha(float to, float duration)
        {
            alphaTween = DOTween.To(() => alpha, v => ApplyAlpha(v), to, duration);
            return alphaTween;
        }

        private void ApplyAlpha(float value)
        {
            alpha = value;
            if (glyphRenderer == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = ResolvedTint;
            c.a = ResolvedTint.a * alpha;
            block.SetColor("_Color", c);
            glyphRenderer.SetPropertyBlock(block);
        }

        private void KillTweens()
        {
            if (showSeq != null && showSeq.IsActive()) showSeq.Kill();
            showSeq = null;
            if (alphaTween != null && alphaTween.IsActive()) alphaTween.Kill();
            alphaTween = null;
            transform.DOKill();
        }

        private void BindRefs()
        {
            if (glyphRenderer != null) return;

            Transform core = transform.Find("Core");
            if (core != null) glyphRenderer = core.GetComponent<MeshRenderer>();
            if (glyphRenderer == null)
                glyphRenderer = GetComponentInChildren<MeshRenderer>(true);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }
    }

}
