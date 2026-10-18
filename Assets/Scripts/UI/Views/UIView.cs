using UnityEngine;

namespace ARPG.UI
{

    // 战斗界面视图基类，提供显示、隐藏、初始化和复战复位入口。
    public abstract class UIView : MonoBehaviour
    {
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        // 由控制器初始化视图引用和显示状态，也可在动态重建后再次调用。
        public virtual void OnViewInit() { }

        // 由 CombatUIController 统一调用，清理视图自己的临时状态；覆写实现应可重复调用且不依赖复位顺序。
        // 默认保留常驻 HUD，初始数值由控制器重新推送。
        public virtual void ResetForEncounter() { }
    }

}
