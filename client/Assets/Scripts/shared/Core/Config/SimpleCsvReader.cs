namespace Shared.Core.Config
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;

    /// <summary>
    /// 轻量 CSV 读取器，兼容 .NET 8 和 Unity 2021.3（.NET Framework 4.7.1）。
    /// 支持 int/string/float/long/double/bool 及 int[]（分号分隔）类型。
    /// </summary>
    public static class SimpleCsvReader
    {
        /// <summary>
        /// 从 CSV 文件加载配置列表。首行为表头，后续为数据行。
        /// </summary>
        public static List<T> Load<T>(string filePath) where T : class, new()
        {
            var result = new List<T>();
            var lines = File.ReadAllLines(filePath);
            if (lines.Length == 0) return result;

            var headers = ParseLine(lines[0]);
            var propMap = new Dictionary<string, PropertyInfo>();
            var type = typeof(T);

            for (int h = 0; h < headers.Count; h++)
            {
                var header = headers[h].Trim();
                if (string.IsNullOrEmpty(header)) continue;
                var prop = type.GetProperty(header);
                if (prop != null && !propMap.ContainsKey(header))
                {
                    propMap[header] = prop;
                }
            }

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var values = ParseLine(line);
                var item = new T();

                for (int j = 0; j < headers.Count && j < values.Count; j++)
                {
                    var header = headers[j].Trim();
                    if (!propMap.ContainsKey(header)) continue;

                    var prop = propMap[header];
                    var val = values[j].Trim();
                    if (string.IsNullOrEmpty(val)) continue;

                    try
                    {
                        SetProperty(prop, item, val);
                    }
                    catch
                    {
                        // 忽略解析错误，保留默认值
                    }
                }
                result.Add(item);
            }
            return result;
        }

        private static void SetProperty(PropertyInfo prop, object target, string val)
        {
            var type = prop.PropertyType;
            if (type == typeof(int))
                prop.SetValue(target, int.Parse(val));
            else if (type == typeof(float))
                prop.SetValue(target, float.Parse(val));
            else if (type == typeof(long))
                prop.SetValue(target, long.Parse(val));
            else if (type == typeof(double))
                prop.SetValue(target, double.Parse(val));
            else if (type == typeof(string))
                prop.SetValue(target, val);
            else if (type == typeof(bool))
                prop.SetValue(target, bool.Parse(val));
            else if (type == typeof(int[]))
                prop.SetValue(target, ParseIntArray(val));
        }

        /// <summary>
        /// 解析分号分隔的整数数组，如 "1;2;3"。
        /// </summary>
        private static int[] ParseIntArray(string val)
        {
            if (string.IsNullOrEmpty(val)) return new int[0];
            var parts = val.Split(';');
            var list = new List<int>();
            foreach (var p in parts)
            {
                var trimmed = p.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                    list.Add(int.Parse(trimmed));
            }
            return list.ToArray();
        }

        /// <summary>
        /// 简单 CSV 行解析，处理双引号包裹的字段。
        /// </summary>
        private static List<string> ParseLine(string line)
        {
            var result = new List<string>();
            var current = "";
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < line.Length && line[i + 1] == '"')
                        {
                            current += '"';
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        current += c;
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        result.Add(current);
                        current = "";
                    }
                    else
                    {
                        current += c;
                    }
                }
            }

            result.Add(current);
            return result;
        }
    }
}
