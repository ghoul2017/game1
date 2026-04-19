using System;
using System.Collections.Generic;

namespace Server.Config;

public interface IConfigManager
{
    /// <summary>加载指定目录下所有 CSV 配置表（自动扫描 [ConfigTable] 标注的类）</summary>
    void LoadAll(string tablesDirectory);

    /// <summary>按主键查单条配置</summary>
    T? Get<T>(int id) where T : class;

    /// <summary>获取全表数据</summary>
    List<T> GetAll<T>() where T : class;

    /// <summary>获取索引字典（主键 → 配置对象）</summary>
    Dictionary<int, T> GetDict<T>() where T : class;

    /// <summary>热更新通知事件</summary>
    event Action<Type>? OnTableReloaded;
}
