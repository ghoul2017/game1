using System;
using System.Threading.Tasks;
using Robot;
using Shared.Core.Event;

EventBus.Instance.AutoRegister();

Console.WriteLine("Robot 自动化测试框架");
Console.WriteLine("=====================");

// 命令行解析
string host = "127.0.0.1";
int port = 9000;
string? scenarioName = null;

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "--host":
            host = args[++i];
            break;
        case "--port":
            port = int.Parse(args[++i]);
            break;
        case "--scenario":
            scenarioName = args[++i];
            break;
    }
}

TestRunner runner = new TestRunner
{
    Host = host,
    Port = port
};

// 注册测试场景
if (scenarioName == null || scenarioName == "login_test")
{
    runner.AddScenario(new LoginTest());
}

if (scenarioName == null || scenarioName == "task_persistence_test")
{
    runner.AddScenario(new TaskPersistenceTest());
}

// 下列场景会创建独立持久化数据，只在显式指定时执行。
if (scenarioName == "expedition_authority_test")
{
    runner.AddScenario(new ExpeditionAuthorityTest());
}

if (scenarioName == "arena_idempotency_test")
{
    runner.AddScenario(new ArenaIdempotencyTest());
}

if (runner.ScenarioCount == 0)
{
    Console.WriteLine(
        "用法: robot [--host 127.0.0.1] [--port 9000] " +
        "[--scenario login_test|task_persistence_test|expedition_authority_test|arena_idempotency_test]");
    return 1;
}

try
{
    int exitCode = await runner.RunAll();
    return exitCode;
}
catch (Exception ex)
{
    Console.WriteLine($"运行失败: {ex}");
    return 1;
}
