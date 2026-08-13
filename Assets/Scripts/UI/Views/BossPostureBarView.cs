using UnityEngine;
using UnityEngine.UI;

// 顶部居中 Boss 架势条：中心向两边双向增长
// 只负责展示，数值由 CombatUIController 传入
public class BossPostureBarView : UIView
{
    [Header("架势条")]
    [SerializeField] private Image leftFill;   // 左半段（从中心向左填充）
    [SerializeField] private Image rightFill;  // 右半段（从中心向右填充）

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

    // 架势条快满时高亮（M13 接 DoTween 后实现颜色渐变/尖刺）
    public void SetDanger(bool isDanger)
    {
        // TODO(DoTween): 架势 > 80% 时颜色变亮 + 边缘尖刺动画
    }
}
