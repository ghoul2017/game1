using System.Threading;
using System.Threading.Tasks;
using Game.Core;
using Shared.Core.Log;

namespace Game.Game.Chat;

/// <summary>
/// 聊天模块。注册到 ModuleRegistry，Order 在邮件之后。
/// </summary>
public class ChatModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<ChatModel>();

    public int Order => 60;

    public Task InitAsync()
    {
        Log.Info("聊天模块初始化完成");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Log.Info("聊天模块启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Log.Info("聊天模块停止");
        await Task.CompletedTask;
    }
}
