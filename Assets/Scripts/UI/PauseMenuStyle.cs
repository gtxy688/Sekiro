using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ARPG.UI
{
    // 暂停菜单的配色、选中态画法，以及一批无状态的查找 / 摆位小工具。
    //
    // 从 PauseMenuController 里分出来的理由很实际：那一个类原本 1450 行，
    // 而这里每个成员都是静态的、不碰任何实例状态——搬走不需要动一行调用逻辑，
    // 零风险地先削掉一块，剩下的才是真正需要状态的部分（页面机、改键流程）。
    //
    // 刻意不含：任何 Button 字段、任何"现在是第几页"这类状态。
    // 一旦这里开始持有状态，它就和 Controller 没有区别，拆分也就白拆了。
    public static class PauseMenuStyle
    {
        public const string KitTrackPath =
            "Assets/Space_Exploration_GUI_Kit/Settings_&_Menu_Components/Large/sound-bar-container-large.png";
        public const string KitDividerPath =
            "Assets/Space_Exploration_GUI_Kit/Settings_&_Menu_Components/Large/settings-divider-large.png";
        public const string KitClosePath =
            "Assets/Space_Exploration_GUI_Kit/Picto_Icons/White/cross-128.png";

        public static readonly Color DimmerColor = new Color(0f, 0f, 0f, 0.72f);
        public static readonly Color PanelColor = new Color(0.07f, 0.07f, 0.07f, 0.96f);
        public static readonly Color ButtonColor = new Color(0.18f, 0.18f, 0.18f, 1f);
        public static readonly Color TabActive = new Color(0.38f, 0.3f, 0.14f, 1f);
        public static readonly Color SelectedFill = new Color(0.95f, 0.78f, 0.22f, 1f);
        public static readonly Color TextColor = new Color(0.92f, 0.88f, 0.78f, 1f);
        public static readonly Color SelectedTextColor = new Color(0.12f, 0.1f, 0.05f, 1f);
        public static readonly Color AccentColor = new Color(0.89f, 0.64f, 0.22f, 1f);
        public static readonly Color HandleIdle = new Color(0.86f, 0.6f, 0.18f, 1f);
        public static readonly Color HandleSelected = new Color(1f, 0.82f, 0.38f, 1f);
        public static readonly Color FillIdle = new Color(0.9f, 0.68f, 0.24f, 1f);
        public static readonly Color FillSelected = new Color(1f, 0.82f, 0.4f, 1f);
        // 套件槽是深紫，乘暖色压掉青紫，只留暗槽形
        public static readonly Color TrackTint = new Color(0.92f, 0.78f, 0.48f, 1f);
        public static readonly Color DividerColor = new Color(0.89f, 0.64f, 0.22f, 0.4f);
        public static readonly Color SliderLabelIdle = new Color(0.93f, 0.93f, 0.93f, 1f);

        public static GameObject CurrentSelected =>
            EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

        public static bool IsUiSelected(Button button)
        {
            return IsUiSelected(button, CurrentSelected);
        }

        public static bool IsUiSelected(Button button, GameObject selected)
        {
            return button != null && selected != null && button.gameObject == selected;
        }

        // 注意：按钮的 transition 一律设成 None。
        // ColorTint 会每帧把 Image.color 盖回去，选中态只能自己画。
        public static void ApplyButtonVisual(Button button, bool schemeActive, bool uiSelected)
        {
            if (button == null) return;
            Image image = button.GetComponent<Image>();
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (uiSelected)
            {
                if (image != null) image.color = SelectedFill;
                if (label != null) label.color = SelectedTextColor;
                return;
            }

            if (image != null) image.color = schemeActive ? TabActive : ButtonColor;
            if (label != null) label.color = TextColor;
        }

        public static void ApplyToggleVisual(Button button, TextMeshProUGUI valueLabel, GameObject selected)
        {
            if (button == null) return;
            bool uiSelected = IsUiSelected(button, selected);
            ApplyButtonVisual(button, false, uiSelected);
            Transform titleTf = button.transform.Find("Title");
            TextMeshProUGUI titleLabel = titleTf != null ? titleTf.GetComponent<TextMeshProUGUI>() : null;
            Color text = uiSelected ? SelectedTextColor : TextColor;
            if (titleLabel != null) titleLabel.color = text;
            if (valueLabel != null) valueLabel.color = text;
        }

        public static void ApplyCloseVisual(Button button, Image icon, GameObject selected)
        {
            if (icon == null) return;
            icon.color = IsUiSelected(button, selected) ? AccentColor : TextColor;
        }

        public static void ApplySliderVisual(
            Slider slider,
            Image handle,
            Image fill,
            Image tick,
            TextMeshProUGUI titleLabel,
            TextMeshProUGUI valueLabel)
        {
            if (slider == null) return;
            // slider 非空，故 CurrentSelected 为 null 时结果自然是 false，无需再判 EventSystem
            bool selected = CurrentSelected == slider.gameObject;
            Color text = selected ? AccentColor : SliderLabelIdle;
            if (titleLabel != null) titleLabel.color = text;
            if (valueLabel != null) valueLabel.color = text;
            if (handle != null) handle.color = selected ? HandleSelected : HandleIdle;
            if (fill != null) fill.color = selected ? FillSelected : FillIdle;
            if (tick != null) tick.enabled = selected;
        }

        public static void ResetPanelPose(GameObject panel)
        {
            SetPanelPose(panel, Vector2.zero);
        }

        public static void SetPanelPose(GameObject panel, Vector2 position)
        {
            if (panel == null) return;
            RectTransform rect = panel.GetComponent<RectTransform>();
            if (rect != null)
                rect.anchoredPosition = position;
        }

        public static void BindClick(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        public static Button FindButton(Transform root, string name)
        {
            Transform t = root != null ? root.Find(name) : null;
            return t != null ? t.GetComponent<Button>() : null;
        }

        public static void EnsureButtonLabel(Button button, string text)
        {
            if (button == null || string.IsNullOrEmpty(text)) return;
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.text = text;
        }

        public static TextMeshProUGUI FindTmp(Transform root, string name)
        {
            Transform t = root != null ? root.Find(name) : null;
            return t != null ? t.GetComponent<TextMeshProUGUI>() : null;
        }
    }
}
