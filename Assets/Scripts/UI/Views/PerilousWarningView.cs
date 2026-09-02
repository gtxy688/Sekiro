using UnityEngine;

namespace ARPG.UI
{

    // 「危」字：Boss 出危字招式时弹在玩家头顶，红色。
    // 显示逻辑全在 WorldGlyphView，这里只剩染色和触发入口。
    //
    // ShowWarning 不再收 PerilousType：当前三种危字（突刺 / 横扫 / 抓取）用同一张图，
    // 参数一路传下来却没人用。将来真要按类型切贴图时，加回来是一行改动，
    // 现在留着只是让每个调用点都多背一个用不到的实参。
    public class PerilousWarningView : WorldGlyphView
    {
        protected override Color DefaultTint => new Color(0.95f, 0.16f, 0.12f, 1f);

        public void ShowWarning()
        {
            PlayGlyph();
        }
    }

}
