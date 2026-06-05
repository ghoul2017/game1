using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class MapController
    {
        public static MapController Instance { get; } = new();

        // 地图事件：由消息 Handler 触发，View 消费。
        public event Action OnEnterMapReady;
        public event Action<List<MapObjectInfo>, List<long>> OnMapObjectsChanged;
        public event Action<MapObjectMoveNtf> OnMapObjectMove;
        public event Action<int, int> OnMapMoveCorrected; // x, y
        public event Action<int, int, int> OnMapRebuild; // mapCfgId, spawnX, spawnY
        public event Action<string> OnChangeMapFailed;
        public event Action<int> OnKicked;
        public event Action OnReconnected;
        public event Action<string> OnReconnectFailed;

        private readonly List<MapObjectInfo> _pendingObjects = new List<MapObjectInfo>();
        private readonly List<long> _pendingRemoveObjectIds = new List<long>();
        private bool _mapViewReady;

        public async Task Move(int x, int y)
        {
            try
            {
                MapMoveAck ack = await NetClient.Instance.CallAsync<MapMoveAck>(
                    new MapMoveReq { X = x, Y = y });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    OnMapMoveCorrected?.Invoke(ack.X, ack.Y);
                }
            }
            catch (Exception ex)
            {
                Context.UI?.ShowToast("同步位置失败: " + ex.Message);
            }
        }

        public async Task ChangeMap(int targetMapCfgId, int targetLine = 1, int x = 0, int y = 0)
        {
            try
            {
                ChangeMapAck ack = await NetClient.Instance.CallAsync<ChangeMapAck>(
                    new ChangeMapReq
                    {
                        TargetMapCfgId = targetMapCfgId,
                        TargetLine = targetLine,
                        X = x,
                        Y = y
                    });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    OnChangeMapFailed?.Invoke(ack.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                OnChangeMapFailed?.Invoke(ex.Message);
            }
        }

        public async Task EnterMap(int mapCfgId, int line)
        {
            try
            {
                EnterMapAck ack = await NetClient.Instance.CallAsync<EnterMapAck>(
                    new EnterMapReq { MapCfgId = mapCfgId, Line = line });

                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    OnChangeMapFailed?.Invoke(ack.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                OnChangeMapFailed?.Invoke(ex.Message);
            }
        }

        // 地图 AOI 消息处理。

        public void HandleEnterMapNtf(EnterMapNtf ntf)
        {
            _pendingObjects.Clear();
            _pendingRemoveObjectIds.Clear();

            GameData.CurrentMap = new EnterMapData
            {
                MapCfgId = ntf.MapCfgId,
                Line = ntf.Line,
                SpawnX = ntf.X,
                SpawnY = ntf.Y
            };

            if (_mapViewReady)
                OnMapRebuild?.Invoke(ntf.MapCfgId, ntf.X, ntf.Y);

            OnEnterMapReady?.Invoke();
        }

        public void HandleMapObjectsNtf(MapObjectsNtf ntf)
        {
            var objects = ntf.Objects ?? new List<MapObjectInfo>();
            var removeObjectIds = ntf.RemoveObjectIds ?? new List<long>();
            if (objects.Count == 0 && removeObjectIds.Count == 0)
                return;

            if (!_mapViewReady)
            {
                _pendingObjects.AddRange(objects);
                _pendingRemoveObjectIds.AddRange(removeObjectIds);
                return;
            }

            OnMapObjectsChanged?.Invoke(objects, removeObjectIds);
        }

        public void HandleMapObjectMoveNtf(MapObjectMoveNtf ntf)
        {
            OnMapObjectMove?.Invoke(ntf);
        }

        public void MarkMapViewReady()
        {
            _mapViewReady = true;
            FlushPendingMapObjects();
        }

        public void MarkMapViewClosed()
        {
            _mapViewReady = false;
            _pendingObjects.Clear();
            _pendingRemoveObjectIds.Clear();
        }

        private void FlushPendingMapObjects()
        {
            if (_pendingObjects.Count == 0 && _pendingRemoveObjectIds.Count == 0)
                return;

            var objects = new List<MapObjectInfo>(_pendingObjects);
            var removeObjectIds = new List<long>(_pendingRemoveObjectIds);
            _pendingObjects.Clear();
            _pendingRemoveObjectIds.Clear();
            OnMapObjectsChanged?.Invoke(objects, removeObjectIds);
        }

        // 重连和踢下线。

        public async Task HandleReconnected()
        {
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                await EnterMap(mapData.MapCfgId, mapData.Line);
            }
            OnReconnected?.Invoke();
        }

        public void HandleReconnectFailed(string message)
        {
            OnReconnectFailed?.Invoke(message);
        }

        public void HandleKicked(int reason)
        {
            OnKicked?.Invoke(reason);
        }

        public static string GetKickReasonText(int reason)
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
    }
}
