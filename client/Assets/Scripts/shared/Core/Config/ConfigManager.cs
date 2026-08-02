using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Shared.Common.Config;
using Shared.Core.Config;
using Shared.Core.Log;

#nullable enable
namespace Shared.Core.Config
{
    public class ConfigManager : IDisposable
    {
        private static readonly ILog Log = LogManager.GetLogger<ConfigManager>();

        public static ConfigManager Instance { get; } = new();

        /// <summary>全表数据 Type → List&lt;T&gt;</summary>
        private readonly ConcurrentDictionary<Type, object> _lists = new();

        /// <summary>索引字典 Type → Dictionary&lt;int, T&gt;（按主键）</summary>
        private readonly ConcurrentDictionary<Type, object> _dicts = new();

        /// <summary>扫描到的配置元信息 Type → (FileName, KeyProperty)</summary>
        private readonly Dictionary<Type, (string fileName, PropertyInfo keyProp)> _configMeta = new();
        private readonly object _snapshotLock = new();

        private FileSystemWatcher? _watcher;

        public void Init(string directory)
        {
            LoadAll(directory, true);
            Log.Info("配置表模块初始化完成");
        }

        /// <param name="tablesDirectory">CSV 文件目录</param>
        /// <param name="enableHotReload">是否启用文件监听热更新（服务端 true，客户端 false）</param>
        public void LoadAll(string tablesDirectory, bool enableHotReload = false)
        {
            if (!Directory.Exists(tablesDirectory))
            {
                throw new DirectoryNotFoundException($"表目录不存在: {tablesDirectory}");
            }

            ScanConfigs();
            LoadFromDirectory(tablesDirectory);
            if (enableHotReload)
            {
                StartWatch(tablesDirectory);
            }
        }

        /// <summary>
        /// 返回启动前必须取得的全部 CSV 文件名。调用方可以在平台层异步加载文本。
        /// </summary>
        public IReadOnlyList<string> GetRequiredFileNames()
        {
            ScanConfigs();
            List<string> fileNames = _configMeta.Values
                .Select(value => value.fileName)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(fileName => fileName, StringComparer.Ordinal)
                .ToList();
            return fileNames.AsReadOnly();
        }

        /// <summary>
        /// 从调用方已经取得的 CSV 文本原子加载全部配置。缺表或解析失败时保留旧快照。
        /// </summary>
        public void LoadAll(IReadOnlyDictionary<string, string> tableTexts)
        {
            if (tableTexts == null)
            {
                throw new ArgumentNullException(nameof(tableTexts));
            }

            ScanConfigs();
            LoadFromTexts(tableTexts);
            Log.Info("配置表模块异步文本加载完成");
        }

        /// <summary>反射扫描程序集中所有 [ConfigTable] 标注的类</summary>
        private void ScanConfigs()
        {
            _configMeta.Clear();
            Assembly assembly = Assembly.GetExecutingAssembly();
            foreach (Type type in assembly.GetTypes())
            {
                ConfigTableAttribute? attr = type.GetCustomAttribute<ConfigTableAttribute>();
                if (attr == null)
                {
                    continue;
                }

                PropertyInfo? keyProp = type.GetProperty(attr.KeyName);
                if (keyProp == null)
                {
                    throw new InvalidOperationException(
                        $"配置表 {type.Name} 标注 KeyName=\"{attr.KeyName}\"，但未找到该属性");
                }

                if (keyProp.PropertyType != typeof(int))
                {
                    throw new InvalidOperationException(
                        $"配置表 {type.Name} 主键 {attr.KeyName} 必须是 int");
                }

                string attrFileName = attr.FileName;
                if (string.IsNullOrEmpty(attrFileName))
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
            Dictionary<string, string> tableTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string fileName in GetRequiredFileNames())
            {
                string filePath = Path.Combine(tablesDirectory, fileName);
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"表文件不存在: {fileName}", filePath);
                }

                tableTexts.Add(fileName, File.ReadAllText(filePath));
            }

            LoadFromTexts(tableTexts);
        }

        private void LoadFromTexts(IReadOnlyDictionary<string, string> tableTexts)
        {
            Dictionary<Type, object> nextLists = new Dictionary<Type, object>();
            Dictionary<Type, object> nextDicts = new Dictionary<Type, object>();

            foreach (KeyValuePair<Type, (string fileName, PropertyInfo keyProp)> entry in _configMeta)
            {
                Type type = entry.Key;
                string fileName = entry.Value.fileName;
                PropertyInfo keyProp = entry.Value.keyProp;
                if (!tableTexts.TryGetValue(fileName, out string? csvText))
                {
                    throw new FileNotFoundException($"表文本不存在: {fileName}", fileName);
                }

                (System.Collections.IList list, System.Collections.IDictionary dict) =
                    BuildTableSnapshot(type, fileName, keyProp, csvText);

                nextLists.Add(type, list);
                nextDicts.Add(type, dict);
                Log.Info($"加载 {fileName}: {list.Count} 条");
            }

            lock (_snapshotLock)
            {
                _lists.Clear();
                _dicts.Clear();
                foreach (KeyValuePair<Type, object> entry in nextLists)
                {
                    _lists[entry.Key] = entry.Value;
                }

                foreach (KeyValuePair<Type, object> entry in nextDicts)
                {
                    _dicts[entry.Key] = entry.Value;
                }
            }
        }

        private static (System.Collections.IList list, System.Collections.IDictionary dict) BuildTableSnapshot(
            Type type,
            string fileName,
            PropertyInfo keyProp,
            string csvText)
        {
            if (string.IsNullOrWhiteSpace(csvText))
            {
                throw new InvalidDataException($"表文本为空: {fileName}");
            }

            System.Collections.IList list = SimpleCsvReader.LoadText(type, csvText);
            if (list.Count == 0)
            {
                throw new InvalidDataException($"配置表 {fileName} 没有数据行");
            }

            Type dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), type);
            System.Collections.IDictionary dict =
                (System.Collections.IDictionary)Activator.CreateInstance(dictType)!;
            foreach (object item in list)
            {
                int key = (int)keyProp.GetValue(item)!;
                if (dict.Contains(key))
                {
                    throw new InvalidDataException($"配置表 {fileName} 包含重复主键 {key}");
                }

                dict.Add(key, item);
            }

            return (list, dict);
        }

        /// <summary>按主键查单条</summary>
        public static T? Get<T>(int id) where T : class
        {
            lock (Instance._snapshotLock)
            {
                if (!Instance._dicts.TryGetValue(typeof(T), out object? dict))
                {
                    return null;
                }

                return ((Dictionary<int, T>)dict).TryGetValue(id, out T? value) ? value : null;
            }
        }

        /// <summary>获取全表数据</summary>
        public static List<T> GetAll<T>() where T : class
        {
            lock (Instance._snapshotLock)
            {
                return Instance._lists.TryGetValue(typeof(T), out object? data)
                    ? (List<T>)data
                    : new List<T>();
            }
        }

        /// <summary>获取索引字典</summary>
        public static Dictionary<int, T> GetDict<T>() where T : class
        {
            lock (Instance._snapshotLock)
            {
                return Instance._dicts.TryGetValue(typeof(T), out object? dict)
                    ? (Dictionary<int, T>)dict
                    : new Dictionary<int, T>();
            }
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

                var type = _configMeta.FirstOrDefault(kv => kv.Value.fileName == fileName).Key;
                if (type == null)
                    return;

                var now = DateTime.UtcNow;
                if (lastWrite.TryGetValue(fileName, out var last) && (now - last).TotalSeconds < 1)
                    return;
                lastWrite[fileName] = now;

                Log.Info($"检测到文件变化: {fileName}，正在热更新...");
                try
                {
                    (string configuredFileName, PropertyInfo keyProp) = _configMeta[type];
                    string csvText = File.ReadAllText(e.FullPath);
                    (System.Collections.IList list, System.Collections.IDictionary dict) =
                        BuildTableSnapshot(type, configuredFileName, keyProp, csvText);

                    lock (_snapshotLock)
                    {
                        _lists[type] = list;
                        _dicts[type] = dict;
                    }
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
