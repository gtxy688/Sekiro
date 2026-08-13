using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 顶部居中 Boss 架势条：中心向两边双向增长
// 只负责展示，数值由 CombatUIController 传入
public class BossPostureBarView : UIView
{
    [Header("架势条")]
    [SerializeField] private Image leftFill;   // 左半段（从中心向左填充）
    [SerializeField] private Image rightFill;  // 右半段（从中心向右填充）
    [SerializeField] private Image spike;      // 高亮时的边缘尖刺装饰（可选）

    // 设置架势比例 (0~1)，从中心向两边同时增长
    public void SetPosture(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        // 双向增长：中心锚点，fillAmount 各占一半
        if (leftFill != null)
            leftFill.fillAmount = ratio;
        if (rightFill != null)
            rightFill.fillAmount = ratio;
    }

    // 架势条快满时高亮：颜色变亮 + 边缘尖刺脉动
    public void SetDanger(bool isDanger)
    {
        // 先杀掉残留动画，避免连续触发时叠加
        leftFill.DOKill();
        rightFill.DOKill();
        if (spike != null) spike.DOKill();

        if (isDanger)
        {
            // 颜色循环渐变制造"快崩了"的紧张感，来回往复
            leftFill.DOColor(new Color(1f, 0.55f, 0.2f), 0.25f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
            rightFill.DOColor(new Color(1f, 0.55f, 0.2f), 0.25f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);

            if (spike != null)
            {
                spike.gameObject.SetActive(true);
                // 尖刺脉冲放大制造"濒临崩解"的视觉警告
                spike.transform.DOScale(1.3f, 0.2f)
                    .SetLoops(-1, LoopType.Yoyo)
                    .SetEase(Ease.InOutQuad);
            }
        }
        else
        {
            // 恢复原色
            leftFill.DOColor(Color.white, 0.2f);
            rightFill.DOColor(Color.white, 0.2f);

            if (spike != null)
            {
                spike.DOKill();
                spike.transform.DOKill();
                spike.gameObject.SetActive(false);
            }
        }
    }
}
