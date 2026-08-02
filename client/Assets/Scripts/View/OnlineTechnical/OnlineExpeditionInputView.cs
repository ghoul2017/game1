using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Client.View.OnlineTechnical
{
    /// <summary>
    /// 在线玩法的键盘和单指摇杆输入。这里只产生方向意图，不计算任何权威玩法结果。
    /// </summary>
    public sealed class OnlineExpeditionInputView : MonoBehaviour, IDragHandler, IPointerDownHandler,
        IPointerUpHandler
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

        public static OnlineExpeditionInputView Create(Transform parent)
        {
            GameObject backgroundObject = new GameObject(
                "OnlineExpeditionJoystick",
                typeof(RectTransform),
                typeof(Image));
            backgroundObject.transform.SetParent(parent, false);
            RectTransform background = backgroundObject.GetComponent<RectTransform>();
            background.anchorMin = Vector2.zero;
            background.anchorMax = Vector2.zero;
            background.pivot = new Vector2(0.5f, 0.5f);
            background.anchoredPosition = new Vector2(145.0f, 165.0f);
            background.sizeDelta = new Vector2(190.0f, 190.0f);
            backgroundObject.GetComponent<Image>().color = new Color(0.9f, 0.95f, 1.0f, 0.2f);

            GameObject handleObject = new GameObject("Handle", typeof(RectTransform), typeof(Image));
            handleObject.transform.SetParent(background, false);
            RectTransform handle = handleObject.GetComponent<RectTransform>();
            handle.anchorMin = new Vector2(0.5f, 0.5f);
            handle.anchorMax = new Vector2(0.5f, 0.5f);
            handle.pivot = new Vector2(0.5f, 0.5f);
            handle.sizeDelta = new Vector2(76.0f, 76.0f);
            handleObject.GetComponent<Image>().color = new Color(0.85f, 0.95f, 1.0f, 0.65f);

            OnlineExpeditionInputView input = backgroundObject.AddComponent<OnlineExpeditionInputView>();
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
            bool hasPoint = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _background,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint);
            if (!hasPoint)
            {
                return;
            }

            Vector2 halfSize = _background.rect.size * 0.5f;
            Vector2 direction = new Vector2(localPoint.x / halfSize.x, localPoint.y / halfSize.y);
            direction = Vector2.ClampMagnitude(direction, 1.0f);
            _pointerDirection = direction.magnitude < DeadZone ? Vector2.zero : direction;
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
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                horizontal -= 1.0f;
            }

            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                horizontal += 1.0f;
            }

            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
            {
                vertical -= 1.0f;
            }

            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
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
