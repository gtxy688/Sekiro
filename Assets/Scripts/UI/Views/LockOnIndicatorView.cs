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

    // 处决可用（架势崩解）→ 变大红点
    public void SetFinisherReady(bool ready)
    {
        if (ready)
        {
            // TODO(DoTween): 放大 + 红色脉动 + 高光
            dot.color = Color.red;
            dot.transform.localScale = Vector3.one * 3f;
        }
        else
        {
            dot.color = new Color(1, 1, 1, 0.5f);
            dot.transform.localScale = Vector3.one;
        }
    }
}
