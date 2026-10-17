using UnityEngine;
using UnityEngine.UI;

namespace ARPG.UI
{

    // 玩家状态栏：回生节点 + 血条。用完的节点换成 EndDot，不要把图标直接关掉。
    public class PlayerStatusView : UIView
    {
        [Header("回生节点")]
        [SerializeField] private Image[] reviveDots;
        [SerializeField] private Image[] endDots;

        [Header("血条")]
        [SerializeField] private Slider hpBar;

        public override void OnViewInit()
        {
            for (int i = 0; i < endDots.Length; i++)
                endDots[i].gameObject.SetActive(false);
        }

        public void SetReviveDots(int remaining)
        {
            int total = reviveDots.Length;
            remaining = Mathf.Clamp(remaining, 0, total);

            for (int i = 0; i < total; i++)
            {
                // 从左到右用掉：剩 1 次时左边已换成 EndDot，右边还亮
                bool used = i < total - remaining;
                Image live = reviveDots[i];
                Image spent = endDots[i];
                live.enabled = true;
                live.gameObject.SetActive(!used);
                spent.enabled = true;
                spent.gameObject.SetActive(used);
            }
        }

        public void SetHP(float ratio)
        {
            hpBar.value = Mathf.Clamp01(ratio);
        }

    }

}
