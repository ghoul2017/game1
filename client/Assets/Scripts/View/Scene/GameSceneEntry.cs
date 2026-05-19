using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Client.Logic;
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

        // 存储 NetClient 事件委托以便取消订阅
        private Action _onReconnectedHandler;
        private Action<string> _onReconnectFailedHandler;
        private Action<int> _onKickedHandler;

        private void Start()
        {
            Debug.Log("[GameSceneEntry] 初始化游戏场景");

            // UIManager 因 DontDestroyOnLoad 在场景切换后保留
            UIManager.EnsureCreated();

            InitializeScene();

            // 订阅 MapController 事件
            MapController.Instance.OnMapObjectsSpawned += HandleMapObjectsSpawned;
            MapController.Instance.OnMapPlayerEnter += HandleMapPlayerEnter;
            MapController.Instance.OnMapPlayerLeave += HandleMapPlayerLeave;
            MapController.Instance.OnMapPlayerMove += HandleMapPlayerMove;
            MapController.Instance.OnMapObjectEnter += HandleMapObjectEnter;
            MapController.Instance.OnMapObjectLeave += HandleMapObjectLeave;
            MapController.Instance.OnMapObjectMove += HandleMapObjectMove;
            MapController.Instance.OnMapMoveCorrected += HandleMapMoveCorrected;
            MapController.Instance.OnMapRebuild += HandleMapRebuild;
            MapController.Instance.OnChangeMapFailed += HandleChangeMapFailed;
            MapController.Instance.OnKicked += HandleKickedEvent;
            MapController.Instance.OnReconnected += HandleReconnectedEvent;
            MapController.Instance.OnReconnectFailed += HandleReconnectFailedEvent;

            // Wire NetClient events to MapController
            var nc = NetClient.Instance;
            if (nc != null)
            {
                _onReconnectedHandler = () => MapController.Instance.HandleReconnected();
                _onReconnectFailedHandler = (msg) => MapController.Instance.HandleReconnectFailed(msg);
                _onKickedHandler = (reason) => MapController.Instance.HandleKicked(reason);

                nc.OnReconnected += _onReconnectedHandler;
                nc.OnReconnectFailed += _onReconnectFailedHandler;
                nc.OnKicked += _onKickedHandler;
            }

            _mainPanel = UIManager.Instance.ShowPanel<MainPanel>();
        }

        private void OnDestroy()
        {
            // 取消订阅 MapController 事件
            MapController.Instance.OnMapObjectsSpawned -= HandleMapObjectsSpawned;
            MapController.Instance.OnMapPlayerEnter -= HandleMapPlayerEnter;
            MapController.Instance.OnMapPlayerLeave -= HandleMapPlayerLeave;
            MapController.Instance.OnMapPlayerMove -= HandleMapPlayerMove;
            MapController.Instance.OnMapObjectEnter -= HandleMapObjectEnter;
            MapController.Instance.OnMapObjectLeave -= HandleMapObjectLeave;
            MapController.Instance.OnMapObjectMove -= HandleMapObjectMove;
            MapController.Instance.OnMapMoveCorrected -= HandleMapMoveCorrected;
            MapController.Instance.OnMapRebuild -= HandleMapRebuild;
            MapController.Instance.OnChangeMapFailed -= HandleChangeMapFailed;
            MapController.Instance.OnKicked -= HandleKickedEvent;
            MapController.Instance.OnReconnected -= HandleReconnectedEvent;
            MapController.Instance.OnReconnectFailed -= HandleReconnectFailedEvent;

            // 取消订阅 NetClient 事件
            var nc = NetClient.Instance;
            if (nc != null)
            {
                nc.OnReconnected -= _onReconnectedHandler;
                nc.OnReconnectFailed -= _onReconnectFailedHandler;
                nc.OnKicked -= _onKickedHandler;
            }

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

        // ==================== MapController Event Handlers ====================

        private void HandleMapObjectsSpawned(List<MapObjectInfo> objects)
        {
            if (objects == null) return;
            foreach (var obj in objects)
            {
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

        private void HandleMapPlayerEnter(MapObjectInfo player)
        {
            if (player != null)
                OtherPlayerManager.Instance.SpawnPlayer(player);
        }

        private void HandleMapPlayerLeave(long roleId)
        {
            OtherPlayerManager.Instance.RemovePlayer(roleId);
        }

        private void HandleMapPlayerMove(long roleId, int x, int y)
        {
            OtherPlayerManager.Instance.MovePlayer(roleId, x, y);
        }

        private void HandleMapObjectEnter(long objectId, int cfgId, int objectType, string name, int x, int y)
        {
            MonsterEntityManager.Instance.Spawn(objectId, cfgId, objectType, name, x, y);
        }

        private void HandleMapObjectLeave(long objectId)
        {
            MonsterEntityManager.Instance.Remove(objectId);
        }

        private void HandleMapObjectMove(long objectId, int x, int y)
        {
            MonsterEntityManager.Instance.Move(objectId, x, y);
        }

        private void HandleMapMoveCorrected(int x, int y)
        {
            if (PlayerController != null)
            {
                PlayerController.CorrectPosition(x, y);
                Debug.Log(string.Format("[GameSceneEntry] 移动被服务端纠正: ({0},{1})", x, y));
            }
        }

        private void HandleMapRebuild(int mapCfgId, int spawnX, int spawnY)
        {
            // 清除其他玩家和怪物
            OtherPlayerManager.Instance.ClearAll();
            MonsterEntityManager.Instance.ClearAll();

            // 销毁旧地图
            if (MapGround != null)
            {
                UnityEngine.Object.Destroy(MapGround.gameObject);
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

        private void HandleChangeMapFailed(string error)
        {
            UIToast.Show("传送失败: " + error);
            Debug.Log("[GameSceneEntry] 切图失败: " + error);
        }

        private void HandleKickedEvent(int reason)
        {
            Debug.Log("[GameSceneEntry] 被踢下线，reason=" + reason);
            string reasonText = MapController.GetKickReasonText(reason);
            UIMessageBox.Show("下线通知", reasonText, () =>
            {
                GoBackToLogin();
            }, showCancel: false, confirmText: "确定");
        }

        private void HandleReconnectedEvent()
        {
            Debug.Log("[GameSceneEntry] 重连成功，清除旧的视野数据");
            OtherPlayerManager.Instance.ClearAll();
        }

        private void HandleReconnectFailedEvent(string msg)
        {
            Debug.Log("[GameSceneEntry] 重连失败: " + msg + "，返回登录");
            GoBackToLogin();
        }

        private void GoBackToLogin()
        {
            OtherPlayerManager.Instance.ClearAll();
            SceneManager.LoadScene("SampleScene");
        }
    }
}
