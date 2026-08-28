using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 弦一郎台词：钉在玩家架势条上方，不挡中央结算面板。
public class VoiceLineView : UIView
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI lineText;

    public override void OnViewInit()
    {
        EnsureUi();
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
        Hide();
    }

    public void ShowLine(string text)
    {
        EnsureUi();
        if (lineText != null)
            lineText.text = text ?? string.Empty;

        Show();
        transform.SetAsLastSibling();

        if (canvasGroup == null) return;
        canvasGroup.DOKill();
        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.12f).SetUpdate(true).SetLink(gameObject);
    }

    public void HideLine()
    {
        if (canvasGroup == null)
        {
            Hide();
            return;
        }

        canvasGroup.DOKill();
        canvasGroup.DOFade(0f, 0.2f)
            .SetUpdate(true)
            .SetLink(gameObject)
            .OnComplete(() =>
            {
                if (this == null) return;
                Hide();
            });
    }

    private void OnDisable()
    {
        if (canvasGroup != null)
            canvasGroup.DOKill();
    }

    private void EnsureUi()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (lineText == null)
        {
            Transform existing = transform.Find("Line");
            if (existing != null)
                lineText = existing.GetComponent<TextMeshProUGUI>();
        }

        Image bg = GetComponent<Image>();
        if (bg != null)
            bg.enabled = false;

        if (lineText != null) return;

        GameObject textGo = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer));
        textGo.transform.SetParent(transform, false);
        lineText = textGo.AddComponent<TextMeshProUGUI>();
        lineText.fontSize = 26f;
        lineText.color = CombatPromptStyle.Text;
        lineText.alignment = TextAlignmentOptions.Center;
        lineText.raycastTarget = false;

        RectTransform textRect = lineText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 4f);
        textRect.offsetMax = new Vector2(-16f, -4f);
    }
}
