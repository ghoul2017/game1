using UnityEngine;
using UnityEngine.SceneManagement;
using Client.Core;
using Client.Network;
using GameShared.Network;
using GameShared.Protos;

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
            RegisterAoiHandlers();
            RegisterReconnectEvents();
            UIManager.Instance.ShowPanel<MainPanel>();
        }

        private void OnDestroy()
        {
            UnregisterAoiHandlers();
            UnregisterReconnectEvents();
            OtherPlayerManager.Instance.ClearAll();
        }

        private void InitializeScene()
        {
            MapConfig mapConfig;
            int spawnX, spawnY;

            // 优先使用服务端进图数据
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                mapConfig = DefaultMapConfigs.GetById(mapData.MapCfgId);
                spawnX = mapData.SpawnX;
                spawnY = mapData.SpawnY;
                Debug.Log(string.Format("[GameSceneEntry] 使用服务端数据: Map={0} Spawn=({1},{2})",
                    mapConfig.Name, spawnX, spawnY));
            }
            else
            {
                // 本地回退（开发/调试用）
                mapConfig = DefaultMapConfigs.GetById(1);
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

            // 创建玩家（使用服务端返回的出生点）
            var playerObj = new GameObject("Player");
            playerObj.transform.position = MapData.GridToWorld(spawnX, spawnY);

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

        private void RegisterAoiHandlers()
        {
            var dispatcher = NetClient.Instance != null ? NetClient.Instance.Dispatcher : null;
            if (dispatcher == null)
            {
                Debug.LogWarning("[GameSceneEntry] NetClient 或 Dispatcher 不可用，跳过 AOI 注册");
                return;
            }

            dispatcher.Register<MapObjectsNtf>(MsgId.MapObjectsNtf, OnMapObjectsNtf);
            dispatcher.Register<MapPlayerEnterNtf>(MsgId.MapPlayerEnterNtf, OnMapPlayerEnterNtf);
            dispatcher.Register<MapPlayerLeaveNtf>(MsgId.MapPlayerLeaveNtf, OnMapPlayerLeaveNtf);
            dispatcher.Register<MapPlayerMoveNtf>(MsgId.MapPlayerMoveNtf, OnMapPlayerMoveNtf);
            dispatcher.Register<MapMoveAck>(MsgId.MapMoveAck, OnMapMoveAck);

            Debug.Log("[GameSceneEntry] AOI 视野同步 + 移动同步处理器已注册");
        }

        private void UnregisterAoiHandlers()
        {
            var dispatcher = NetClient.Instance != null ? NetClient.Instance.Dispatcher : null;
            if (dispatcher == null) return;

            dispatcher.Unregister(MsgId.MapObjectsNtf);
            dispatcher.Unregister(MsgId.MapPlayerEnterNtf);
            dispatcher.Unregister(MsgId.MapPlayerLeaveNtf);
            dispatcher.Unregister(MsgId.MapPlayerMoveNtf);
            dispatcher.Unregister(MsgId.MapMoveAck);
        }

        private void OnMapObjectsNtf(MapObjectsNtf ntf)
        {
            OtherPlayerManager.Instance.SpawnPlayers(ntf.Objects);
        }

        private void OnMapPlayerEnterNtf(MapPlayerEnterNtf ntf)
        {
            OtherPlayerManager.Instance.SpawnPlayer(ntf.Player);
        }

        private void OnMapPlayerLeaveNtf(MapPlayerLeaveNtf ntf)
        {
            OtherPlayerManager.Instance.RemovePlayer(ntf.RoleId);
        }

        private void OnMapPlayerMoveNtf(MapPlayerMoveNtf ntf)
        {
            OtherPlayerManager.Instance.MovePlayer(ntf.RoleId, ntf.X, ntf.Y);
        }

        private void OnMapMoveAck(MapMoveAck ack)
        {
            if (ack.Code != MapErrorCode.Ok)
            {
                // 服务端校验失败，回退到服务端位置
                if (PlayerController != null)
                {
                    PlayerController.CorrectPosition(ack.X, ack.Y);
                    Debug.Log(string.Format("[GameSceneEntry] 移动被服务端纠正: ({0},{1})", ack.X, ack.Y));
                }
            }
        }

        // ==================== Reconnect Events ====================

        private void RegisterReconnectEvents()
        {
            var netClient = NetClient.Instance;
            if (netClient == null) return;

            netClient.OnReconnected += HandleReconnected;
            netClient.OnReconnectFailed += HandleReconnectFailed;
            netClient.OnKicked += HandleKicked;
            Debug.Log("[GameSceneEntry] 重连事件已注册");
        }

        private void UnregisterReconnectEvents()
        {
            var netClient = NetClient.Instance;
            if (netClient == null) return;

            netClient.OnReconnected -= HandleReconnected;
            netClient.OnReconnectFailed -= HandleReconnectFailed;
            netClient.OnKicked -= HandleKicked;
        }

        private void HandleReconnected()
        {
            Debug.Log("[GameSceneEntry] 重连成功，重新进入地图");

            // 清除旧的视野数据
            OtherPlayerManager.Instance.ClearAll();

            // 重新发送进图请求，获取最新AOI数据
            var mapData = GameData.CurrentMap;
            if (mapData != null && NetClient.Instance != null)
            {
                var req = new EnterMapReq();
                req.MapCfgId = mapData.MapCfgId;
                req.Line = mapData.Line;
                NetClient.Instance.Send(req);
            }
        }

        private void HandleReconnectFailed(string message)
        {
            Debug.Log("[GameSceneEntry] 重连失败: " + message + "，返回登录");
            GoBackToLogin();
        }

        private void HandleKicked(int reason)
        {
            Debug.Log("[GameSceneEntry] 被踢下线，reason=" + reason);
            GoBackToLogin();
        }

        private void GoBackToLogin()
        {
            OtherPlayerManager.Instance.ClearAll();
            SceneManager.LoadScene("SampleScene");
        }
    }
}
