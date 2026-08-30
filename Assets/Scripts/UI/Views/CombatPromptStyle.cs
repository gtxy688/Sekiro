using TMPro;
using UnityEngine;
using UnityEngine.UI;

using ARPG.Mgr;
namespace ARPG.UI
{

    // 战斗提示层（回生 / 胜利 / 真死）和暂停设置页同一套暗金，避免两套皮肤。
    public static class CombatPromptStyle
    {
        public static readonly Color Dimmer = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color Panel = new Color(0.07f, 0.07f, 0.07f, 0.96f);
        public static readonly Color Text = new Color(0.92f, 0.88f, 0.78f, 1f);
        public static readonly Color Accent = new Color(0.89f, 0.64f, 0.22f, 1f);
        public static readonly Color Divider = new Color(0.89f, 0.64f, 0.22f, 0.4f);
        public static readonly Color Button = new Color(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color SelectedFill = new Color(0.95f, 0.78f, 0.22f, 1f);
        public static readonly Color SelectedText = new Color(0.12f, 0.1f, 0.05f, 1f);

        public static void EnsureChrome(
            Transform root,
            TextMeshProUGUI title,
            TextMeshProUGUI hint,
            Vector2 panelSize,
            bool withActionButton = false)
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

            float titleY = withActionButton ? 92f : 58f;
            float hintY = withActionButton ? -16f : -36f;
            float dividerY = withActionButton ? 28f : 12f;

            if (title != null)
            {
                StyleTitle(title);
                Place(title.rectTransform, new Vector2(0f, titleY), new Vector2(panelSize.x - 48f, 64f));
            }

            if (hint != null)
            {
                StyleHint(hint);
                Place(hint.rectTransform, new Vector2(0f, hintY), new Vector2(panelSize.x - 48f, 40f));
            }

            Transform dividerTf = panelRect.Find("Divider");
            if (dividerTf == null)
            {
                Image divider = CreateImage(panelRect, "Divider", Divider);
                divider.raycastTarget = false;
                dividerTf = divider.transform;
            }

            RectTransform dividerRect = dividerTf.GetComponent<RectTransform>();
            dividerRect.anchorMin = dividerRect.anchorMax = dividerRect.pivot = new Vector2(0.5f, 0.5f);
            dividerRect.sizeDelta = new Vector2(panelSize.x - 80f, 2f);
            dividerRect.anchoredPosition = new Vector2(0f, dividerY);

            EnsureCloseButton(panelRect, null);

            if (Application.isPlaying)
            {
                panelRect.anchoredPosition = Vector2.zero;
                if (dimmerTf != null)
                    dimmerTf.gameObject.SetActive(true);
            }
        }

        public static Button EnsureActionButton(Transform root, string label)
        {
            return EnsureActionButton(root, label, "Replay", new Vector2(0f, -108f), new Vector2(280f, 52f));
        }

        public static Button EnsureActionButton(
            Transform root,
            string label,
            string goName,
            Vector2 position,
            Vector2 size)
        {
            Transform panel = root != null ? root.Find("Panel") : null;
            if (panel == null) return null;

            Transform existing = panel.Find(goName);
            if (existing != null)
            {
                RectTransform existingRect = existing as RectTransform;
                if (existingRect != null)
                {
                    existingRect.sizeDelta = size;
                    existingRect.anchoredPosition = position;
                }

                TextMeshProUGUI existingLabel = existing.GetComponentInChildren<TextMeshProUGUI>();
                if (existingLabel != null) existingLabel.text = label;
                return existing.GetComponent<Button>();
            }

            Image image = CreateImage(panel, goName, Button);
            image.raycastTarget = true;
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;

            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };

            GameObject textGo = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer));
            textGo.transform.SetParent(image.transform, false);
            TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 26f;
            tmp.color = Text;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            Place(tmp.rectTransform, Vector2.zero, size);
            return button;
        }

        public static Button EnsureCloseButton(Transform panel, UnityEngine.Events.UnityAction onClick)
        {
            if (panel == null) return null;

            Transform existing = panel.Find("Close");
            Button button;
            if (existing != null)
            {
                button = existing.GetComponent<Button>();
                if (button == null) button = existing.gameObject.AddComponent<Button>();
            }
            else
            {
                Image hit = CreateImage(panel, "Close", new Color(1f, 1f, 1f, 0f));
                hit.raycastTarget = true;
                RectTransform rect = hit.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(48f, 48f);
                rect.anchoredPosition = new Vector2(-8f, -8f);

                Image icon = CreateImage(hit.transform, "Icon", Text);
                icon.sprite = LoadCloseSprite();
                icon.preserveAspect = true;
                icon.raycastTarget = false;
                RectTransform iconRect = icon.rectTransform;
                iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
                iconRect.sizeDelta = new Vector2(28f, 28f);
                iconRect.anchoredPosition = Vector2.zero;

                button = hit.gameObject.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.targetGraphic = icon;
            }

            if (onClick != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(onClick);
            }

            return button;
        }

        public static void QuitGame()
        {
            CombatInputGate.SetBlocked(false);
            if (GamePause.IsPaused)
                GamePause.SetPaused(false);

            if (Application.isEditor)
            {
                HideNamed("EndPanel");
                HideNamed("GameOver");
                return;
            }

            Application.Quit();
        }

        private static void HideNamed(string name)
        {
            GameObject canvas = GameObject.Find("CombatCanvas");
            Transform t = canvas != null ? canvas.transform.Find(name) : null;
            if (t == null)
            {
                GameObject found = GameObject.Find(name);
                t = found != null ? found.transform : null;
            }

            if (t != null)
                t.gameObject.SetActive(false);
        }

        public static void ApplyButtonSelected(Button button, bool selected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (selected)
            {
                if (image != null) image.color = SelectedFill;
                if (label != null) label.color = SelectedText;
                return;
            }

            if (image != null) image.color = Button;
            if (label != null) label.color = Text;
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

        private static Sprite LoadCloseSprite()
        {
            Transform setting = FindSettingCloseIcon();
            if (setting != null)
            {
                Image icon = setting.GetComponent<Image>();
                if (icon != null && icon.sprite != null)
                    return icon.sprite;
            }

    #if UNITY_EDITOR
            UnityEngine.Object[] assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(
                "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/cross-128.png");
            if (assets != null)
            {
                for (int i = 0; i < assets.Length; i++)
                {
                    Sprite sprite = assets[i] as Sprite;
                    if (sprite != null)
                        return sprite;
                }
            }
    #endif
            return null;
        }

        private static Transform FindSettingCloseIcon()
        {
            GameObject canvas = GameObject.Find("CombatCanvas");
            if (canvas == null) return null;
            return canvas.transform.Find("SettingPanel/HubPanel/Close/Icon");
        }

        private static void StyleTitle(TextMeshProUGUI title)
        {
            title.fontStyle = FontStyles.Normal;
            title.color = Accent;
            title.alignment = TextAlignmentOptions.Center;
            title.raycastTarget = false;
        }

        private static void StyleHint(TextMeshProUGUI hint)
        {
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

}
