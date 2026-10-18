namespace ARPG.UI
{

    // Boss 发出危招时触发红色「危」字；三种危招共用同一提示图。
    public class PerilousWarningView : WorldGlyphView
    {
        public void ShowWarning()
        {
            PlayGlyph();
        }
    }

}
