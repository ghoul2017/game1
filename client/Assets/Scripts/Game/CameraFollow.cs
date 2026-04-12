using UnityEngine;

namespace Client.Game
{
    public class CameraFollow : MonoBehaviour
    {
        public Transform target;
        public Vector3 offset = new Vector3(0f, 15f, -15f);
        public float followSpeed = 5f;

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 targetPos = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, targetPos, followSpeed * Time.deltaTime);
        }
    }
}
