using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// "危"字警告：Boss 放危字攻击时弹出，玩家头顶 World→Canvas 投影
// 只负责展示，由 CombatUIController 触发
public class PerilousWarningView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI warningText; // 巨大红色"危"字
    [SerializeField] private Image glow;                        // 背后的红光泛晕（可选）
    [SerializeField] private float showDuration = 0.8f;         // 保持全亮的时间

    // 由 Controller 调用：显示"危"，播完自动隐藏
    public void ShowWarning(PerilousType type)
    {
        // 先杀掉上次残留动画，避免连续触发时叠加
        if (warningText != null) warningText.DOKill();
        if (warningText != null) warningText.transform.DOKill();
        if (glow != null) glow.DOKill();

        Show();

        // 初始态：缩放 0、全透明
        warningText.transform.localScale = Vector3.zero;
        warningText.color = new Color(warningText.color.r, warningText.color.g, warningText.color.b, 0f);
        if (glow != null) glow.color = new Color(glow.color.r, glow.color.g, glow.color.b, 0f);

        // 序列：放大淡入 → 保持 → 缩小淡出 → 隐藏
        var seq = DOTween.Sequence();
        seq.Append(warningText.transform.DOScale(Vector3.one, 0.12f).SetEase(Ease.OutBack));
        seq.Join(warningText.DOColor(new Color(warningText.color.r, warningText.color.g, warningText.color.b, 1f), 0.08f));
        if (glow != null)
            seq.Join(glow.DOColor(new Color(1f, 0.3f, 0.1f, 0.6f), 0.12f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine));

        seq.AppendInterval(showDuration);

        seq.Append(warningText.transform.DOScale(Vector3.one * 0.8f, 0.15f).SetEase(Ease.InQuad));
        seq.Join(warningText.DOColor(new Color(warningText.color.r, warningText.color.g, warningText.color.b, 0f), 0.15f));
        if (glow != null)
            seq.Join(glow.DOFade(0f, 0.15f));

        seq.OnComplete(Hide);
    }
}
