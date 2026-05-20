using System.Text.Json;
using Shared.Core;
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