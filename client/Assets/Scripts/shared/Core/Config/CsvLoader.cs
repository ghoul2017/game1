using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.TypeConversion;

namespace Game.Config
{
    /// <summary>
    /// int[] 类型转换器。CSV 中用分号分隔，如 "1;2;3"。
    /// </summary>
    public class IntArrayConverter : ITypeConverter
    {
        public object ConvertFromString(string? text, IReaderRow row, MemberMapData memberMapData)
        {
            if (string.IsNullOrWhiteSpace(text)) return Array.Empty<int>();
#if UNITY_2021
            return text.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(int.Parse)
                .ToArray();
#else
            return text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(int.Parse)
                .ToArray();
#endif
        }

        public string? ConvertToString(object? value, IWriterRow row, MemberMapData memberMapData)
        {
            if (value is int[] arr) return string.Join(';', arr);
            return "";
        }
    }

    public static class CsvLoader
    {
        public static List<T> Load<T>(string filePath) where T : class
        {
            using var reader = new StreamReader(filePath);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
            };
            using var csv = new CsvReader(reader, config);

            // 注册自定义类型转换器
            csv.Context.TypeConverterCache.AddConverter<int[]>(new IntArrayConverter());

            return csv.GetRecords<T>().ToList();
        }
    }
}