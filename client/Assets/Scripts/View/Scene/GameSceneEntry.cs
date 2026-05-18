using UnityEngine;
using UnityEngine.SceneManagement;
using Client.Logic;
using Client.View;
using Shared.Common;
using Shared.Core.Network;
using Shared.Common.Protos;
using Shared.Common.Config;
using Shared.Core.Config;

namespace Client.View
{
    public class GameSceneEntry : MonoBehaviour
    {
        public static PlayerController PlayerController { get; private set; }
        public static MapGround MapGround { get; private set; }
        private static MainPanel _mainPanel;

        private void Start()
        {
            Debug.Log("[GameSceneEntry] 初始化游戏场景");

            // UIManager 因 DontDestroyOnLoad 在场景切换后保留
            UIManager.EnsureCreated();

            InitializeScene();
            RegisterReconnectEvents();
            _mainPanel = UIManager.Instance.ShowPanel<MainPanel>();
        }

        private void OnDestroy()
        {
            UnregisterReconnectEvents();
            OtherPlayerManager.Instance.ClearAll();
            MonsterEntityManager.Instance.ClearAll();
            _mainPanel = null;
        }

        private void Update()
        {
            HandleTargetSelection();
        }

        private void HandleTargetSelection()
        {
            if (Input.GetMouseButtonDown(0))
            {
                // Skip if clicking UI
                if (UnityEngine.EventSystems.EventSystem.current != null
                    && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    return;

                var cam = Camera.main;
                if (cam == null) return;

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

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectsNtf(TcpSession session, MapObjectsNtf ntf)
        {
            if (ntf.Objects == null) return;
            foreach (var obj in ntf.Objects)
            {
                // ObjectType: 1=Player, 2=NPC, 3=Monster
                if (obj.ObjectType == MapObjectType.Player)
                {
                    OtherPlayerManager.Instance.SpawnPlayer(obj);
                }
                else if (obj.ObjectType == MapObjectType.Monster || obj.ObjectType == MapObjectType.Npc)
                {
                    MonsterEntityManager.Instance.Spawn(obj.ObjectId, obj.CfgId, obj.ObjectType, obj.Name, obj.X, obj.Y);
                }
            }
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerEnterNtf(TcpSession session, MapPlayerEnterNtf ntf)
        {
            OtherPlayerManager.Instance.SpawnPlayer(ntf.Player);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerLeaveNtf(TcpSession session, MapPlayerLeaveNtf ntf)
        {
            OtherPlayerManager.Instance.RemovePlayer(ntf.RoleId);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerMoveNtf(TcpSession session, MapPlayerMoveNtf ntf)
        {
            OtherPlayerManager.Instance.MovePlayer(ntf.RoleId, ntf.X, ntf.Y);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectEnterNtf(TcpSession session, MapObjectEnterNtf ntf)
        {
            MonsterEntityManager.Instance.Spawn(ntf.ObjectId, ntf.CfgId, ntf.ObjectType, ntf.Name, ntf.X, ntf.Y);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectLeaveNtf(TcpSession session, MapObjectLeaveNtf ntf)
        {
            MonsterEntityManager.Instance.Remove(ntf.ObjectId);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectMoveNtf(TcpSession session, MapObjectMoveNtf ntf)
        {
            MonsterEntityManager.Instance.Move(ntf.ObjectId, ntf.X, ntf.Y);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapMoveAck(TcpSession session, MapMoveAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                // 服务端校验失败，回退到服务端位置
                if (PlayerController != null)
                {
                    PlayerController.CorrectPosition(ack.X, ack.Y);
                    Debug.Log(string.Format("[GameSceneEntry] 移动被服务端纠正: ({0},{1})", ack.X, ack.Y));
                }
            }
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnChangeMapAck(TcpSession session, ChangeMapAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                UIToast.Show("传送失败: " + ack.ErrorMessage);
                Debug.Log("[GameSceneEntry] 切图失败: " + ack.ErrorMessage);
                return;
            }

            Debug.Log(string.Format("[GameSceneEntry] 切图成功 Map={0} Spawn=({1},{2})", ack.MapCfgId, ack.X, ack.Y));

            // 更新缓存的地图数据
            GameData.CurrentMap = new EnterMapData
            {
                MapCfgId = ack.MapCfgId,
                Line = ack.Line,
                SpawnX = ack.X,
                SpawnY = ack.Y
            };

            RebuildScene(ack.MapCfgId, ack.X, ack.Y);
        }

        private static void RebuildScene(int mapCfgId, int spawnX, int spawnY)
        {
            // 清除其他玩家和怪物
            OtherPlayerManager.Instance.ClearAll();
            MonsterEntityManager.Instance.ClearAll();

            // 销毁旧地图
            if (MapGround != null)
            {
                Object.Destroy(MapGround.gameObject);
                MapGround = null;
            }

            // 创建新地图
            var mapConfig = ConfigManager.Get<MapConfig>(mapCfgId);
            var mapObj = new GameObject("Map");
            var mapGround = mapObj.AddComponent<MapGround>();
            mapGround.Generate(mapConfig);
            MapGround = mapGround;

            // 重置玩家位置
            if (PlayerController != null)
            {
                PlayerController.Init(mapConfig, spawnX, spawnY);
            }

            Debug.Log(string.Format("[GameSceneEntry] 场景重建完成 Map={0} ({1}x{2})",
                mapConfig.Name, mapConfig.Width, mapConfig.Height));
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
            string reasonText = GetKickReasonText(reason);
            UIMessageBox.Show("下线通知", reasonText, () =>
            {
                GoBackToLogin();
            }, showCancel: false, confirmText: "确定");
        }

        private static string GetKickReasonText(int reason)
        {
            switch (reason)
            {
                case 2: return "您的账号在其他设备登录，已被迫下线。";
                case 3: return "您的账号已被封禁，请联系客服。";
                case 4: return "您已被GM强制踢下线。";
                case 5: return "网络连接超时，请检查网络后重试。";
                case 1: return "服务器正在维护，请稍后重试。";
                default: return "您已与服务器断开连接。";
            }
        }

        private void GoBackToLogin()
        {
            OtherPlayerManager.Instance.ClearAll();
            SceneManager.LoadScene("SampleScene");
        }
    }
}
