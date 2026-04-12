using UnityEngine;
using Client.Core;

namespace Client.Game
{
    public class GameSceneEntry : MonoBehaviour
    {
        public static PlayerController PlayerController { get; private set; }
        public static MapGround MapGround { get; private set; }

        private void Start()
        {
            Debug.Log("[GameSceneEntry] 初始化游戏场景");

            // UIManager 因 DontDestroyOnLoad 在场景切换后保留
            UIManager.EnsureCreated();

            InitializeScene();
            UIManager.Instance.ShowPanel<MainPanel>();
        }

        private void InitializeScene()
        {
            // 默认加载新手村
            var mapConfig = DefaultMapConfigs.GetById(1);

            // 创建地图
            var mapObj = new GameObject("Map");
            var mapGround = mapObj.AddComponent<MapGround>();
            mapGround.Generate(mapConfig);
            MapGround = mapGround;

            // 创建玩家
            var playerObj = new GameObject("Player");
            playerObj.transform.position = MapData.GridToWorld(mapConfig.BirthX, mapConfig.BirthY);

            var controller = playerObj.AddComponent<PlayerController>();
            controller.Init(mapConfig, mapConfig.BirthX, mapConfig.BirthY);
            PlayerController = controller;

            var view = playerObj.AddComponent<PlayerView>();
            var role = GameData.CurrentRole;
            view.CreateModel(role?.Name ?? "勇者", role?.Level ?? 1);

            // 设置相机跟随
            var cam = Camera.main;
            if (cam != null)
            {
                cam.transform.position = playerObj.transform.position + new Vector3(0f, 15f, -15f);
                cam.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
                cam.orthographic = true;
                cam.orthographicSize = 8f;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 200f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.12f, 0.15f, 0.22f);

                var follow = cam.gameObject.AddComponent<CameraFollow>();
                follow.target = playerObj.transform;
            }
        }
    }
}
