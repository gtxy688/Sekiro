using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 战斗提示层（回生 / 胜利 / 真死）和暂停设置页同一套暗金，避免两套皮肤。
public static class CombatPromptStyle
{
    public static readonly Color Dimmer = new Color(0f, 0f, 0f, 0.72f);
    public static readonly Color Panel = new Color(0.07f, 0.07f, 0.07f, 0.96f);
    public static readonly Color Text = new Color(0.92f, 0.88f, 0.78f, 1f);
    public static readonly Color Accent = new Color(0.89f, 0.64f, 0.22f, 1f);
    public static readonly Color Divider = new Color(0.89f, 0.64f, 0.22f, 0.4f);

    public static void EnsureChrome(
        Transform root,
        TextMeshProUGUI title,
        TextMeshProUGUI hint,
        Vector2 panelSize)
    {
        if (root == null) return;

        Transform dimmerTf = root.Find("Dimmer");
        if (dimmerTf == null)
        {
            Image dimmer = CreateImage(root, "Dimmer", Dimmer);
            dimmer.raycastTarget = false;
            StretchFull(dimmer.rectTransform);
            dimmer.transform.SetAsFirstSibling();
        }

        Transform panelTf = root.Find("Panel");
        RectTransform panelRect;
        if (panelTf == null)
        {
            Image panel = CreateImage(root, "Panel", Panel);
            panel.raycastTarget = false;
            panelRect = panel.rectTransform;
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = panelSize;
        }
        else
        {
            panelRect = panelTf.GetComponent<RectTransform>();
            panelRect.sizeDelta = panelSize;
        }

        if (title != null && title.transform.parent != panelRect)
            title.transform.SetParent(panelRect, false);
        if (hint != null && hint.transform.parent != panelRect)
            hint.transform.SetParent(panelRect, false);

        if (title != null)
        {
            StyleTitle(title);
            Place(title.rectTransform, new Vector2(0f, 58f), new Vector2(panelSize.x - 48f, 64f));
        }

        if (hint != null)
        {
            StyleHint(hint);
            Place(hint.rectTransform, new Vector2(0f, -36f), new Vector2(panelSize.x - 48f, 40f));
        }

        if (panelRect.Find("Divider") == null)
        {
            Image divider = CreateImage(panelRect, "Divider", Divider);
            divider.raycastTarget = false;
            RectTransform dividerRect = divider.rectTransform;
            dividerRect.anchorMin = dividerRect.anchorMax = dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.sizeDelta = new Vector2(panelSize.x - 80f, 2f);
            dividerRect.anchoredPosition = new Vector2(0f, 12f);
        }
    }

    public static TextMeshProUGUI EnsureHint(Transform root, string text)
    {
        Transform existing = root.Find("Hint");
        if (existing == null && root.Find("Panel") != null)
            existing = root.Find("Panel/Hint");
        if (existing != null)
            return existing.GetComponent<TextMeshProUGUI>();

        GameObject go = new GameObject("Hint", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(root, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.raycastTarget = false;
        StyleHint(tmp);
        return tmp;
    }

    private static void StyleTitle(TextMeshProUGUI title)
    {
        title.fontSize = 42f;
        title.fontStyle = FontStyles.Normal;
        title.color = Accent;
        title.alignment = TextAlignmentOptions.Center;
        title.raycastTarget = false;
    }

    private static void StyleHint(TextMeshProUGUI hint)
    {
        hint.fontSize = 26f;
        hint.fontStyle = FontStyles.Normal;
        hint.color = Text;
        hint.alignment = TextAlignmentOptions.Center;
        hint.raycastTarget = false;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static void Place(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
