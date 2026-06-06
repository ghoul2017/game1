using UnityEngine;
using UnityEngine.UI;

namespace Client.View
{
    public static class UIBuilder
    {
        // RPG 风格配色
        public static readonly Color DarkBg = new Color(0.102f, 0.102f, 0.18f, 0.95f);
        public static readonly Color PanelBg = new Color(0.06f, 0.06f, 0.14f, 0.92f);
        public static readonly Color Gold = new Color(0.886f, 0.718f, 0.078f);
        public static readonly Color GoldDark = new Color(0.6f, 0.48f, 0.05f);
        public static readonly Color ButtonNormal = new Color(0.15f, 0.12f, 0.25f, 0.9f);
        public static readonly Color ButtonHover = new Color(0.25f, 0.2f, 0.38f, 0.95f);
        public static readonly Color TextWhite = Color.white;
        public static readonly Color TextGray = new Color(0.7f, 0.7f, 0.7f);
        public static readonly Color TextDim = new Color(0.45f, 0.45f, 0.5f);
        public static readonly Color InputBg = new Color(0.08f, 0.08f, 0.15f, 0.9f);
        public static readonly Color AccentBlue = new Color(0.2f, 0.4f, 0.8f, 0.9f);
        public static readonly Color SectionBg = new Color(0.08f, 0.08f, 0.18f, 0.6f);
        public static readonly Color DividerColor = new Color(0.886f, 0.718f, 0.078f, 0.3f);

        private const string BuiltinDefaultFontPath = "LegacyRuntime.ttf";
        private static Font _defaultFont;
        public static Font DefaultFont =>
            _defaultFont != null ? _defaultFont : _defaultFont = Resources.GetBuiltinResource<Font>(BuiltinDefaultFontPath);

        public static Image CreateFullScreenBg(Transform parent, Color color)
        {
            var obj = CreateUI(parent, "Bg");
            SetStretch(obj.GetComponent<RectTransform>());
            var img = obj.AddComponent<Image>();
            img.color = color;
            return img;
        }

        public static GameObject CreatePanel(Transform parent, string name, float width, float height, Color bgColor)
        {
            var obj = CreateUI(parent, name);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            var img = obj.AddComponent<Image>();
            img.color = bgColor;
            return obj;
        }

        public static Text CreateText(Transform parent, string name, string text,
            float fontSize = 24, Color? color = null, TextAnchor alignment = TextAnchor.MiddleCenter,
            FontStyle style = FontStyle.Normal)
        {
            var obj = CreateUI(parent, name);
            var txt = obj.AddComponent<Text>();
            txt.text = text;
            txt.fontSize = (int)fontSize;
            txt.color = color ?? TextWhite;
            txt.alignment = alignment;
            txt.fontStyle = style;
            txt.font = DefaultFont;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button CreateButton(Transform parent, string name, string text,
            float width, float height, float fontSize = 24, Color? bgColor = null, Color? textColor = null)
        {
            var obj = CreateUI(parent, name);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);

            var img = obj.AddComponent<Image>();
            img.color = bgColor ?? ButtonNormal;

            var btn = obj.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
            colors.fadeDuration = 0.1f;
            btn.colors = colors;

            var tmp = CreateText(obj.transform, "Text", text, fontSize, textColor ?? Gold);
            var textRect = tmp.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            return btn;
        }

        public static InputField CreateInputField(Transform parent, string name, string placeholder,
            float width, float height, float fontSize = 22)
        {
            var obj = CreateUI(parent, name);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            obj.AddComponent<Image>().color = InputBg;

            var inputField = obj.AddComponent<InputField>();

            // Text
            var textObj = CreateUI(obj.transform, "Text");
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 5);
            textRect.offsetMax = new Vector2(-10, -5);
            var txt = textObj.AddComponent<Text>();
            txt.fontSize = (int)fontSize;
            txt.color = TextWhite;
            txt.alignment = TextAnchor.MiddleLeft;
            txt.font = DefaultFont;

            // Placeholder
            var phObj = CreateUI(obj.transform, "Placeholder");
            var phRect = phObj.GetComponent<RectTransform>();
            phRect.anchorMin = Vector2.zero;
            phRect.anchorMax = Vector2.one;
            phRect.offsetMin = new Vector2(10, 5);
            phRect.offsetMax = new Vector2(-10, -5);
            var ph = phObj.AddComponent<Text>();
            ph.fontSize = (int)fontSize;
            ph.color = TextGray;
            ph.alignment = TextAnchor.MiddleLeft;
            ph.text = placeholder;
            ph.font = DefaultFont;

            inputField.textComponent = txt;
            inputField.placeholder = ph;
            return inputField;
        }

        public static Slider CreateSlider(Transform parent, string name, float width, float height)
        {
            var obj = CreateUI(parent, name);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            obj.AddComponent<Image>().color = new Color(0.1f, 0.1f, 0.2f, 0.8f);

            var slider = obj.AddComponent<Slider>();
            slider.interactable = false;

            var fillArea = CreateUI(obj.transform, "Fill Area");
            var faRect = fillArea.GetComponent<RectTransform>();
            faRect.anchorMin = Vector2.zero;
            faRect.anchorMax = Vector2.one;
            faRect.offsetMin = Vector2.zero;
            faRect.offsetMax = Vector2.zero;

            var fillObj = CreateUI(fillArea.transform, "Fill");
            var fRect = fillObj.GetComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = Vector2.zero;
            fRect.offsetMax = Vector2.zero;
            var fillImg = fillObj.AddComponent<Image>();
            fillImg.color = Gold;

            slider.fillRect = fRect;
            slider.targetGraphic = fillImg;
            slider.value = 0f;
            return slider;
        }

        public static Image CreateDivider(Transform parent, string name, float width, float thickness = 2f)
        {
            var obj = CreateUI(parent, name);
            var rect = obj.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, thickness);
            var img = obj.AddComponent<Image>();
            img.color = DividerColor;
            img.raycastTarget = false;
            return img;
        }

        public static GameObject CreateTitleBar(Transform parent, string name, string title, float width, float height)
        {
            var bar = CreateUI(parent, name);
            var barRect = bar.GetComponent<RectTransform>();
            barRect.sizeDelta = new Vector2(width, height);
            bar.AddComponent<Image>().color = new Color(0.12f, 0.1f, 0.22f, 0.95f);

            var div = CreateDivider(bar.transform, "Div", width, 2f);
            var divRect = div.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0, 0);
            divRect.anchorMax = new Vector2(1, 0);
            divRect.pivot = new Vector2(0.5f, 0);
            divRect.offsetMin = Vector2.zero;
            divRect.offsetMax = Vector2.zero;

            var txt = CreateText(bar.transform, "TitleText", title, 28, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            var tRect = txt.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.offsetMin = Vector2.zero;
            tRect.offsetMax = Vector2.zero;
            return bar;
        }

        public static GameObject CreateSection(Transform parent, string name, string label, float width, float height)
        {
            var section = CreateUI(parent, name);
            var sRect = section.GetComponent<RectTransform>();
            sRect.sizeDelta = new Vector2(width, height);
            section.AddComponent<Image>().color = SectionBg;

            var tmp = CreateText(section.transform, "Label", label, 14, TextDim, TextAnchor.MiddleLeft);
            var lRect = tmp.GetComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0, 1);
            lRect.anchorMax = new Vector2(1, 1);
            lRect.pivot = new Vector2(0, 1);
            lRect.offsetMin = new Vector2(8, -18);
            lRect.offsetMax = new Vector2(-8, -4);
            return section;
        }

        public static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public static (GameObject root, Transform content) CreateBorderedPanel(Transform parent, string name,
            float width, float height, Color bgColor, Color borderColor, float borderWidth = 2f)
        {
            var outer = CreateUI(parent, name);
            var outerRect = outer.GetComponent<RectTransform>();
            outerRect.sizeDelta = new Vector2(width + borderWidth * 2, height + borderWidth * 2);
            outer.AddComponent<Image>().color = borderColor;

            var inner = CreateUI(outer.transform, "Inner");
            var innerRect = inner.GetComponent<RectTransform>();
            innerRect.anchorMin = Vector2.zero;
            innerRect.anchorMax = Vector2.one;
            innerRect.offsetMin = new Vector2(borderWidth, borderWidth);
            innerRect.offsetMax = new Vector2(-borderWidth, -borderWidth);
            inner.AddComponent<Image>().color = bgColor;

            return (outer, inner.transform);
        }

        private static GameObject CreateUI(Transform parent, string name)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            obj.AddComponent<RectTransform>();
            return obj;
        }
    }
}
