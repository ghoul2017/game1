using UnityEngine;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    internal static class OnlineTechnicalUiFactory
    {
        private static Font _font;

        public static GameObject CreatePanel(string objectName, Transform parent, Color color)
        {
            GameObject panelObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            panelObject.GetComponent<Image>().color = color;
            return panelObject;
        }

        public static Text CreateText(
            string objectName,
            Transform parent,
            string content,
            int fontSize,
            TextAnchor alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            Text text = textObject.GetComponent<Text>();
            text.font = GetFont();
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = content;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(
            string objectName,
            Transform parent,
            string label,
            Color color)
        {
            GameObject buttonObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            buttonObject.GetComponent<Image>().color = color;
            Button button = buttonObject.GetComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = color * 1.18f;
            colors.pressedColor = color * 0.78f;
            colors.disabledColor = new Color(color.r, color.g, color.b, 0.35f);
            button.colors = colors;

            Text labelText = CreateText("Label", buttonObject.transform, label, 24, TextAnchor.MiddleCenter);
            Stretch(labelText.rectTransform, 4.0f);
            return button;
        }

        public static InputField CreateIntegerInput(
            string objectName,
            Transform parent,
            string placeholderText)
        {
            GameObject inputObject = new GameObject(
                objectName,
                typeof(RectTransform),
                typeof(Image),
                typeof(InputField));
            inputObject.transform.SetParent(parent, false);
            inputObject.GetComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1.0f);

            Text valueText = CreateText("Value", inputObject.transform, string.Empty, 23, TextAnchor.MiddleLeft);
            Stretch(valueText.rectTransform, 12.0f);
            Text placeholder = CreateText(
                "Placeholder",
                inputObject.transform,
                placeholderText,
                21,
                TextAnchor.MiddleLeft);
            placeholder.color = new Color(1.0f, 1.0f, 1.0f, 0.45f);
            Stretch(placeholder.rectTransform, 12.0f);

            InputField input = inputObject.GetComponent<InputField>();
            input.textComponent = valueText;
            input.placeholder = placeholder;
            input.contentType = InputField.ContentType.IntegerNumber;
            input.characterLimit = 10;
            return input;
        }

        public static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        public static void SetAnchoredRect(
            RectTransform rect,
            Vector2 anchor,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect, float padding)
        {
            SetRect(
                rect,
                Vector2.zero,
                Vector2.one,
                new Vector2(padding, padding),
                new Vector2(-padding, -padding));
        }

        private static Font GetFont()
        {
            if (_font == null)
            {
                _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _font;
        }
    }
}
