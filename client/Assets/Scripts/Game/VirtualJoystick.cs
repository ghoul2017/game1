using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Game
{
    public class VirtualJoystick : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private RectTransform handle;
        private float maxRadius = 50f;
        private Vector2 inputVector;

        public Vector2 InputVector => inputVector;

        public static VirtualJoystick Create(Transform parent)
        {
            // 底座
            var bgObj = new GameObject("JoystickBg");
            bgObj.transform.SetParent(parent, false);
            var bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0);
            bgRect.anchorMax = new Vector2(0, 0);
            bgRect.pivot = new Vector2(0.5f, 0.5f);
            bgRect.anchoredPosition = new Vector2(150, 200);
            bgRect.sizeDelta = new Vector2(160, 160);
            var bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(1, 1, 1, 0.15f);
            bgImage.sprite = CreateCircleSprite(80, new Color(1, 1, 1, 0.15f));
            bgImage.raycastTarget = true;

            // 摇杆头
            var handleObj = new GameObject("JoystickHandle");
            handleObj.transform.SetParent(bgObj.transform, false);
            var handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(60, 60);
            var handleImage = handleObj.AddComponent<Image>();
            handleImage.color = new Color(1, 1, 1, 0.4f);
            handleImage.sprite = CreateCircleSprite(30, new Color(1, 1, 1, 0.4f));

            // 挂载组件
            var joystick = bgObj.AddComponent<VirtualJoystick>();
            joystick.handle = handleRect;
            return joystick;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            RectTransform bgRect = transform as RectTransform;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                bgRect, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                localPoint.x = localPoint.x / bgRect.sizeDelta.x * 2f;
                localPoint.y = localPoint.y / bgRect.sizeDelta.y * 2f;

                inputVector = localPoint.magnitude > 1f ? localPoint.normalized : localPoint;

                handle.anchoredPosition = new Vector2(
                    inputVector.x * maxRadius,
                    inputVector.y * maxRadius);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            inputVector = Vector2.zero;
            handle.anchoredPosition = Vector2.zero;
        }

        private static Sprite CreateCircleSprite(int radius, Color color)
        {
            int size = radius * 2;
            var tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];
            float center = radius;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    pixels[y * size + x] = dist <= radius ? color : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
