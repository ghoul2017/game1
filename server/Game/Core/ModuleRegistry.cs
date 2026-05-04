using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Game.Core;

/// <summary>
/// 模块注册表。自动扫描程序集中的 IModule 实现，按 Order 管理生命周期。
/// </summary>
public class ModuleRegistry
{
    private static ModuleRegistry? _instance;
    public static ModuleRegistry Instance => _instance!;
    private static AppConfig? _config;
    public static AppConfig Config => _config!;

    private readonly Dictionary<Type, IModule> _modules = new();
    private readonly List<IModule> _orderedModules = [];


    private ModuleRegistry(AppConfig config)
    {
        _config = config;
        _instance = this;
    }

    /// <summary>获取指定类型的模块实例</summary>
    public T? GetModule<T>() where T : class, IModule
    {
        return _modules.TryGetValue(typeof(T), out var m) ? (T)m : null;
    }

    /// <summary>创建注册表并自动扫描程序集中的所有 IModule 实现</summary>
    public static ModuleRegistry Create(AppConfig config)
    {
        var registry = new ModuleRegistry(config);

        var moduleTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && typeof(IModule).IsAssignableFrom(t));

        var modules = new List<(IModule module, int order)>();
        foreach (var type in moduleTypes)
        {
            var module = (IModule)Activator.CreateInstance(type)!;
            modules.Add((module, module.Order));
        }

        modules.Sort((a, b) => a.order.CompareTo(b.order));

        foreach (var (module, _) in modules)
        {
            registry._modules[module.GetType()] = module;
            registry._orderedModules.Add(module);
        }

        return registry;
    }

    /// <summary>按 Order 升序初始化所有模块</summary>
    public async Task InitAllAsync()
    {
        foreach (var module in _orderedModules)
        {
            await module.InitAsync();
        }
    }

    /// <summary>按 Order 升序启动所有模块</summary>
    public async Task StartAllAsync()
    {
        foreach (var module in _orderedModules)
        {
            await module.StartAsync();
        }
    }

    /// <summary>按 Order 降序停止所有模块</summary>
    public async Task StopAllAsync()
    {
        for (var i = _orderedModules.Count - 1; i >= 0; i--)
        {
            try
            {
                await _orderedModules[i].StopAsync();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"模块 {_orderedModules[i].GetType().Name} 停止失败: {ex.Message}");
            }
        }
    }
}