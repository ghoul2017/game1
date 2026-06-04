using Shared.Core;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Misc;

/// <summary>
/// 握手+心跳处理。
/// </summary>
public static class MiscHandlers
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(MiscHandlers));

    private const int CheckIntervalSeconds = 20;

    [EventSubscriber]
    public static void OnServerInit(EventType.ServerInitEvent e)
    {
        GameTimer.Start(CheckIntervalSeconds * 1000, () =>
        {
            Context.Network.DoHeartbeatCheck();
        });
        Log.Info("心跳检测定时器已启动");
    }
}
