using UnityEngine;
using UnityEngine.UI;

// 左上角 Boss 状态栏：忍杀提示灯(2红点) + Boss 血条 + 名称"苇名弦一郎"
// 只负责展示，数值由 CombatUIController 通过 SetXXX 传入
public class BossStatusView : UIView
{
    [Header("血条")]
    [SerializeField] private Slider hpBar;            // Boss 血条（左→右填充）

    [Header("忍杀提示灯")]
    [SerializeField] private Image[] lifeDots;        // 2 个红点，代表 2 条命

    [Header("名称")]
    [SerializeField] private TMPro.TextMeshProUGUI nameText; // "苇名弦一郎"

    // 设置剩余命数（2 → 1 → 0），点亮/熄灭对应红点
    public void SetLifeDots(int lives)
    {
        for (int i = 0; i < lifeDots.Length; i++)
        {
            lifeDots[i].enabled = i < lives;
        }
    }

    // 设置血条比例 (0~1)
    public void SetHP(float ratio)
    {
        hpBar.value = Mathf.Clamp01(ratio);
    }

    public void SetName(string name)
    {
        nameText.text = name;
    }
}
