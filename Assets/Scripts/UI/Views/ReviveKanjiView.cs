namespace ARPG.UI
{

    // 玩家回生成功时在头顶触发「回生」提示，外观由 Prefab 配置。
    // 显示逻辑全在 WorldGlyphView，这里只剩触发入口。
    public class ReviveKanjiView : WorldGlyphView
    {
        public void ShowRevive()
        {
            PlayGlyph();
        }
    }

}
