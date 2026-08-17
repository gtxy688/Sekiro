using DG.Tweening;
using UnityEngine;

// 游戏结束提示（M14 UI）：真死时弹出——大字"死" + 重开按键提示
// 由 CombatUIController 订阅 OnDeath 驱动。轻量提示，非结算画面（策划案范围外不做结算）
public class GameOverView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI deathText; // 巨大"死"字
    [SerializeField] private TMPro.TextMeshProUGUI hintText;  // "按攻击键重新开始"
    [SerializeField] private CanvasGroup canvasGroup;

    public void ShowGameOver()
    {
        Show();

        canvasGroup.DOKill();
        deathText.transform.DOKill();

        canvasGroup.alpha = 0f;
        canvasGroup.DOFade(1f, 0.6f);

        // "死"字压出：大 → 正常，带红晕
        deathText.transform.localScale = Vector3.one * 1.8f;
        deathText.transform.DOScale(Vector3.one, 0.4f).SetEase(Ease.OutCubic);
        deathText.DOColor(new Color(0.8f, 0.05f, 0.05f, 1f), 0.5f);
    }
}
