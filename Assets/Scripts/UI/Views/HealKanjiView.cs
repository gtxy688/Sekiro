using UnityEngine;

namespace ARPG.UI
{

    // 「治」字：玩家喝葫芦成功时弹在头顶，绿色。
    // 显示逻辑全在 WorldGlyphView，这里只剩染色和触发入口。
    public class HealKanjiView : WorldGlyphView
    {
        protected override Color DefaultTint => new Color(0.18f, 0.92f, 0.32f, 1f);

        public void ShowHeal()
        {
            PlayGlyph();
        }
    }

}
