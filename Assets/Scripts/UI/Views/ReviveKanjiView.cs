namespace ARPG.UI
{

    // 「回生」字：玩家爬起成功时弹在头顶，金白色。位置与「治」相同。
    // 显示逻辑全在 WorldGlyphView，这里只剩触发入口。
    public class ReviveKanjiView : WorldGlyphView
    {
        public void ShowRevive()
        {
            PlayGlyph();
        }
    }

}
