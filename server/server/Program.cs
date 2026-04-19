using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Server.Core;
using Server.Core.Log;
using Server.Database;

var json = await File.ReadAllTextAsync("appsettings.json");
var config =
    JsonSerializer.Deserialize<AppConfig>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
    ?? throw new Exception("appsettings.json 解析失败");


LogManager.Init(config.Log);
await DatabaseManager.Instance.Init(config);

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