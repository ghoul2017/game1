using System;
using System.Collections.Generic;
using Shared.Common.Protos;

namespace Client.Logic
{
    public class MapController
    {
        public static MapController Instance { get; } = new();

        // --- AOI events (fired from handlers, consumed by View) ---
        public event Action<List<MapObjectInfo>> OnMapObjectsSpawned;
        public event Action<MapObjectInfo> OnMapPlayerEnter;
        public event Action<long> OnMapPlayerLeave;
        public event Action<long, int, int> OnMapPlayerMove;
        public event Action<long, int, int, string, int, int> OnMapObjectEnter; // objectId, cfgId, objectType, name, x, y
        public event Action<long> OnMapObjectLeave;
        public event Action<long, int, int> OnMapObjectMove;
        public event Action<int, int> OnMapMoveCorrected; // x, y
        public event Action<int, int, int> OnMapRebuild; // mapCfgId, spawnX, spawnY
        public event Action<string> OnChangeMapFailed;
        public event Action<int> OnKicked;
        public event Action OnReconnected;
        public event Action<string> OnReconnectFailed;

        // --- AOI Handlers ---

        public void HandleMapObjectsNtf(MapObjectsNtf ntf)
        {
            if (ntf.Objects != null && ntf.Objects.Count > 0)
                OnMapObjectsSpawned?.Invoke(ntf.Objects);
        }

        public void HandleMapPlayerEnterNtf(MapPlayerEnterNtf ntf)
        {
            if (ntf.Player != null)
                OnMapPlayerEnter?.Invoke(ntf.Player);
        }

        public void HandleMapPlayerLeaveNtf(MapPlayerLeaveNtf ntf)
        {
            OnMapPlayerLeave?.Invoke(ntf.RoleId);
        }

        public void HandleMapPlayerMoveNtf(MapPlayerMoveNtf ntf)
        {
            OnMapPlayerMove?.Invoke(ntf.RoleId, ntf.X, ntf.Y);
        }

        public void HandleMapObjectEnterNtf(MapObjectEnterNtf ntf)
        {
            OnMapObjectEnter?.Invoke(ntf.ObjectId, ntf.CfgId, ntf.ObjectType, ntf.Name, ntf.X, ntf.Y);
        }

        public void HandleMapObjectLeaveNtf(MapObjectLeaveNtf ntf)
        {
            OnMapObjectLeave?.Invoke(ntf.ObjectId);
        }

        public void HandleMapObjectMoveNtf(MapObjectMoveNtf ntf)
        {
            OnMapObjectMove?.Invoke(ntf.ObjectId, ntf.X, ntf.Y);
        }

        public void HandleMapMoveAck(MapMoveAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                OnMapMoveCorrected?.Invoke(ack.X, ack.Y);
            }
        }

        public void HandleChangeMapAck(ChangeMapAck ack)
        {
            if (!string.IsNullOrEmpty(ack.ErrorMessage))
            {
                OnChangeMapFailed?.Invoke(ack.ErrorMessage);
                return;
            }

            GameData.CurrentMap = new EnterMapData
            {
                MapCfgId = ack.MapCfgId,
                Line = ack.Line,
                SpawnX = ack.X,
                SpawnY = ack.Y
            };
            OnMapRebuild?.Invoke(ack.MapCfgId, ack.X, ack.Y);
        }

        // --- Reconnect / Kick ---

        public void HandleReconnected()
        {
            var mapData = GameData.CurrentMap;
            if (mapData != null)
            {
                var req = new EnterMapReq();
                req.MapCfgId = mapData.MapCfgId;
                req.Line = mapData.Line;
                ClientContext.Network?.Send(req);
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
