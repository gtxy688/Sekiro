using UnityEngine;
using UnityEngine.UI;

namespace ARPG.UI
{

    // 右下角物品栏：葫芦槽位（图标 + 数量）
    // 只负责展示，数值由 CombatUIController 传入
    public class ItemSlotView : UIView
    {
        [Header("葫芦")]
        [SerializeField] private Image gourdIcon;  // 伤药葫芦 2D 贴图
        [SerializeField] private TMPro.TextMeshProUGUI countText; // 剩余数量白色数字

        public void SetGourdIcon(Sprite icon)
        {
            gourdIcon.sprite = icon;
        }

        public void SetGourdCount(int count)
        {
            countText.text = count.ToString();
        }
    }

}
