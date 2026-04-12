using UnityEngine;

namespace Client.Game
{
    public class PlayerController : MonoBehaviour
    {
        public float moveSpeed = 5f;
        public int CurrentGridX { get; private set; }
        public int CurrentGridY { get; private set; }

        private MapConfig currentMap;

        [HideInInspector]
        public Vector2 joystickInput;

        public void Init(MapConfig mapConfig, int gridX, int gridY)
        {
            currentMap = mapConfig;
            transform.position = MapData.GridToWorld(gridX, gridY);
            CurrentGridX = gridX;
            CurrentGridY = gridY;
        }

        private void Update()
        {
            if (currentMap == null) return;

            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");

            // 合并键盘和摇杆输入
            float inputX = Mathf.Clamp(h + joystickInput.x, -1f, 1f);
            float inputZ = Mathf.Clamp(v + joystickInput.y, -1f, 1f);

            Vector3 moveDir = new Vector3(inputX, 0, inputZ);

            if (moveDir.sqrMagnitude > 0.01f)
            {
                // 面向移动方向
                transform.rotation = Quaternion.LookRotation(moveDir);

                Vector3 newPos = transform.position + moveDir * moveSpeed * Time.deltaTime;

                // 边界约束
                newPos.x = Mathf.Clamp(newPos.x, 1f, currentMap.Width - 1f);
                newPos.z = Mathf.Clamp(newPos.z, 1f, currentMap.Height - 1f);
                newPos.y = 0;

                transform.position = newPos;

                // 更新格子坐标
                var (gx, gy) = MapData.WorldToGrid(transform.position);
                CurrentGridX = gx;
                CurrentGridY = gy;
            }
        }
    }
}
