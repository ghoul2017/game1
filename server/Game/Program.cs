using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Game.Components.Bag;
using Game.Components.Chat;
using Game.Components.Fight;
using Game.Components.Mail;
using Game.Components.Map;
using Game.Components.Monster;
using Game.Components.Player;
using Game.Components.Task;
using Game.Components.User;
using Game.Config;
using Game.Core;
using Game.Core.Network;
using Game.Database;
using Game.Game.Fight;
using Game.Game.Map;
using Game.Game.Monster;
using Game.Game.Player;
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
        Context.Server.AddComponent<BagComponent>();
        Context.Server.AddComponent<MailComponent>();
        Context.Server.AddComponent<FightComponent>();
        Context.Server.AddComponent<ChatComponent>();
        Context.Server.AddComponent<MapComponent>();
        Context.Server.AddComponent<TaskComponent>();
        Context.Server.AddComponent<MonsterComponent>();

        // 初始化有实质逻辑的子系统
        FightManager.Instance.SetConfigManager();
        MapManager.Instance.InitDefaultMaps();
        MonsterManager.Instance.Start();

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
            await PlayerManager.Instance.SaveAllRolesAsync();
            FightManager.Instance.RemoveAll();
            MapManager.Instance.RemoveAll();
            MonsterManager.Instance.Stop();
            Log.Info("服务端已停止");
        }
    }
}
