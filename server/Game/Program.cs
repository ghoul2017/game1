using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Game;
using Game.Config;
using Game.Core;
using Shared.Core.Log;
using Game.Database;

var json = await File.ReadAllTextAsync("appsettings.json");
var config =
    JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new Exception("appsettings.json 解析失败");


LogManager.Init(config.Log);
await DatabaseManager.Instance.Init(config);
Context.Server.AddComponent<ConfigComponent>().Init(config.Tables.Directory);

var registry = ModuleRegistry.Create(config);

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

try
{
    await registry.InitAllAsync();
    await registry.StartAllAsync(cts.Token);
}
finally
{
    await registry.StopAllAsync();
}