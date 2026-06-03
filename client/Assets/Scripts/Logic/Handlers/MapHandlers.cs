using Shared.Core.Network;
using Shared.Common.Protos;

namespace Client.Logic
{
    /// <summary>
    /// 地图相关消息 Handler（AOI + 移动校正 + 切图）。
    /// 原在 GameSceneEntry 中，现迁入 Logic 层。
    /// </summary>
    public static class MapHandlers
    {
        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectsNtf(TcpSession session, MapObjectsNtf ntf)
        {
            MapController.Instance.HandleMapObjectsNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerEnterNtf(TcpSession session, MapPlayerEnterNtf ntf)
        {
            MapController.Instance.HandleMapPlayerEnterNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerLeaveNtf(TcpSession session, MapPlayerLeaveNtf ntf)
        {
            MapController.Instance.HandleMapPlayerLeaveNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapPlayerMoveNtf(TcpSession session, MapPlayerMoveNtf ntf)
        {
            MapController.Instance.HandleMapPlayerMoveNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectEnterNtf(TcpSession session, MapObjectEnterNtf ntf)
        {
            MapController.Instance.HandleMapObjectEnterNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectLeaveNtf(TcpSession session, MapObjectLeaveNtf ntf)
        {
            MapController.Instance.HandleMapObjectLeaveNtf(ntf);
        }

        [MessageHandler(typeof(NtfMethodHandler))]
        public static void OnMapObjectMoveNtf(TcpSession session, MapObjectMoveNtf ntf)
        {
            MapController.Instance.HandleMapObjectMoveNtf(ntf);
        }

    }
}
