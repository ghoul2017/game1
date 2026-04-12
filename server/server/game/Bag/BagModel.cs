using Server.Core;
using Server.Core.Entity;
using Server.Core.Log;

namespace Server.Game.Bag;

/// <summary>
/// 背包模块。注册到 ModuleRegistry，管理 RoleBag 实体工厂。
/// </summary>
public class BagModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<BagModel>();

    public int Order => 56;

    public Task InitAsync()
    {
        var bagPersistFactory = new ProtobufPersistFactory<RoleBag>();
        EntityFactory.Instance.Register(bagPersistFactory);

        Log.Info("背包模块初始化完成 — RoleBag 实体工厂已注册");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Log.Info("背包模块启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        // 停服时背包数据通过 PersistTask.FlushAll 统一持久化
        Log.Info("背包模块停止");
        await Task.CompletedTask;
    }
}
