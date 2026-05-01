using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Shared.Common.Config;
using Shared.Core.Log;
using Shared.Core.Config;

#nullable enable
namespace Game.Config
{
    public class ConfigManager : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<ConfigManager>();
        
        public static ConfigManager Instance { get; } = new();

        /// <summary>全表数据 Type → List&lt;T&gt;</summary>
        private readonly ConcurrentDictionary<Type, object> _lists = new();

        /// <summary>索引字典 Type → Dictionary&lt;int, T&gt;（按主键）</summary>
        private readonly ConcurrentDictionary<Type, object> _dicts = new();

        /// <summary>扫描到的配置元信息 Type → (FileName, KeyName, KeyProperty)</summary>
        private readonly Dictionary<Type, (string fileName, PropertyInfo keyProp)> _configMeta = new();

        private FileSystemWatcher? _watcher;
        
        public void Init(string directory)
        {
            Instance.LoadAll(directory);
            Log.Info("配置表模块初始化完成");
        }

        public void LoadAll(string tablesDirectory)
        {
            if (!Directory.Exists(tablesDirectory))
            {
                Log.Warn($"表目录不存在: {tablesDirectory}");
                return;
            }

            ScanConfigs();
            LoadFromDirectory(tablesDirectory);
            StartWatch(tablesDirectory);
        }

        /// <summary>反射扫描程序集中所有 [ConfigTable] 标注的类</summary>
        private void ScanConfigs()
        {
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<ConfigTableAttribute>();
                if (attr == null) continue;

                var keyProp = type.GetProperty(attr.KeyName);
                if (keyProp == null)
                    throw new InvalidOperationException(
                        $"配置表 {type.Name} 标注 KeyName=\"{attr.KeyName}\"，但未找到该属性");

                var attrFileName = attr.FileName;
                if ("".Equals(attrFileName))
                {
                    attrFileName = type.Name + ".csv";
                }

                _configMeta[type] = (attrFileName, keyProp);
                Log.Debug($"扫描到配置表: {type.Name} → {attrFileName} (Key={attr.KeyName})");
            }

            Log.Info($"共扫描到 {_configMeta.Count} 个配置表");
        }

        /// <summary>从目录加载所有已扫描的 CSV 文件</summary>
        private void LoadFromDirectory(string tablesDirectory)
        {
            var loadMethod = typeof(CsvLoader).GetMethod("Load")!;

            foreach (var (type, (fileName, keyProp)) in _configMeta)
            {
                var filePath = Path.Combine(tablesDirectory, fileName);
                if (!File.Exists(filePath))
                {
                    Log.Warn($"表文件不存在: {fileName}");
                    continue;
                }

                var genericMethod = loadMethod.MakeGenericMethod(type);
                var data = genericMethod.Invoke(null, new object[] { filePath })!;
                var list = (System.Collections.IList)data;

                // 构建索引字典
                var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), type);
                var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;
                foreach (var item in list)
                {
                    var key = (int)keyProp.GetValue(item)!;
                    dict[key] = item;
                }

                _lists[type] = data;
                _dicts[type] = dict;
                Log.Info($"加载 {fileName}: {list.Count} 条");
            }
        }

        /// <summary>按主键查单条</summary>
        public static T? Get<T>(int id) where T : class
        {
            if (!Instance._dicts.TryGetValue(typeof(T), out var dict)) return null;
            return ((Dictionary<int, T>)dict).TryGetValue(id, out var val) ? val : null;
        }

        /// <summary>获取全表数据</summary>
        public static List<T> GetAll<T>() where T : class
        {
            return Instance._lists.TryGetValue(typeof(T), out var data) ? (List<T>)data : new();
        }

        /// <summary>获取索引字典</summary>
        public static Dictionary<int, T> GetDict<T>() where T : class
        {
            return Instance._dicts.TryGetValue(typeof(T), out var dict) ? (Dictionary<int, T>)dict : new();
        }

        /// <summary>热更新通知事件</summary>
        public event Action<Type>? OnTableReloaded;

        private void StartWatch(string tablesDirectory)
        {
            _watcher = new FileSystemWatcher(tablesDirectory, "*.csv")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            };

            var lastWrite = new ConcurrentDictionary<string, DateTime>();

            _watcher.Changed += (_, e) =>
            {
                var fileName = Path.GetFileName(e.Name ?? "");

                // 查找对应的配置类型
                var type = _configMeta.FirstOrDefault(kv => kv.Value.fileName == fileName).Key;
                if (type == null) return;

                var now = DateTime.UtcNow;
                if (lastWrite.TryGetValue(fileName, out var last) && (now - last).TotalSeconds < 1)
                    return;
                lastWrite[fileName] = now;

                Log.Info($"检测到文件变化: {fileName}，正在热更新...");
                try
                {
                    var (_, keyProp) = _configMeta[type];
                    var method = typeof(CsvLoader).GetMethod("Load")!;
                    var genericMethod = method.MakeGenericMethod(type);
                    var data = genericMethod.Invoke(null, new object[] { e.FullPath })!;
                    var list = (System.Collections.IList)data;

                    // 重建索引字典
                    var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), type);
                    var dict = (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;
                    foreach (var item in list)
                    {
                        var key = (int)keyProp.GetValue(item)!;
                        dict[key] = item;
                    }

                    _lists[type] = data;
                    _dicts[type] = dict;
                    OnTableReloaded?.Invoke(type);
                    Log.Info($"热更新完成: {fileName} ({list.Count} 条)");
                }
                catch (Exception ex)
                {
                    Log.Error($"热更新失败: {fileName}", ex);
                }
            };

            _watcher.EnableRaisingEvents = true;
            Log.Info("热更新监听已启动");
        }

        public void Dispose()
        {
            _watcher?.Dispose();
        }
    }
}