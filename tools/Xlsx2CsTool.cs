using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace Tools;

/// <summary>
/// 从 xlsx 文件生成 C# 配置类代码。
/// 读取第一行作为列名，第二行推断类型，生成带 [ConfigTable] 的配置类。
/// </summary>
public class Xlsx2CsTool : ITool
{
    public string Name => "xlsx2cs";
    public string Desc => "从 Excel(.xlsx) 生成 C# 配置类代码";

    public Task<int> Run(string[] args)
    {
        string inputDir = "xlsx";
        string? singleFile = null;
        string outputDir = Path.Combine("..", "server", "common", "config");

        var positional = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] is "-f" or "--file" && i + 1 < args.Length)
                singleFile = args[++i];
            else if (args[i] is "-o" or "--output" && i + 1 < args.Length)
                outputDir = args[++i];
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

        Console.WriteLine($"生成 {xlsxFiles.Length} 个配置类:");
        foreach (var filePath in xlsxFiles)
        {
            var fullPath = singleFile != null ? Path.Combine(inputDir, filePath) : filePath;
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"  文件不存在: {fullPath}");
                continue;
            }
            GenerateConfigClass(fullPath, outputDir);
        }

        Console.WriteLine("完成!");
        return Task.FromResult(0);
    }

    static void GenerateConfigClass(string xlsxPath, string outputDir)
    {
        using var wb = new XLWorkbook(xlsxPath);
        var ws = wb.Worksheets.FirstOrDefault();
        if (ws == null)
            return;

        var basename = Path.GetFileNameWithoutExtension(xlsxPath);

        // 读取第一行（列名）
        var headerRow = ws.Row(1);
        var columns = new List<(string name, string type)>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var colName = cell.Value.ToString().Trim();
            if (string.IsNullOrEmpty(colName))
                continue;

            // 推断类型：从第二行数据判断
            var dataCell = ws.Cell(2, cell.Address.ColumnNumber);
            var typeStr = InferType(dataCell.Value.ToString());
            columns.Add((colName, typeStr));
        }

        if (columns.Count == 0)
            return;

        // 生成类名: Item → ItemConfig
        var className = $"{basename}Config";

        // 第一列作为主键
        var keyName = columns[0].name;

        var sb = new StringBuilder();
        sb.AppendLine("namespace server.common.config;");
        sb.AppendLine();
        sb.AppendLine("/// <summary>");
        sb.AppendLine($"/// {basename} 配置。对应 {basename}.csv。");
        sb.AppendLine("/// </summary>");

        // 如果主键不是 Id，指定 KeyName
        if (keyName == "Id")
            sb.AppendLine($"[ConfigTable(\"{basename}.csv\")]");
        else
            sb.AppendLine($"[ConfigTable(\"{basename}.csv\", KeyName = \"{keyName}\")]");

        sb.AppendLine($"public class {className}");
        sb.AppendLine("{");

        for (int i = 0; i < columns.Count; i++)
        {
            var (name, type) = columns[i];

            if (i > 0) sb.AppendLine();

            sb.AppendLine($"    /// <summary>{name}</summary>");
            sb.AppendLine($"    public {type} {name} {{ get; set; }}{GetDefaultValue(type)};");
        }

        sb.AppendLine("}");

        var outputPath = Path.Combine(outputDir, $"{className}.cs");
        File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
        Console.WriteLine($"  {className}.cs ({columns.Count} 字段)");
    }

    /// <summary>根据数据值推断类型</summary>
    static string InferType(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "int";

        // 整数
        if (int.TryParse(value, out _))
            return "int";

        // 浮点数
        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return "float";

        // 分号分隔 → int[]
        if (value.Contains(';') && value.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .All(s => int.TryParse(s.Trim(), out _)))
            return "int[]";

        return "string";
    }

    /// <summary>获取类型默认值后缀</summary>
    static string GetDefaultValue(string type) => type switch
    {
        "int" => "",
        "float" => "",
        "string" => " = \"\"",
        "int[]" => " = []",
        _ => ""
    };
}
