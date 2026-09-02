using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARPG.UI
{
    // 暂停菜单的 UI 构建原语：造 Image / 造按钮 / 造面板，以及运行时程序生成的贴图。
    //
    // 为什么是实例类而不是静态类：它缓存运行时生成的 Sprite 与 Texture2D。
    // 这些资源每次 Build 都要复用，且必须跟着持有者的生命周期销毁，
    // 做成静态就等于把一堆 Texture2D 挂成全局，切场景也带不走。
    //
    // 刻意不持有：
    //   - 任何"当前选中哪个按钮""现在是第几页"这类状态（那是 Controller 的事）
    //   - 三个从 GUI 套件加载的 Sprite（它们在 Controller 上是 [SerializeField]，
    //     允许在 Inspector 里预先拖好，因此归 Controller 管，由调用方传进来）
    //
    // 这里每个方法都只负责"造出来并返回"，不往任何外部字段写值——
    // 赋值一律由调用方（BuildUIInto / CreateSliderRow 等）自己做，
    // 所以搬移这些方法不需要动 Controller 的字段结构。
    public class PauseMenuBuilder
    {
        private readonly List<Texture2D> generatedTextures = new List<Texture2D>();
        private Sprite diamondSprite;
        private Sprite fillBarSprite;

        // 滑块两端的菱形手柄。取用时才生成，避免没建过 UI 也白造两张贴图。
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

        // 生成的贴图不走 Unity 的资源管理，必须显式销毁，否则每次重建面板都泄漏两张。
        // 由 Controller 在 OnDestroy 里调用。
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

        // 以下三个没有做成 static：调用方统一写 builder.CreateImage(...) 更顺，
        // 而 C# 不允许用实例名访问静态成员（CS0176），混着写编译不过。
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

        // 叉号图标由调用方传入：它是 Controller 上的 [SerializeField]，
        // 可能已经在 Inspector 里拖好了，Builder 不该越过调用方自己去找图。
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
