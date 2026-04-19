using Shared.Core.Config;

namespace Client.Config
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Shared.Common.Config;

    /// <summary>
    /// 客户端配置表管理器，轻量版（无热更新）。
    /// 自动扫描 [ConfigTable] 标注的类，使用 SimpleCsvReader 加载 CSV。
    /// </summary>
    public class ClientConfigManager
    {
        private static readonly ClientConfigManager _instance = new ClientConfigManager();
        public static ClientConfigManager Instance { get { return _instance; } }

        private readonly Dictionary<Type, object> _lists = new Dictionary<Type, object>();
        private readonly Dictionary<Type, object> _dicts = new Dictionary<Type, object>();
        private readonly Dictionary<Type, KeyValuePair<string, PropertyInfo>> _configMeta =
            new Dictionary<Type, KeyValuePair<string, PropertyInfo>>();

        private ClientConfigManager() { }

        /// <summary>
        /// 扫描配置表类并从指定目录加载所有 CSV 文件。
        /// </summary>
        public void LoadAll(string tablesDirectory)
        {
            ScanConfigs();
            LoadFromDirectory(tablesDirectory);
        }

        private void ScanConfigs()
        {
            _configMeta.Clear();
            var assembly = Assembly.GetExecutingAssembly();
            foreach (var type in assembly.GetTypes())
            {
                var attr = type.GetCustomAttribute<ConfigTableAttribute>();
                if (attr == null) continue;

                var keyProp = type.GetProperty(attr.KeyName);
                if (keyProp == null)
                {
                    UnityEngine.Debug.LogError(
                        "配置表 " + type.Name + " 标注 KeyName=\"" + attr.KeyName + "\"，但未找到该属性");
                    continue;
                }

                _configMeta[type] = new KeyValuePair<string, PropertyInfo>(attr.FileName, keyProp);
                UnityEngine.Debug.Log("扫描到配置表: " + type.Name + " -> " + attr.FileName);
            }
        }

        private void LoadFromDirectory(string tablesDirectory)
        {
            foreach (var kv in _configMeta)
            {
                var type = kv.Key;
                var fileName = kv.Value.Key;
                var keyProp = kv.Value.Value;
                var filePath = Path.Combine(tablesDirectory, fileName);

                if (!File.Exists(filePath))
                {
                    UnityEngine.Debug.LogWarning("配置表文件不存在: " + fileName);
                    continue;
                }

                try
                {
                    var method = typeof(SimpleCsvReader).GetMethod("Load");
                    var genericMethod = method.MakeGenericMethod(type);
                    var data = genericMethod.Invoke(null, new object[] { filePath }) as IList;
                    if (data == null) continue;

                    var dictType = typeof(Dictionary<,>).MakeGenericType(typeof(int), type);
                    var dict = (IDictionary)Activator.CreateInstance(dictType);
                    foreach (var item in data)
                    {
                        var key = (int)keyProp.GetValue(item);
                        dict[key] = item;
                    }

                    _lists[type] = data;
                    _dicts[type] = dict;
                    UnityEngine.Debug.Log("加载配置表 " + fileName + ": " + data.Count + " 条");
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError("加载配置表失败: " + fileName + "\n" + ex);
                }
            }
        }

        /// <summary>
        /// 按主键查单条。
        /// </summary>
        public T Get<T>(int id) where T : class
        {
            if (!_dicts.TryGetValue(typeof(T), out var dict)) return null;
            var d = (Dictionary<int, T>)dict;
            return d.TryGetValue(id, out var val) ? val : null;
        }

        /// <summary>
        /// 获取全表数据。
        /// </summary>
        public List<T> GetAll<T>() where T : class
        {
            if (_lists.TryGetValue(typeof(T), out var data)) return (List<T>)data;
            return new List<T>();
        }

        /// <summary>
        /// 获取索引字典。
        /// </summary>
        public Dictionary<int, T> GetDict<T>() where T : class
        {
            if (_dicts.TryGetValue(typeof(T), out var dict)) return (Dictionary<int, T>)dict;
            return new Dictionary<int, T>();
        }
    }
}
