using UnityEngine;
using UnityEngine.UI;

// 玩家状态栏：回生节点(粉花瓣) + 血条 + 架势条
// 只负责展示，数值由 CombatUIController 传入
public class PlayerStatusView : UIView
{
    [Header("回生节点")]
    [SerializeField] private Image[] reviveDots;  // 回生次数图标（本次需求 1 次）

    [Header("血条")]
    [SerializeField] private Slider hpBar;

    [Header("架势条")]
    [SerializeField] private Image postureLeftFill;  // 左半段（中心向左）
    [SerializeField] private Image postureRightFill; // 右半段（中心向右）

    // 设置剩余回生次数（点亮/熄灭图标）
    public void SetReviveDots(int count)
    {
        for (int i = 0; i < reviveDots.Length; i++)
        {
            reviveDots[i].enabled = i < count;
        }
    }

    public void SetHP(float ratio)
    {
        hpBar.value = Mathf.Clamp01(ratio);
    }

    // 玩家架势条：中心向两边双向增长
    public void SetPosture(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);

        if (postureLeftFill != null)
            postureLeftFill.fillAmount = ratio;
        if (postureRightFill != null)
            postureRightFill.fillAmount = ratio;
    }
}
