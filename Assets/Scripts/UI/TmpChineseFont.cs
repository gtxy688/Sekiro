using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 中文 TMP：图集满了会把缺字换成空格。进场景前打开多图集、互为回退，并预热会用到的字。
public static class TmpChineseFont
{
    private const string SimyouPath = "Fonts/SIMYOU SDF";
    private const string FangsongPath = "Fonts/STFANGSO SDF";

    private const string UiGlyphs =
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
        "%….,!?:;、。，！？—-()/ " +
        "苇名弦一郎" +
        "我上了一定会守护是龙胤的力量吗那么无论多少次杀死你为止还没完神子的忍者" +
        "回生按攻击键复活死重新开始胜利击败再来一局退出游戏" +
        "暂停继续战斗设置退出音乐音量音效键位返回键盘鼠标手柄恢复默认按下新按键等待或取消" +
        "防御垫步跳跃葫芦锁定";

    private static TMP_FontAsset simyou;
    private static TMP_FontAsset fangsong;
    private static bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        EnsureReady();
    }

    public static TMP_FontAsset Primary
    {
        get
        {
            EnsureReady();
            return fangsong != null ? fangsong : simyou;
        }
    }

    public static void EnsureReady()
    {
        if (ready) return;

        simyou = Resources.Load<TMP_FontAsset>(SimyouPath);
        fangsong = Resources.Load<TMP_FontAsset>(FangsongPath);

        Prepare(simyou);
        Prepare(fangsong);
        AddFallback(simyou, fangsong);
        AddFallback(fangsong, simyou);
        RegisterGlobalFallback(simyou);
        RegisterGlobalFallback(fangsong);

        TryAdd(simyou, UiGlyphs);
        TryAdd(fangsong, UiGlyphs);
        ready = true;
    }

    // 只给编辑器生成新文字用。Play 时不要调用，否则会盖掉场景里调好的 Font Asset。
    public static void Apply(TMP_Text tmp)
    {
        EnsureReady();
        if (tmp == null || Primary == null) return;
        tmp.font = Primary;
    }

    public static void ApplyAll(Transform root)
    {
        if (root == null) return;
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
            Apply(texts[i]);
    }

    private static void Prepare(TMP_FontAsset font)
    {
        if (font == null) return;
        font.isMultiAtlasTexturesEnabled = true;
    }

    private static void AddFallback(TMP_FontAsset font, TMP_FontAsset fallback)
    {
        if (font == null || fallback == null || font == fallback) return;
        if (font.fallbackFontAssetTable == null)
            font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (!font.fallbackFontAssetTable.Contains(fallback))
            font.fallbackFontAssetTable.Add(fallback);
    }

    private static void RegisterGlobalFallback(TMP_FontAsset font)
    {
        if (font == null) return;
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null) return;
        if (!list.Contains(font))
            list.Add(font);
    }

    private static void TryAdd(TMP_FontAsset font, string glyphs)
    {
        if (font == null || string.IsNullOrEmpty(glyphs)) return;
        if (font.atlasPopulationMode == AtlasPopulationMode.Static) return;
        font.TryAddCharacters(glyphs);
    }
}
