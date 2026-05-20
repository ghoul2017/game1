using Game.Components.Bag;
using Game.Components.Chat;
using Game.Components.Mail;
using Game.Components.Map;
using Game.Components.Player;
using Game.Components.User;
using Shared.Core.Config;
using Shared.Core;
using Game.Database;
using Shared.Common.Enums;
using Shared.Core.Event;
using Shared.Core.Log;
using Shared.Core.Network;

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
        await MongoDb.Instance.Init(Context.Config.MongoDb);

        // 添加所有 Component 到根实体
        var networkComponent = Context.Server.AddComponent<NetworkComponent>();
        networkComponent.SessionDisconnected = OnSessionDisconnected;
        EventBus.Instance.Publish(new EventType.ServerInitEvent());
        Context.Server.AddComponent<PlayerComponent>();
        Context.Server.AddComponent<ChatComponent>();
        Context.Server.AddComponent<MapComponent>();

        // 初始化地图（含刷怪和 AI 定时器）
        Context.Map.InitDefaultMaps();

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
            Context.Map.RemoveAll();
        }
    }

    private static void OnSessionDisconnected(TcpSession session, LogoutReason reason)
    {
        // 清理 User 级 session 引用
        if (session.UserId != 0)
        {
            var userComponent = Context.Server.GetComponent<UserComponent>();
            if (userComponent.Users.TryGetValue(session.UserId, out var user))
            {
                user.Session = null;
            }
        }

        // 清理 Player 级 session 引用 + 保存数据 + 发布下线事件
        if (session.RoleId != 0)
        {
            var player = Context.Players.GetByRoleId(session.RoleId);
            if (player != null)
            {
                player.Online.Session = null;
                _ = Context.Players.SaveRoleAsync(player);
                Context.Players.RemovePlayer(session.RoleId);
                EventBus.Instance.Publish(new EventType.PlayerOfflineEvent { RoleId = session.RoleId, Reason = reason });
            }
        }
    }
}
