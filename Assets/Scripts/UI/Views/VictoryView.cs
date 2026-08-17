using DG.Tweening;
using UnityEngine;

// 胜利提示（M10 UI）：Boss 命数清空时弹出——"胜利"文字。
// 轻量状态提示（策划案：击杀结算画面不在范围内，这里只给一个落点）
public class VictoryView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI victoryText;
    [SerializeField] private CanvasGroup canvasGroup;

    public void ShowVictory()
    {
        Show();

        canvasGroup.DOKill();
        victoryText.transform.DOKill();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.5f);

        victoryText.transform.localScale = Vector3.one * 0.6f;
        victoryText.transform.DOScale(Vector3.one, 0.35f).SetEase(Ease.OutBack);
    }
}
