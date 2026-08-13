using UnityEngine;
using UnityEngine.UI;

// "危"字警告：Boss 放危字攻击时弹出，玩家头顶 World→Canvas 投影
// 只负责展示，由 CombatUIController 触发
public class PerilousWarningView : UIView
{
    [SerializeField] private TMPro.TextMeshProUGUI warningText; // 巨大红色"危"字
    [SerializeField] private float showDuration = 1.0f;          // 显示时长
    private float timer;

    public override void Show()
    {
        base.Show();
        timer = showDuration;
        // TODO(DoTween): 放大淡入 + 红光泛晕
    }

    private void Update()
    {
        if (timer <= 0) return;

        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            Hide();
        }
    }

    // 由 Controller 调用：显示"危"
    public void ShowWarning(PerilousType type)
    {
        Show();
    }
}
