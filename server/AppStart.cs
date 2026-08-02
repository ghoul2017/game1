using Game.Components.Arena;
using Game.Components.Bag;
using Game.Components.Chat;
using Game.Components.Expedition;
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
            await using LocalTestServerLifetime? localTestServerLifetime =
                LocalTestServerLifetime.StartIfConfigured();
            bool localEditorServer = localTestServerLifetime != null;
            await GameSynchronizationContext.Instance.PostAsync(() => RunServerAsync(localEditorServer));
        }
        finally
        {
            GameSynchronizationContext.Instance.Stop();
        }
    }


    /// <summary>服务端主逻辑（在游戏主线程上执行）</summary>
    private static async Task RunServerAsync(bool localEditorServer)
    {
        // 初始化配置管理器
        ConfigManager.Instance.Init(Context.Config.Tables.Directory);

        // 扫描注册事件处理器
        EventBus.Instance.AutoRegister();

        // 初始化数据库（MongoDB）
        await MongoDb.Instance.Init(Context.Config.MongoDb);

        // 添加所有 Component 到根实体
        NetworkComponent networkComponent = Context.Server.AddComponent<NetworkComponent>();
        networkComponent.SessionDispatcher = DispatchToGameThread;
        networkComponent.SessionDisconnected = OnSessionDisconnected;
        EventBus.Instance.Publish(new EventType.ServerInitEvent());
        PlayerComponent playerComponent = Context.Server.AddComponent<PlayerComponent>();
        playerComponent.StartBatchSave();
        Context.Server.AddComponent<ChatComponent>();
        Context.Server.AddComponent<MapComponent>();
        ExpeditionComponent expeditionComponent = Context.Server.AddComponent<ExpeditionComponent>();
        expeditionComponent.Configure(new GameplayCoreExpeditionSimulationFactory());
        Context.Server.AddComponent<ArenaComponent>();

        // 初始化地图（含刷怪和 AI 定时器）
        Context.Map.InitDefaultMaps();
        expeditionComponent.StartScheduler();

        try
        {
            // 开始网络监听
            string listenHost = localEditorServer ? "127.0.0.1" : Context.Config.Server.Host;
            await networkComponent.Listen(listenHost, Context.Config.Server.Port);

            // 等待关闭信号
            await ShutdownToken.Instance.WaitForShutdownAsync();
        }
        finally
        {
            // 停服清理
            GameTimer.StopAll();
            expeditionComponent.StopScheduler();
            Context.Players.StopBatchSave();
            await Context.Players.FlushAllPlayersAsync();
            Context.Map.RemoveAll();
        }
    }

    private static void DispatchToGameThread(Action action)
    {
        if (GameSynchronizationContext.IsMainThread)
        {
            action();
            return;
        }

        GameSynchronizationContext.Instance.Post(_ => action(), null);
    }

    private static void OnSessionDisconnected(TcpSession session, LogoutReason reason)
    {
        UserComponent userComponent = Context.Server.GetComponent<UserComponent>();
        UserHelper.TryBeginRecoveryWindow(
            userComponent,
            Context.Players,
            session,
            ReconnectTokenRegistry.Instance);

        // 清理 User 级 session 引用
        if (session.UserId != 0L)
        {
            if (userComponent.Users.TryGetValue(session.UserId, out User? user))
            {
                if (ReferenceEquals(user.Session, session))
                {
                    user.PendingReconnectRoleId = 0L;
                    user.Session = null;
                }
            }
        }

        // 清理 Player 级 session 引用 + 保存数据 + 发布下线事件
        if (session.RoleId != 0L)
        {
            PlayerEntity? player = Context.Players.GetByRoleId(session.RoleId);
            if (player != null)
            {
                PlayerOnlineComponent onlineComponent = player.GetComponent<PlayerOnlineComponent>();
                if (!ReferenceEquals(onlineComponent.Session, session))
                {
                    return;
                }

                player.RefreshOnlineActivity(session.LastActiveTime);
                Context.Expedition.DetachRole(session.RoleId);
                onlineComponent.Session = null;
                Context.Players.QueueDetachedPlayerSave(player);
                Context.Players.RemovePlayer(session.RoleId);
                EventBus.Instance.Publish(new EventType.PlayerOfflineEvent
                {
                    RoleId = session.RoleId,
                    Reason = reason
                });
            }
        }
    }
}
