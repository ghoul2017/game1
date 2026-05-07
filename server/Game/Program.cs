using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Components.Chat;
using Game.Components.Fight;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Components.User;
using Game.Config;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Shared.Core.Log;

namespace Game;

public class Program
{
    private static readonly ILog Log = LogManager.GetLogger<Program>();

    static async Task<int> Main(string[] args)
    {
        // 安装游戏主线程同步上下文，确保所有 await 恢复后回到主线程
        GameSynchronizationContext.Instance.Start();

        // 注册 Ctrl+C 关闭信号
        ShutdownToken.Instance.RegisterConsoleCancel();
        // 将主逻辑投递到游戏主线程执行
        try
        {
            await GameSynchronizationContext.Instance.PostAsync(() => RunServerAsync(args));
        }
        finally
        {
            GameSynchronizationContext.Instance.Stop();
        }

        return 0;
    }

    /// <summary>服务端主逻辑（在游戏主线程上执行）</summary>
    private static async Task RunServerAsync(string[] args)
    {
        var json = await File.ReadAllTextAsync("appsettings.json");
        var config =
            JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("appsettings.json 解析失败");

        Context.Config = config;
        LogManager.Init(config.Log);

        // 初始化数据库（MongoDB）
        await DatabaseManager.Instance.Init(config);

        // 初始化配置管理器
        ConfigManager.Instance.Init(config.Tables.Directory);

        // 添加所有 Component 到根实体
        var networkComponent = Context.Server.AddComponent<NetworkComponent>();
        Context.Server.AddComponent<UserComponent>();
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
            await networkComponent.Listen(config.Server.Host, config.Server.Port);

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
            Log.Info("服务端已停止");
        }
    }
}
