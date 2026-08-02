using UnityEngine;
using UnityEngine.EventSystems;

namespace Client.View.OnlineTechnical
{
    public sealed class OnlineDirectionButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler
    {
        private Vector2 _direction;
        private OnlineExpeditionPanel _owner;

        public void Initialize(OnlineExpeditionPanel owner, Vector2 direction)
        {
            _owner = owner;
            _direction = direction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _owner?.BeginMovement(_direction);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _owner?.EndMovement(_direction);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _owner?.EndMovement(_direction);
        }
    }
}
