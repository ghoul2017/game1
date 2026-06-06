using UnityEngine;
using Client.Logic;
using Shared.Common.Config;
using Shared.Core.Config;

namespace Client.View
{
    public class GameSceneEntry : MonoBehaviour
    {
        public static GameSceneEntry Instance { get; private set; }
        public static PlayerController PlayerController { get; private set; }
        public static MapGround MapGround { get; internal set; }
        private static MainPanel _mainPanel;

        private void Start()
        {
            Instance = this;
            Debug.Log("[GameSceneEntry] 初始化游戏场景");

            // UIManager 因 DontDestroyOnLoad 在场景切换后保留
            UIManager.EnsureCreated();

            InitializeScene();

            MapController.Instance.MarkMapViewReady();

            _mainPanel = UIManager.Instance.ShowPanel<MainPanel>();
        }

        private void OnDestroy()
        {
            MapController.Instance.MarkMapViewClosed();

            OtherPlayerManager.Instance.ClearAll();
            MonsterEntityManager.Instance.ClearAll();
            _mainPanel = null;
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            SelectTargetUnderCursor();
        }

        private void SelectTargetUnderCursor()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Skip if clicking UI
                if (UnityEngine.EventSystems.EventSystem.current != null
                    && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                var cam = Camera.main;
                if (cam == null)
                    return;

                var ray = cam.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 100f))
                {
                    // 优先选中怪物/NPC
                    var monsterView = hit.collider.GetComponentInParent<MonsterView>();
                    if (monsterView != null)
                    {
                        GameData.SelectedTargetId = monsterView.ObjectId;
                        GameData.SelectedTargetName = monsterView.name;
                        if (_mainPanel != null)
                            _mainPanel.UpdateTargetDisplay(monsterView.ObjectId, monsterView.name);
                        Debug.Log("[GameSceneEntry] 选中怪物: " + monsterView.name + " Id=" + monsterView.ObjectId);
                        return;
                    }

                    // 其次选中其他玩家
                    var otherView = hit.collider.GetComponentInParent<OtherPlayerView>();
                    if (otherView != null)
                    {
                        GameData.SelectedTargetId = otherView.RoleId;
                        GameData.SelectedTargetName = otherView.name;
                        if (_mainPanel != null)
                            _mainPanel.UpdateTargetDisplay(otherView.RoleId, otherView.name);
                        Debug.Log("[GameSceneEntry] 选中目标: " + otherView.name);
                        return;
                    }
                }

                // Clicked on empty space - deselect
                GameData.SelectedTargetId = 0;
                GameData.SelectedTargetName = "";
                if (_mainPanel != null)
                    _mainPanel.UpdateTargetDisplay(0, "");
            }
        }

        private void InitializeScene()
        {
            MapConfig mapConfig;
            int spawnX, spawnY;

            // 优先使用服务端进图数据
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                mapConfig = ConfigManager.Get<MapConfig>(mapData.MapCfgId);
                spawnX = mapData.SpawnX;
                spawnY = mapData.SpawnY;
                Debug.Log(string.Format("[GameSceneEntry] 使用服务端数据: Map={0} Spawn=({1},{2})",
                    mapConfig.Name, spawnX, spawnY));
            }
            else
            {
                // 本地回退（开发/调试用）
                mapConfig = ConfigManager.Get<MapConfig>(1);
                spawnX = mapConfig.BirthX;
                spawnY = mapConfig.BirthY;
                Debug.Log("[GameSceneEntry] 无服务端数据，使用本地默认配置");
            }

            // 创建地图
            var mapObj = new GameObject("Map");
            var mapGround = mapObj.AddComponent<MapGround>();
            mapGround.Generate(mapConfig);
            MapGround = mapGround;

            // 创建其他玩家容器
            var otherPlayersObj = new GameObject("OtherPlayers");
            OtherPlayerManager.Instance.Init(otherPlayersObj.transform);

            // 创建怪物/NPC容器
            var monstersObj = new GameObject("Monsters");
            MonsterEntityManager.Instance.Init(monstersObj.transform);

            // 创建玩家（使用服务端返回的出生点）
            var playerObj = new GameObject("Player");
            (float px, float py, float pz) = Client.Logic.MapData.GridToWorld(spawnX, spawnY);
            playerObj.transform.position = new Vector3(px, py, pz);

            var controller = playerObj.AddComponent<PlayerController>();
            controller.Init(mapConfig, spawnX, spawnY);
            PlayerController = controller;

            var view = playerObj.AddComponent<PlayerView>();
            var role = GameData.CurrentRole;
            view.CreateModel(role != null ? role.Name : "勇者", role != null ? role.Level : 1);

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
