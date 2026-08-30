using UnityEngine;

namespace ARPG.UI
{

    // MVC 之 View 基类：所有 UI 界面元素的基类
    // 职责：只负责"展示"，暴露 SetXXX 接口给 Controller 调用，不持有战斗逻辑
    public abstract class UIView : MonoBehaviour
    {
        // 显示/隐藏整个 View（按需覆写，比如加淡入淡出动画）
        public virtual void Show()
        {
            gameObject.SetActive(true);
        }

        public virtual void Hide()
        {
            gameObject.SetActive(false);
        }

        // 由 Controller 在初始化时调用一次，绑定引用（替代 Awake 里的手动查找）
        public virtual void OnViewInit() { }
    }

}
