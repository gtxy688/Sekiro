namespace ARPG.UI
{

    // 「治」字：玩家喝葫芦成功时弹在头顶，绿色。
    // 显示逻辑全在 WorldGlyphView，这里只剩触发入口。
    public class HealKanjiView : WorldGlyphView
    {
        public void ShowHeal()
        {
            PlayGlyph();
        }
    }

}
