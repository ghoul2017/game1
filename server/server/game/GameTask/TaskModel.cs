using System.Threading;
using System.Threading.Tasks;
using Server.Core;
using Server.Core.Entity;
using Server.Core.Log;

namespace Server.Game.GameTask;

/// <summary>
/// 任务模块。注册到 ModuleRegistry，管理 RoleTaskList 实体工厂。
/// </summary>
public class TaskModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<TaskModel>();

    public int Order => 60;

    public async Task InitAsync()
    {
        var taskPersistFactory = new ProtobufPersistFactory<RoleTaskList>();
        await EntityFactory.Instance.RegisterAsync(taskPersistFactory);

        Log.Info("任务模块初始化完成 — RoleTaskList 实体工厂已注册");
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
