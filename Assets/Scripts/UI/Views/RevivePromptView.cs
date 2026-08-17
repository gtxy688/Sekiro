using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 回生提示（M14 UI）：玩家死亡且有复活次数时弹出——大字"回生" + 按键提示
// 由 CombatUIController 订阅 OnReviveAvailable/OnRevived 驱动（事件总线，不轮询）
public class RevivePromptView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI reviveText; // 巨大"回生"字
    [SerializeField] private TMPro.TextMeshProUGUI hintText;   // "按攻击键复活"
    [SerializeField] private CanvasGroup canvasGroup;          // 整体淡入淡出

    private Tween fadeTween;

    // 弹出回生提示：淡入 + 脉动
    public void ShowPrompt()
    {
        Show();

        canvasGroup.DOKill();
        reviveText.transform.DOKill();

        canvasGroup.alpha = 0f;
        fadeTween = canvasGroup.DOFade(1f, 0.25f);

        // "回生"字脉动
        reviveText.transform.localScale = Vector3.one * 0.8f;
        reviveText.transform.DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        reviveText.DOColor(new Color(1f, 0.4f, 0.55f, 1f), 0.2f);
    }

    // 复活成功 → 隐藏
    public void HidePrompt()
    {
        canvasGroup.DOKill();
        fadeTween = canvasGroup.DOFade(0f, 0.15f).OnComplete(Hide);
    }
}
