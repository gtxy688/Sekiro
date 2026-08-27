using UnityEngine;
using UnityEngine.UI;

// 玩家状态栏：回生节点 + 血条。架势走独立的 PlayerPosture（BossPostureBarView）。
public class PlayerStatusView : UIView
{
    [Header("回生节点")]
    [SerializeField] private Image[] reviveDots;

    [Header("血条")]
    [SerializeField] private Slider hpBar;

    public override void OnViewInit()
    {
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
}
