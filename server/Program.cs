using System.Text.Json;
using Shared.Core;
using Shared.Core.Log;

namespace Game;

public static class Program
{
    private static async Task<int> Main(string[] args)
    {
        string json = await File.ReadAllTextAsync("appsettings.json");
        AppConfig config =
            JsonSerializer.Deserialize<AppConfig>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new Exception("appsettings.json 解析失败");

        Context.Config = config;
        LogManager.Init(Context.Config.Log);

        try
        {
            await AppStart.RunServer();
            return 0;
        }
        finally
        {
            LogManager.Shutdown();
        }
    }
}
