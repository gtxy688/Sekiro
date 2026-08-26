using DG.Tweening;
using UnityEngine;

// 胜利提示：Boss 命数清空。外观跟暂停设置页同一套暗金，不是结算画面。
public class VictoryView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI victoryText;
    [SerializeField] private TMPro.TextMeshProUGUI hintText;
    [SerializeField] private CanvasGroup canvasGroup;

    public override void OnViewInit()
    {
        if (hintText == null)
            hintText = CombatPromptStyle.EnsureHint(transform, "击败 苇名弦一郎");
        CombatPromptStyle.EnsureChrome(transform, victoryText, hintText, new Vector2(500f, 280f));
    }

    public void ShowVictory()
    {
        Show();
        OnViewInit();

        canvasGroup.DOKill();
        if (victoryText != null) victoryText.transform.DOKill();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.35f).SetUpdate(true);
        if (victoryText != null)
        {
            victoryText.color = CombatPromptStyle.Accent;
            victoryText.transform.localScale = Vector3.one * 0.92f;
            victoryText.transform.DOScale(Vector3.one, 0.25f).SetEase(Ease.OutCubic).SetUpdate(true);
        }
    }
}
