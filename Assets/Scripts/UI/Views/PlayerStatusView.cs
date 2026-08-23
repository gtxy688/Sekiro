using UnityEngine;
using UnityEngine.UI;

// 玩家状态栏：回生节点 + 血条 + 架势条（宽度映射架势）
public class PlayerStatusView : UIView
{
    [Header("回生节点")]
    [SerializeField] private Image[] reviveDots;

    [Header("血条")]
    [SerializeField] private Slider hpBar;

    [Header("架势条")]
    [SerializeField] private Image postureLeftFill;
    [SerializeField] private Image postureRightFill;

    private float leftMaxWidth;
    private float rightMaxWidth;

    public override void OnViewInit()
    {
        if (postureLeftFill != null) leftMaxWidth = postureLeftFill.rectTransform.sizeDelta.x;
        if (postureRightFill != null) rightMaxWidth = postureRightFill.rectTransform.sizeDelta.x;
    }

    public void SetReviveDots(int count)
    {
        if (reviveDots == null) return;
        for (int i = 0; i < reviveDots.Length; i++)
        {
            if (reviveDots[i] != null) reviveDots[i].enabled = i < count;
        }
    }

    public void SetHP(float ratio)
    {
        if (hpBar != null) hpBar.value = Mathf.Clamp01(ratio);
    }

    public void SetPosture(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        SetWidth(postureLeftFill, leftMaxWidth * ratio);
        SetWidth(postureRightFill, rightMaxWidth * ratio);
    }

    private static void SetWidth(Image img, float width)
    {
        if (img == null) return;
        Vector2 size = img.rectTransform.sizeDelta;
        size.x = width;
        img.rectTransform.sizeDelta = size;
    }
}
