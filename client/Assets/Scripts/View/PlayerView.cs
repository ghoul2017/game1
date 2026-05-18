using UnityEngine;
using UnityEngine.UI;

namespace Client.View
{
    public class PlayerView : MonoBehaviour
    {
        private Canvas nameplateCanvas;
        private Text nameText;
        private RectTransform hpFillRect;
        private int _maxHp = 100;

        public void CreateModel(string roleName, int level)
        {
            CreateBody();
            CreateHead();
            CreateNameplate(roleName, level);
        }

        public void UpdateHp(int hp, int maxHp)
        {
            _maxHp = maxHp > 0 ? maxHp : 100;
            if (hpFillRect != null)
            {
                float ratio = (float)hp / _maxHp;
                hpFillRect.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
            }
        }

        private void CreateBody()
        {
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.parent = transform;
            body.transform.localPosition = new Vector3(0, 0.5f, 0);
            body.transform.localScale = new Vector3(0.6f, 0.5f, 0.6f);

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.2f, 0.4f, 0.8f);
            body.GetComponent<Renderer>().material = mat;
        }

        private void CreateHead()
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            head.transform.parent = transform;
            head.transform.localPosition = new Vector3(0, 1.4f, 0);
            head.transform.localScale = Vector3.one * 0.35f;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = new Color(0.9f, 0.75f, 0.6f);
            head.GetComponent<Renderer>().material = mat;
            Destroy(head.GetComponent<SphereCollider>());
        }

        private void CreateNameplate(string roleName, int level)
        {
            var canvasObj = new GameObject("Nameplate");
            canvasObj.transform.parent = transform;
            canvasObj.transform.localPosition = new Vector3(0, 2.2f, 0);

            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 10;

            var scaler = canvasObj.AddComponent<CanvasScaler>();

            nameplateCanvas = canvas;

            // 背景
            var bgObj = new GameObject("Bg");
            bgObj.transform.SetParent(canvasObj.transform, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.sizeDelta = new Vector2(160, 48);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.5f);

            // 名字文本
            var textObj = new GameObject("NameText");
            textObj.transform.SetParent(canvasObj.transform, false);
            var textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0.5f);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            nameText = textObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.text = $"{roleName} Lv.{level}";
            nameText.fontSize = 14;
            nameText.color = new Color(1f, 0.85f, 0.3f);
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.fontStyle = FontStyle.Bold;

            // HP条背景
            var hpBgObj = new GameObject("HpBg");
            hpBgObj.transform.SetParent(canvasObj.transform, false);
            var hpBgRect = hpBgObj.AddComponent<RectTransform>();
            hpBgRect.anchorMin = new Vector2(0.5f, 0);
            hpBgRect.anchorMax = new Vector2(0.5f, 0);
            hpBgRect.pivot = new Vector2(0.5f, 0);
            hpBgRect.sizeDelta = new Vector2(140, 10);
            hpBgObj.AddComponent<Image>().color = new Color(0.2f, 0.1f, 0.1f, 0.8f);

            // HP条填充
            var hpFillObj = new GameObject("HpFill");
            hpFillObj.transform.SetParent(hpBgObj.transform, false);
            hpFillRect = hpFillObj.AddComponent<RectTransform>();
            hpFillRect.anchorMin = Vector2.zero;
            hpFillRect.anchorMax = Vector2.one;
            hpFillRect.offsetMin = Vector2.zero;
            hpFillRect.offsetMax = Vector2.zero;
            hpFillObj.AddComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f);

            // 设置 Canvas 大小
            var canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(160, 48);

            // 缩放：让名字板在世界空间中大小合适
            canvasObj.transform.localScale = Vector3.one * 0.015f;
        }

        public void UpdateNameplate(string text)
        {
            if (nameText != null) nameText.text = text;
        }

        private void LateUpdate()
        {
            // Billboard：名字板始终面向相机
            if (nameplateCanvas != null && Camera.main != null)
            {
                nameplateCanvas.transform.rotation = Camera.main.transform.rotation;
            }
        }
    }
}
