using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.Expedition
{
    /// <summary>
    /// 汇总键盘方向和单指虚拟摇杆方向，不参与局内规则计算。
    /// </summary>
    public sealed class ExpeditionInputView : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        private const float DeadZone = 0.15f;
        private const float MaximumHandleRadius = 54.0f;

        private RectTransform _background;
        private RectTransform _handle;
        private Vector2 _keyboardDirection;
        private Vector2 _pointerDirection;

        public Vector2 Direction => _keyboardDirection.sqrMagnitude > 0.0f
            ? _keyboardDirection
            : _pointerDirection;

        public static ExpeditionInputView Create(Transform parent)
        {
            GameObject backgroundObject = new GameObject("ExpeditionJoystick", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.zero;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = new Vector2(130.0f, 160.0f);
            background.sizeDelta = new Vector2(180.0f, 180.0f);

            Image backgroundImage = backgroundObject.GetComponent<Image>();
            backgroundImage.color = new Color(0.9f, 0.95f, 1.0f, 0.18f);
            backgroundImage.raycastTarget = true;

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(background, false);
            RectTransform handle = handleObject.GetComponent<RectTransform>();
            handle.anchorMin = new Vector2(0.5f, 0.5f);
            handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(72.0f, 72.0f);
            handleObject.GetComponent<Image>().color = new Color(0.9f, 0.95f, 1.0f, 0.55f);

            ExpeditionInputView input = backgroundObject.AddComponent<ExpeditionInputView>();
            input._background = background;
            input._handle = handle;
            return input;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            Vector2 localPoint;
            bool hasPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background,
                eventData.position,
                eventData.pressEventCamera,
                out localPoint);
            if (!hasPoint)
            {
                return;
            }

            Vector2 halfSize = _background.rect.size * 0.5f;
            Vector2 rawDirection = new Vector2(localPoint.x / halfSize.x, localPoint.y / halfSize.y);
            rawDirection = Vector2.ClampMagnitude(rawDirection, 1.0f);
            _pointerDirection = rawDirection.magnitude < DeadZone ? Vector2.zero : rawDirection;
            _handle.anchoredPosition = _pointerDirection * MaximumHandleRadius;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetPointerDirection();
        }

        private void Update()
        {
            float horizontal = 0.0f;
            float vertical = 0.0f;

            if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1.0f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1.0f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow))
            {
                vertical -= 1.0f;
            }

            if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow))
            {
                vertical += 1.0f;
            }

            _keyboardDirection = Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1.0f);
        }

        private void OnDisable()
        {
            ResetDirections();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetDirections();
            }
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                ResetDirections();
            }
        }

        private void ResetDirections()
        {
            _keyboardDirection = Vector2.zero;
            ResetPointerDirection();
        }

        private void ResetPointerDirection()
        {
            _pointerDirection = Vector2.zero;
            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }
        }
    }
}
