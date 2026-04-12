using System.Reflection;

namespace Tools;

static class Program
{
    static async Task<int> Main(string[] args)
    {
        // 自动扫描程序集中所有 ITool 实现
        var tools = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(ITool).IsAssignableFrom(t))
            .Select(t => (ITool)Activator.CreateInstance(t)!)
            .ToDictionary(t => t.Name, t => t, StringComparer.OrdinalIgnoreCase);

        if (args.Length == 0 || args[0] is "--help" or "-h")
        {
            PrintUsage(tools);
            return 0;
        }

        if (args[0] is "--list" or "-l")
        {
            Console.WriteLine("可用工具:");
            foreach (var entry in tools.Values.OrderBy(t => t.Name))
                Console.WriteLine($"  {entry.Name,-16} {entry.Desc}");
            return 0;
        }

        var toolName = args[0];
        if (!tools.TryGetValue(toolName, out var tool))
        {
            Console.WriteLine($"未知工具: {toolName}");
            PrintUsage(tools);
            return 1;
        }

        var toolArgs = args.Skip(1).ToArray();
        return await tool.Run(toolArgs);
    }

    static void PrintUsage(Dictionary<string, ITool> tools)
    {
        Console.WriteLine("用法: dotnet run -- <tool> [args...]");
        Console.WriteLine("      dotnet run -- --list    列出所有工具");
        Console.WriteLine("      dotnet run -- --help    显示帮助");
        if (tools.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("工具列表:");
            foreach (var t in tools.Values.OrderBy(t => t.Name))
                Console.WriteLine($"  {t.Name,-16} {t.Desc}");
        }
    }
}
