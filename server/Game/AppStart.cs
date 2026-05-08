using Game.Components.Bag;
using Game.Components.Chat;
using Game.Components.Fight;
using Game.Components.Mail;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Components.User;
using Game.Config;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Shared.Core.Event;
using Shared.Core.Log;

namespace Game;

public static class AppStart
{
    public static async Task RunServer()
    {
        // 安装游戏主线程同步上下文，确保所有 await 恢复后回到主线程
        GameSynchronizationContext.Instance.Start();

        // 注册 Ctrl+C 关闭信号
        ShutdownToken.Instance.RegisterConsoleCancel();
        // 将主逻辑投递到游戏主线程执行
        try
        {
            await GameSynchronizationContext.Instance.PostAsync(RunServerAsync);
        }
        finally
        {
            GameSynchronizationContext.Instance.Stop();
        }
    }


    /// <summary>服务端主逻辑（在游戏主线程上执行）</summary>
    private static async Task RunServerAsync()
    {
        // 初始化配置管理器
        ConfigManager.Instance.Init(Context.Config.Tables.Directory);

        // 扫描注册事件处理器
        EventBus.Instance.AutoRegister();

        // 初始化数据库（MongoDB）
        await DatabaseManager.Instance.Init();

        // 添加所有 Component 到根实体
        var networkComponent = Context.Server.AddComponent<NetworkComponent>();
        networkComponent.SessionDisconnected = session =>
        {
            var player = Context.Players.GetByRoleId(session.RoleId);
            if (player != null)
            {
                _ = Context.Players.SaveRoleAsync(player);
                Context.Players.RemovePlayer(session.RoleId);
                EventBus.Instance.Publish(new EventType.PlayerOfflineEvent { RoleId = session.RoleId });
            }
        };
        EventBus.Instance.Publish(new EventType.ServerInitEvent());
        Context.Server.AddComponent<PlayerComponent>();
        Context.Server.AddComponent<FightComponent>();
        Context.Server.AddComponent<ChatComponent>();
        Context.Server.AddComponent<MapComponent>();
        Context.Server.AddComponent<MonsterComponent>();

        // 初始化有实质逻辑的子系统
        Context.Map.InitDefaultMaps();
        Context.Monster.Start();

        try
        {
            // 开始网络监听
            await networkComponent.Listen(Context.Config.Server.Host, Context.Config.Server.Port);

            // 等待关闭信号
            await ShutdownToken.Instance.WaitForShutdownAsync();
        }
        finally
        {
            // 停服清理
            await Context.Players.SaveAllRolesAsync();
            Context.Fight.RemoveAll();
            Context.Map.RemoveAll();
            Context.Monster.Stop();
        }
    }
}