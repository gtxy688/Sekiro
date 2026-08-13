using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 世界空间 UI：挂在 Boss 身上的锁定点
// 平时：半透明白点；架势崩解时：变大红点脉动提示可处决
public class LockOnIndicatorView : UIView
{
    [Header("锁定点")]
    [SerializeField] private Image dot; // 世界空间 UI 里的 Image

    // 切换锁定状态（白点显示/隐藏）
    public void SetLocked(bool isLocked)
    {
        dot.enabled = isLocked;
    }

    // 处决可用（架势崩解）→ 变大红点脉动提示
    public void SetFinisherReady(bool ready)
    {
        // 先杀掉残留动画，避免连续触发时叠加
        if (dot != null) dot.DOKill();
        if (dot != null) dot.transform.DOKill();

        if (ready)
        {
            dot.enabled = true;
            // 放大 + 红色脉动 + 高光
            dot.transform.DOScale(Vector3.one * 3f, 0.15f).SetEase(Ease.OutBack);
            dot.DOColor(Color.red, 0.3f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }
        else
        {
            // 回到默认半透明白点
            dot.transform.DOScale(Vector3.one, 0.15f);
            dot.DOColor(new Color(1, 1, 1, 0.5f), 0.15f);
        }
    }
}
