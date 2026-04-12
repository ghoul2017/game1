using Server.Core;
using Server.Core.Entity;
using Server.Core.Log;

namespace Server.Game.Mail;

/// <summary>
/// 邮件模块。注册到 ModuleRegistry，管理 RoleMailList 实体工厂。
/// </summary>
public class MailModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<MailModel>();

    public int Order => 58;

    public Task InitAsync()
    {
        var mailPersistFactory = new ProtobufPersistFactory<RoleMailList>();
        EntityFactory.Instance.Register(mailPersistFactory);

        Log.Info("邮件模块初始化完成 — RoleMailList 实体工厂已注册");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Log.Info("邮件模块启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        // 停服时邮件数据通过 PersistTask.FlushAll 统一持久化
        Log.Info("邮件模块停止");
        await Task.CompletedTask;
    }
}
