using DG.Tweening;
using UnityEngine;

using ARPG.FrameWork.Body;
namespace ARPG.UI
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif

    // 负责「危」「治」「回生」的世界空间跟随和弹出、淡出动画，配置来自场景或 Prefab。
    // 图像材质用亮度表示透明度并保持前景显示，避免黑底和加法混合亮块。
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

        private float alpha;
        private Tweener alphaTween;
        private DG.Tweening.Sequence showSeq;
        private MaterialPropertyBlock block;

        public override void OnViewInit()
        {
            BindRefs();
            ApplyAlpha(0f);
            Hide();
        }

        // 停止上一场的提示动画并隐藏，避免复战时残留文字。
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

        // 重播缩放和透明度动画，完成后隐藏提示。
        protected void PlayGlyph()
        {
            BindRefs();
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

        // 跟随绑定骨骼并朝向相机；位于镜头后方时停止绘制。
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
            glyphRenderer.enabled = !behind;
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
            if (block == null) block = new MaterialPropertyBlock();
            Color c = tint;
            c.a = tint.a * alpha;
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
