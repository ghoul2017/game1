using Server.Core;
using Server.Core.Entity;
using Server.Core.Log;

namespace Server.Game.TaskSystem;

/// <summary>
/// 任务模块。注册到 ModuleRegistry，管理 RoleTaskList 实体工厂。
/// </summary>
public class TaskModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<TaskModel>();

    public int Order => 60;

    public Task InitAsync()
    {
        var taskPersistFactory = new ProtobufPersistFactory<RoleTaskList>();
        EntityFactory.Instance.Register(taskPersistFactory);

        Log.Info("任务模块初始化完成 — RoleTaskList 实体工厂已注册");
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken ct)
    {
        Log.Info("任务模块启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        Log.Info("任务模块停止");
        await Task.CompletedTask;
    }
}
