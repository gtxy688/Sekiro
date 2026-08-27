using DG.Tweening;
using UnityEngine;

// "危"字警告：钉在 Boss 头顶的世界空间 Billboard，不走屏幕中央 HUD。
public class PerilousWarningView : UIView
{
    [SerializeField] private Transform followTarget;
    [SerializeField] private float headOffset = 0.55f;
    [SerializeField] private MeshRenderer glowRenderer;
    [SerializeField] private MeshRenderer coreRenderer;
    [SerializeField] private float showDuration = 0.8f;

    private const float PopDuration = 0.12f;
    private const float FadeDuration = 0.15f;

    private float intensity;
    private Tweener intensityTween;
    // 必须写全名：全局命名空间已有行为树 Sequence，会盖住 using DG.Tweening
    private DG.Tweening.Sequence showSeq;
    private MaterialPropertyBlock block;

    public override void OnViewInit()
    {
        BindRefs();
        ApplyIntensity(0f);
        Hide();
    }

    public void BindFollowTarget(CharacterBody boss)
    {
        if (boss == null) return;
        Transform found = FindDeep(boss.transform, "Head")
            ?? FindDeep(boss.transform, "Spine1")
            ?? FindDeep(boss.transform, "Spine");
        followTarget = found != null ? found : boss.transform;
    }

    public void ShowWarning(PerilousType type)
    {
        BindRefs();
        if (glowRenderer == null && coreRenderer == null)
            return;

        KillTweens();
        Show();

        transform.localScale = Vector3.zero;
        ApplyIntensity(0f);

        showSeq = DOTween.Sequence();
        showSeq.Append(transform.DOScale(Vector3.one, PopDuration).SetEase(Ease.OutBack));
        showSeq.Join(TweenIntensity(1f, 0.08f));
        float hold = Mathf.Max(0f, showDuration - PopDuration - FadeDuration);
        showSeq.AppendInterval(hold);
        showSeq.Append(transform.DOScale(Vector3.one * 0.8f, FadeDuration).SetEase(Ease.InQuad));
        showSeq.Join(TweenIntensity(0f, FadeDuration));
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

        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 to = pos - cam.transform.position;
        bool behind = Vector3.Dot(cam.transform.forward, to) <= 0f;
        SetRenderersVisible(!behind);
        if (behind) return;

        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(cam.transform.forward, Vector3.up);
    }

    private Tweener TweenIntensity(float to, float duration)
    {
        intensityTween = DOTween.To(() => intensity, v => ApplyIntensity(v), to, duration);
        return intensityTween;
    }

    private void ApplyIntensity(float value)
    {
        intensity = value;
        if (block == null) block = new MaterialPropertyBlock();
        block.SetFloat("_Intensity", intensity);
        if (glowRenderer != null) glowRenderer.SetPropertyBlock(block);
        if (coreRenderer != null) coreRenderer.SetPropertyBlock(block);
    }

    private void SetRenderersVisible(bool visible)
    {
        if (glowRenderer != null) glowRenderer.enabled = visible;
        if (coreRenderer != null) coreRenderer.enabled = visible;
    }

    private void KillTweens()
    {
        if (showSeq != null && showSeq.IsActive()) showSeq.Kill();
        showSeq = null;
        if (intensityTween != null && intensityTween.IsActive()) intensityTween.Kill();
        intensityTween = null;
        transform.DOKill();
    }

    private void BindRefs()
    {
        if (glowRenderer == null)
        {
            Transform t = transform.Find("Glow");
            if (t != null) glowRenderer = t.GetComponent<MeshRenderer>();
        }
        if (coreRenderer == null)
        {
            Transform t = transform.Find("Core");
            if (t != null) coreRenderer = t.GetComponent<MeshRenderer>();
        }
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
