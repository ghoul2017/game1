using Server.Core;
using Server.Core.Log;

namespace Server.Network;

public class NetworkModule : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<NetworkModule>();

    private MessageRouter _router = null!;
    private TcpServer _server = null!;

    public int Order => 100;

    public Task InitAsync()
    {
        _router = new MessageRouter();
        _router.AutoRegister();
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _server = new TcpServer(ModuleRegistry.Config.Server, _router);
        Log.Info("服务器启动");
        return _server.StartAsync(ct);
    }

    public Task StopAsync()
    {
        SessionManager.Instance.StopHeartbeat();
        _server?.Stop();
        return Task.CompletedTask;
    }
}
