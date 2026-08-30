using DG.Tweening;
using UnityEngine;

using ARPG.Boss.BehaviourTree;
using ARPG.Configs;
using ARPG.FrameWork.Body;
namespace ARPG.UI
{
    #if UNITY_EDITOR
    using UnityEditor;
    #endif

    // 「危」字：直接画危.png（亮度当透明）。不走加法 Shader，避免整块 Quad 被烧亮。
    [ExecuteAlways]
    public class PerilousWarningView : UIView
    {
        [SerializeField] private Transform followTarget;
        [SerializeField] private float headOffset = -0.4f;
        [SerializeField] private MeshRenderer glyphRenderer;
        [SerializeField] private Color tint = new Color(0.95f, 0.16f, 0.12f, 1f);
        [SerializeField] private float showDuration = 0.8f;

        private const float PopDuration = 0.12f;
        private const float FadeDuration = 0.15f;

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

        private void OnEnable()
        {
            BindRefs();
            if (!Application.isPlaying)
            {
                transform.localScale = Vector3.one;
                ApplyAlpha(1f);
            }
        }

        public void BindFollowTarget(CharacterBody target)
        {
            if (target == null) return;
            Transform found = FindDeep(target.transform, "Head")
                ?? FindDeep(target.transform, "Spine1")
                ?? FindDeep(target.transform, "Spine");
            followTarget = found != null ? found : target.transform;
        }

        public void ShowWarning(PerilousType type)
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
