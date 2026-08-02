#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;

namespace Shared.Core.Config
{
    /// <summary>
    /// 轻量 CSV 读取器，兼容 .NET 8 和 Unity 2021.3（.NET Framework 4.7.1）。
    /// 支持 int/string/float/long/double/bool 及 int[]（分号分隔）类型。
    /// </summary>
    public static class SimpleCsvReader
    {
        /// <summary>
        /// 泛型版本，供外部直接调用。
        /// </summary>
        public static List<T> Load<T>(string filePath) where T : class, new()
        {
            return (List<T>)Load(typeof(T), filePath);
        }

        /// <summary>
        /// 从已经异步取得的 CSV 文本解析配置，供不具备本地文件系统语义的平台使用。
        /// </summary>
        public static List<T> LoadText<T>(string csvText) where T : class, new()
        {
            return (List<T>)LoadText(typeof(T), csvText);
        }

        /// <summary>
        /// 非泛型版本，供 ConfigManager 反射调用。
        /// </summary>
        public static IList Load(Type type, string filePath)
        {
            if (filePath == null)
            {
                throw new ArgumentNullException(nameof(filePath));
            }

            string csvText = File.ReadAllText(filePath);
            return LoadText(type, csvText);
        }

        /// <summary>
        /// 只负责解析调用方提供的 CSV 文本，不读取文件或平台资源。
        /// </summary>
        public static IList LoadText(Type type, string csvText)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (csvText == null)
            {
                throw new ArgumentNullException(nameof(csvText));
            }

            Type listType = typeof(List<>).MakeGenericType(type);
            IList result = (IList)Activator.CreateInstance(listType)!;
            string normalizedText = csvText.Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalizedText.Split('\n');
            if (lines.Length == 0)
            {
                return result;
            }

            List<string> headers = ParseLine(lines[0].TrimStart('\uFEFF'));
            if (headers.Count == 0 || headers.TrueForAll(string.IsNullOrWhiteSpace))
            {
                throw new FormatException($"配置 {type.Name} 缺少表头");
            }

            Dictionary<string, PropertyInfo> propMap = new Dictionary<string, PropertyInfo>();
            HashSet<string> expectedProperties = new HashSet<string>(StringComparer.Ordinal);
            PropertyInfo[] properties = type.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            for (int propertyIndex = 0; propertyIndex < properties.Length; propertyIndex++)
            {
                PropertyInfo property = properties[propertyIndex];
                if (property.CanWrite)
                {
                    expectedProperties.Add(property.Name);
                }
            }

            for (int h = 0; h < headers.Count; h++)
            {
                string header = headers[h].Trim();
                if (string.IsNullOrEmpty(header))
                {
                    throw new FormatException($"配置 {type.Name} 表头第 {h + 1} 列为空");
                }

                PropertyInfo? prop = type.GetProperty(header);
                if (prop == null || !prop.CanWrite)
                {
                    throw new FormatException($"配置 {type.Name} 包含未知字段 {header}");
                }

                if (!propMap.TryAdd(header, prop))
                {
                    throw new FormatException($"配置 {type.Name} 包含重复字段 {header}");
                }
            }

            if (propMap.Count != expectedProperties.Count)
            {
                List<string> missingProperties = new List<string>();
                foreach (string propertyName in expectedProperties)
                {
                    if (!propMap.ContainsKey(propertyName))
                    {
                        missingProperties.Add(propertyName);
                    }
                }

                missingProperties.Sort(StringComparer.Ordinal);
                throw new FormatException(
                    $"配置 {type.Name} 缺少字段: {string.Join(",", missingProperties)}");
            }

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line))
                {
                    continue;
                }

                List<string> values = ParseLine(line);
                if (values.Count != headers.Count)
                {
                    throw new FormatException(
                        $"配置 {type.Name} 第 {i + 1} 行列数为 {values.Count}，预期 {headers.Count}");
                }

                object item = Activator.CreateInstance(type)!;

                for (int j = 0; j < headers.Count; j++)
                {
                    string header = headers[j].Trim();
                    PropertyInfo prop = propMap[header];
                    string val = values[j].Trim();
                    if (string.IsNullOrEmpty(val))
                    {
                        continue;
                    }

                    try
                    {
                        SetProperty(prop, item, val);
                    }
                    catch (Exception exception)
                    {
                        throw new FormatException(
                            $"配置 {type.Name} 第 {i + 1} 行字段 {header} 的值“{val}”无效",
                            exception);
                    }
                }

                result.Add(item);
            }

            return result;
        }

        private static void SetProperty(PropertyInfo prop, object target, string val)
        {
            Type type = prop.PropertyType;
            if (type == typeof(int))
            {
                prop.SetValue(target, int.Parse(val, NumberStyles.Integer, CultureInfo.InvariantCulture));
            }
            else if (type == typeof(float))
            {
                prop.SetValue(target, float.Parse(val, NumberStyles.Float, CultureInfo.InvariantCulture));
            }
            else if (type == typeof(long))
            {
                prop.SetValue(target, long.Parse(val, NumberStyles.Integer, CultureInfo.InvariantCulture));
            }
            else if (type == typeof(double))
            {
                prop.SetValue(target, double.Parse(val, NumberStyles.Float, CultureInfo.InvariantCulture));
            }
            else if (type == typeof(string))
            {
                prop.SetValue(target, val);
            }
            else if (type == typeof(bool))
            {
                prop.SetValue(target, bool.Parse(val));
            }
            else if (type == typeof(int[]))
            {
                prop.SetValue(target, ParseIntArray(val));
            }
            else
            {
                throw new NotSupportedException($"不支持的配置字段类型: {type.FullName}");
            }
        }

        /// <summary>
        /// 解析分号分隔的整数数组，如 "1;2;3"。
        /// </summary>
        private static int[] ParseIntArray(string val)
        {
            if (string.IsNullOrEmpty(val))
            {
                return new int[0];
            }

            string[] parts = val.Split(';');
            List<int> list = new List<int>();
            foreach (string part in parts)
            {
                string trimmed = part.Trim();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    list.Add(int.Parse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture));
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// 简单 CSV 行解析，处理双引号包裹的字段。
        /// </summary>
        private static List<string> ParseLine(string line)
        {
            List<string> result = new List<string>();
            string current = "";
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

            if (inQuotes)
            {
                throw new FormatException("配置 CSV 包含未闭合的引号");
            }

            result.Add(current);
            return result;
        }
    }
}
