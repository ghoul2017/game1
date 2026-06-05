using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shared.Common.Protos;
using Shared.Core.Event;

namespace Client.Logic
{
    public class MapController
    {
        public static MapController Instance { get; } = new();

        private readonly List<MapObjectInfo> _pendingObjects = new List<MapObjectInfo>();
        private readonly List<long> _pendingRemoveObjectIds = new List<long>();
        private bool _mapViewReady;

        internal List<MapObjectInfo> PendingObjects => _pendingObjects;
        internal List<long> PendingRemoveObjectIds => _pendingRemoveObjectIds;
        internal bool MapViewReady => _mapViewReady;

        public async Task Move(int x, int y)
        {
            try
            {
                MapMoveAck ack = await NetClient.Instance.CallAsync<MapMoveAck>(
                    new MapMoveReq { X = x, Y = y });
                if (!string.IsNullOrEmpty(ack.ErrorMessage))
                {
                    EventBus.Instance.Publish(new EventType.MapMoveCorrectedEvent { X = ack.X, Y = ack.Y });
                }
            }
            catch (Exception ex)
            {
                ClientUiEventPublisher.ShowToast("同步位置失败: " + ex.Message);
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
                    EventBus.Instance.Publish(new EventType.ChangeMapFailedEvent { ErrorMessage = ack.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                EventBus.Instance.Publish(new EventType.ChangeMapFailedEvent { ErrorMessage = ex.Message });
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
                    EventBus.Instance.Publish(new EventType.ChangeMapFailedEvent { ErrorMessage = ack.ErrorMessage });
                }
            }
            catch (Exception ex)
            {
                EventBus.Instance.Publish(new EventType.ChangeMapFailedEvent { ErrorMessage = ex.Message });
            }
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
            EventBus.Instance.Publish(new EventType.MapObjectsChangedEvent { Objects = objects, RemoveObjectIds = removeObjectIds });
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
