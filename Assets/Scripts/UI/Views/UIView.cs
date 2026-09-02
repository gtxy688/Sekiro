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

        // 复战 / 连战切场：把本 View 拉回"战斗刚开始的那一瞬间"。
        //
        // 契约与 ARPG.Combat.ICombatResettable 完全一致——顺序无关、可重复调用、只写自己的状态。
        // 刻意不实现那个接口：注册进 EncounterScope 要求每个 View 自己去注册，
        // 而 View 的生杀大权本来就归 Controller，由 Controller 转调一次更省事，也少一处注册遗漏。
        //
        // 默认什么都不做，这是有意的：
        //   血条 / 架势条 / 葫芦这类常驻 HUD 不能在这里被 Hide，
        //   它们的数值由 Controller 从 Config 重新推一遍（Config 整局不变，无顺序依赖），
        //   View 自作主张反而会和 Controller 推的值打架。
        //
        // 需要覆写的是真正持有"一场战斗内临时状态"的 View，典型四类：
        //   播放中的 tween、显示倒计时、跟随目标、"是否已经弹出过"这类一次性标志。
        public virtual void ResetForEncounter() { }
    }

}
