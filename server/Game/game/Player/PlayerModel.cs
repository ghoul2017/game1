using System.Threading.Tasks;
using Game.Core;
using Game.Core.Entity;
using Shared.Core.Log;

namespace Game.Game.Player;

/// <summary>
/// 玩家模块。注册到 ModuleRegistry，管理 Role 实体工厂和 PlayerManager 的生命周期。
/// </summary>
public class PlayerModel : IModule
{
    private static readonly ILog Log = LogManager.GetLogger<PlayerModel>();

    public int Order => 55;

    public async Task InitAsync()
    {
        // 注册 Role 实体的持久化工厂（自动建表）
        var rolePersistFactory = new ProtobufPersistFactory<Role>();
        await EntityFactory.Instance.RegisterAsync(rolePersistFactory);

        Log.Info("玩家模块初始化完成 — Role 实体工厂已注册");
    }

    public Task StartAsync()
    {
        Log.Info("玩家模块启动");
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        // 停服时立即保存所有在线角色数据
        await PlayerManager.Instance.SaveAllRolesAsync(immediately: true);
        PlayerManager.Instance.RemoveAll();
        Log.Info("玩家模块停止");
    }
}
