using DG.Tweening;
using UnityEngine;

// 回生提示：玩家还有复活次数时弹出。外观跟暂停设置页同一套暗金。
public class RevivePromptView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI reviveText;
    [SerializeField] private TMPro.TextMeshProUGUI hintText;
    [SerializeField] private CanvasGroup canvasGroup;

    private Tween fadeTween;

    public override void OnViewInit()
    {
        CombatPromptStyle.EnsureChrome(transform, reviveText, hintText, new Vector2(500f, 280f));
    }

    public void ShowPrompt()
    {
        Show();
        OnViewInit();

        canvasGroup.DOKill();
        if (reviveText != null) reviveText.transform.DOKill();

        canvasGroup.alpha = 0f;
        fadeTween = canvasGroup.DOFade(1f, 0.25f).SetUpdate(true);
        if (reviveText != null)
        {
            reviveText.color = CombatPromptStyle.Accent;
            reviveText.transform.localScale = Vector3.one * 0.92f;
            reviveText.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }

    public void HidePrompt()
    {
        canvasGroup.DOKill();
        fadeTween = canvasGroup.DOFade(0f, 0.15f).SetUpdate(true).OnComplete(Hide);
    }
}
