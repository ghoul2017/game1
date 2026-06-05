using Shared.Core;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game.Components.Misc;

/// <summary>
/// 握手+心跳处理。
/// </summary>
public sealed class ServerInitHeartbeatEventHandler : ISyncEventHandler<EventType.ServerInitEvent>
{
    private static readonly ILog Log = LogManager.GetLogger(nameof(ServerInitHeartbeatEventHandler));

    private const int CheckIntervalSeconds = 20;

    public void Handle(EventType.ServerInitEvent e)
    {
        GameTimer.Start(CheckIntervalSeconds * 1000, () =>
        {
            Context.Network.DoHeartbeatCheck();
        });
        Log.Info("心跳检测定时器已启动");
    }
}
