using System.Text;
using ClosedXML.Excel;

namespace Tools;

public class Xlsx2CsvTool : ITool
{
    public string Name => "xlsx2csv";
    public string Desc => "Excel(.xlsx) 转 CSV 配置表";

    public Task<int> Run(string[] args)
    {
        // 解析参数: xlsx2csv [inputDir] [outputDir] [-f filename]
        string inputDir = "xlsx";
        string? singleFile = null;
        string outputDir = Path.Combine("..", "server", "Data", "Tables");

        var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "-f" or "--file" && i + 1 < args.Length)
                singleFile = args[++i];
            else
                positional.Add(args[i]);
        }

        if (positional.Count > 0) inputDir = positional[0];
        if (positional.Count > 1) outputDir = positional[1];

        if (!Directory.Exists(inputDir))
        {
            Console.WriteLine($"xlsx 目录不存在: {Path.GetFullPath(inputDir)}");
            return Task.FromResult(1);
        }

        Directory.CreateDirectory(outputDir);

        // 收集要处理的文件
        var xlsxFiles = singleFile != null
            ? new[] { singleFile }
            : Directory.GetFiles(inputDir, "*.xlsx")
                .Where(f => !Path.GetFileName(f).StartsWith("~"))
                .OrderBy(f => f)
                .ToArray();

        if (xlsxFiles.Length == 0)
        {
            Console.WriteLine($"在 {Path.GetFullPath(inputDir)} 中未找到 .xlsx 文件");
            return Task.FromResult(0);
        }

        Console.WriteLine($"转换 {xlsxFiles.Length} 个文件:");
        foreach (var filePath in xlsxFiles)
        {
            var fullPath = singleFile != null ? Path.Combine(inputDir, filePath) : filePath;
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"  文件不存在: {fullPath}");
                continue;
            }
            ConvertFile(fullPath, outputDir);
        }

        Console.WriteLine("完成!");
        return Task.FromResult(0);
    }

    static void ConvertFile(string xlsxPath, string csvDir)
    {
        using var wb = new XLWorkbook(xlsxPath);
        var basename = Path.GetFileNameWithoutExtension(xlsxPath);

        foreach (var ws in wb.Worksheets)
        {
            var rows = new List<List<string>>();
            foreach (var row in ws.RangeUsed()?.Rows() ?? Enumerable.Empty<IXLRangeRow>())
            {
                var cells = row.Cells().Select(c => c.Value.ToString()).ToList();
                // 跳过以 # 开头的注释行
                if (cells.Count > 0 && cells[0].TrimStart().StartsWith("#"))
                    continue;
                rows.Add(cells);
            }

            if (rows.Count == 0)
                continue;

            // CSV 文件名: 仅一个 Sheet 或默认 Sheet 时用 xlsx 文件名
            var csvName = wb.Worksheets.Count == 1 || ws.Name == "Sheet"
                ? $"{basename}.csv"
                : $"{basename}_{ws.Name}.csv";
            var csvPath = Path.Combine(csvDir, csvName);

            var sb = new StringBuilder();
            foreach (var row in rows)
                sb.AppendLine(string.Join(",", row.Select(EscapeCsvField)));

            File.WriteAllText(csvPath, sb.ToString(), new UTF8Encoding(true));
            Console.WriteLine($"  {csvName} ({rows.Count} 行)");
        }
    }

    /// <summary>
    /// CSV 字段转义：含逗号、引号、换行时用双引号包裹
    /// </summary>
    static string EscapeCsvField(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
