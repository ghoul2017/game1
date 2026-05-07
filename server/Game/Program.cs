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

public static class Program
{

    static async Task<int> Main(string[] args)
    {
        var json = await File.ReadAllTextAsync("appsettings.json");
        var config =
            JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("appsettings.json 解析失败");

        Context.Config = config;
        LogManager.Init(Context.Config.Log);
        
        await AppStart.RunServer();
        return 0;
    }
}