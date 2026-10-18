using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARPG.UI
{

    // 构建暂停菜单控件，并缓存生成的纹理。
    // 生成资源跟随 Controller 生命周期清理，页面状态和外部 Sprite 由调用方管理。
    public class PauseMenuBuilder
    {
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private Sprite diamondSprite;
        private Sprite fillBarSprite;

        // 按需生成并缓存滑块手柄，避免重复创建纹理。
        public Sprite DiamondSprite
        {
            get
            {
                EnsureGeneratedSprites();
                return diamondSprite;
            }
        }

        public Sprite FillBarSprite
        {
            get
            {
                EnsureGeneratedSprites();
                return fillBarSprite;
            }
        }

        public void EnsureGeneratedSprites()
        {
            if (diamondSprite == null)
                diamondSprite = CreateDiamondSprite();
            if (fillBarSprite == null)
                fillBarSprite = CreateFillBarSprite();
        }

        // 由 Controller 销毁时释放生成纹理，避免重建界面后残留资源。
        public void DestroyGenerated()
        {
            for (int i = 0; i < generatedTextures.Count; i++)
            {
                if (generatedTextures[i] != null)
                    Object.Destroy(generatedTextures[i]);
            }
            generatedTextures.Clear();
            diamondSprite = null;
            fillBarSprite = null;
        }

        public Image CreateImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        public TextMeshProUGUI CreateLabel(
            Transform parent,
            string text,
            float fontSize,
            TextAlignmentOptions align,
            Vector2 position,
            Vector2 size,
            string goName = null)
        {
            GameObject go = new GameObject(
                string.IsNullOrEmpty(goName) ? (string.IsNullOrEmpty(text) ? "Label" : text) : goName,
                typeof(RectTransform), typeof(CanvasRenderer));
            go.transform.SetParent(parent, false);

            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = PauseMenuStyle.TextColor;
            tmp.alignment = align;
            tmp.raycastTarget = false;

            RectTransform rect = tmp.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return tmp;
        }

        public void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public GameObject CreatePanel(Transform parent, string name, Vector2 size)
        {
            Image image = CreateImage(parent, name, PauseMenuStyle.PanelColor);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return image.gameObject;
        }

        public Button CreateMenuButton(
            Transform parent,
            string text,
            Vector2 position,
            UnityEngine.Events.UnityAction onClick,
            Vector2? size = null)
        {
            Image image = CreateImage(parent, text, PauseMenuStyle.ButtonColor);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size ?? new Vector2(320f, 56f);
            rect.anchoredPosition = position;

            Button button = image.gameObject.AddComponent<Button>();
            // ColorTint 会每帧盖掉 Image.color，选中态必须自己画（见 PauseMenuStyle）
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            button.onClick.AddListener(onClick);

            CreateLabel(image.transform, text, 26f, TextAlignmentOptions.Center, Vector2.zero, rect.sizeDelta);
            return button;
        }

        // 图标由 Controller 传入，以保留 Inspector 中的资源配置。
        public Button CreateCloseButton(
            Transform parent,
            Sprite icon,
            UnityEngine.Events.UnityAction onClick)
        {
            Image hit = CreateImage(parent, "Close", new Color(1f, 1f, 1f, 0f));
            RectTransform rect = hit.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(48f, 48f);
            rect.anchoredPosition = new Vector2(-8f, -8f);

            Image iconImage = CreateImage(hit.transform, "Icon", PauseMenuStyle.TextColor);
            iconImage.sprite = icon;
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;
            RectTransform iconRect = iconImage.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            iconRect.anchoredPosition = Vector2.zero;

            Button button = hit.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.targetGraphic = iconImage;
            button.onClick.AddListener(onClick);
            return button;
        }

        public Button CreateGameplayToggleButton(
            Transform parent,
            string title,
            Vector2 position,
            UnityEngine.Events.UnityAction onClick,
            out TextMeshProUGUI valueLabel)
        {
            Image image = CreateImage(parent, title, PauseMenuStyle.ButtonColor);
            RectTransform rect = image.rectTransform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(320f, 50f);
            rect.anchoredPosition = position;

            Button button = image.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.navigation = new Navigation { mode = Navigation.Mode.Explicit };
            button.onClick.AddListener(onClick);

            CreateLabel(image.transform, title, 24f, TextAlignmentOptions.Left,
                new Vector2(-62f, 0f), new Vector2(170f, 44f), "Title");
            valueLabel = CreateLabel(image.transform, "关", 24f, TextAlignmentOptions.Right,
                new Vector2(98f, 0f), new Vector2(56f, 44f), "Value");
            return button;
        }

        private Sprite CreateFillBarSprite()
        {
            const int size = 8;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.white;
            tex.SetPixels(pixels);
            tex.Apply(false, false);
            generatedTextures.Add(tex);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private Sprite CreateDiamondSprite()
        {
            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            Color[] pixels = new Color[size * size];
            float cx = (size - 1) * 0.5f;
            float cy = (size - 1) * 0.5f;
            float hw = 15.5f;
            float hh = 26.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Abs(x - cx) / hw + Mathf.Abs(y - cy) / hh;
                    float alpha = Mathf.Clamp01((1.08f - d) / 0.12f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha * alpha);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply(false, false);
            generatedTextures.Add(tex);
            return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 64f);
        }
    }
}
