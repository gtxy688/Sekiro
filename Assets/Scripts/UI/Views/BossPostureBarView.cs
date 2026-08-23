using UnityEngine;
using UnityEngine.UI;

// 架势条：宽度映射架势。尖刺跟着条一起显示，不跟满条绑定。
// 从未涨过架势 → 隐藏；涨过后归零，持续 5 秒再隐藏。
public class BossPostureBarView : UIView
{
    [Header("架势条")]
    [SerializeField] private Image leftFill;
    [SerializeField] private Image rightFill;
    [SerializeField] private Image spike;
    [SerializeField] private float hideDelay = 5f;

    private float leftMaxWidth;
    private float rightMaxWidth;
    private bool everHadPosture;
    private float zeroTimer;

    public override void OnViewInit()
    {
        if (leftFill != null) leftMaxWidth = leftFill.rectTransform.sizeDelta.x;
        if (rightFill != null) rightMaxWidth = rightFill.rectTransform.sizeDelta.x;
        zeroTimer = 0f;
        everHadPosture = false;
        HideBar();
    }

    private void Update()
    {
        if (!everHadPosture) return;
        if (zeroTimer <= 0f) return;

        zeroTimer -= Time.deltaTime;
        if (zeroTimer <= 0f) HideBar();
    }

    public void SetPosture(float ratio)
    {
        ratio = Mathf.Clamp01(ratio);
        SetWidth(leftFill, leftMaxWidth * ratio);
        SetWidth(rightFill, rightMaxWidth * ratio);

        if (ratio > 0.001f)
        {
            everHadPosture = true;
            zeroTimer = 0f;
            ShowBar();
            return;
        }

        // 还没涨过架势：保持隐藏。涨过之后归零：开始 5 秒倒计时
        if (everHadPosture) zeroTimer = hideDelay;
        else HideBar();
    }

    private void ShowBar()
    {
        gameObject.SetActive(true);
        if (spike != null) spike.gameObject.SetActive(true);
    }

    private void HideBar()
    {
        zeroTimer = 0f;
        if (spike != null) spike.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    private static void SetWidth(Image img, float width)
    {
        if (img == null) return;
        Vector2 size = img.rectTransform.sizeDelta;
        size.x = width;
        img.rectTransform.sizeDelta = size;
    }
}
