using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// "危"字警告：Boss 放危字攻击时弹出
// 场景/Prefab 里实际是 Image（imgWarn），HUD 生成器才挂 TMP。两套都支持。
public class PerilousWarningView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI warningText;
    [SerializeField] private Image warningImage;
    [SerializeField] private Image glow;
    [SerializeField] private float showDuration = 0.8f;

    public override void OnViewInit()
    {
        BindRefs();
    }

    private void BindRefs()
    {
        if (warningText == null)
            warningText = GetComponentInChildren<TMPro.TextMeshProUGUI>(true);

        if (warningImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] == null || images[i] == glow) continue;
                warningImage = images[i];
                break;
            }
        }
    }

    public void ShowWarning(PerilousType type)
    {
        BindRefs();

        if (warningText != null)
        {
            warningText.DOKill();
            warningText.transform.DOKill();
        }
        if (warningImage != null)
        {
            warningImage.DOKill();
            warningImage.transform.DOKill();
        }
        if (glow != null) glow.DOKill();

        Transform main = warningText != null
            ? warningText.transform
            : (warningImage != null ? warningImage.transform : null);

        Show();
        if (warningImage != null && !warningImage.gameObject.activeSelf)
            warningImage.gameObject.SetActive(true);

        if (main == null)
            return;

        main.localScale = Vector3.zero;
        if (warningText != null)
        {
            Color c = warningText.color;
            warningText.color = new Color(c.r, c.g, c.b, 0f);
        }
        if (warningImage != null)
        {
            Color c = warningImage.color;
            warningImage.color = new Color(c.r, c.g, c.b, 0f);
        }
        if (glow != null)
            glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0f);

        DG.Tweening.Sequence seq = DOTween.Sequence();
        seq.Append(main.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack));
        if (warningText != null)
        {
            Color c = warningText.color;
            seq.Join(warningText.DOColor(new Color(c.r, c.g, c.b, 1f), 0.08f));
        }
        if (warningImage != null)
            seq.Join(warningImage.DOFade(1f, 0.08f));
        if (glow != null)
            seq.Join(glow.DOColor(new Color(1f, 0.3f, 0.1f, 0.6f), 0.12f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));

        seq.AppendInterval(showDuration);

        seq.Append(main.DOScale(Vector3.one * 0.8f, 0.15f).SetEase(Ease.InQuad));
        if (warningText != null)
        {
            Color c = warningText.color;
            seq.Join(warningText.DOColor(new Color(c.r, c.g, c.b, 0f), 0.15f));
        }
        if (warningImage != null)
            seq.Join(warningImage.DOFade(0f, 0.15f));
        if (glow != null)
            seq.Join(glow.DOFade(0f, 0.15f));

        seq.OnComplete(Hide);
    }
}
