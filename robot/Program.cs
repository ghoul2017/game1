using System;
using System.Threading.Tasks;
using Client.Logic;
using Robot;

// 设置 Robot UI 桥接
ClientContext.UI = new RobotClientUI();

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

var runner = new TestRunner
{
    Host = host,
    Port = port
};

// 注册测试场景
if (scenarioName == null || scenarioName == "login_test")
    runner.AddScenario(new LoginTest());

if (runner.ScenarioCount == 0)
{
    Console.WriteLine("用法: robot [--host 127.0.0.1] [--port 9000] [--scenario login_test]");
    return 1;
}

try
{
    var exitCode = await runner.RunAll();
    return exitCode;
}
catch (Exception ex)
{
    Console.WriteLine($"运行失败: {ex}");
    return 1;
}
