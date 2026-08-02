using System;
using System.Collections.Generic;
using System.IO;
using Shared.Common.Config;
using Shared.Core.Config;
using Xunit;

namespace Shared.Tests
{
    public sealed class SimpleCsvReaderTests
    {
        [Fact]
        public void LoadTextParsesSupportedValuesWithInvariantCulture()
        {
            const string Csv = "Id,Name,Ratio,Count,Enabled,Values\n1,alpha,1.25,9000000000,true,1;2;3\n";

            List<FixtureConfig> result = SimpleCsvReader.LoadText<FixtureConfig>(Csv);

            FixtureConfig config = Assert.Single(result);
            Assert.Equal(1, config.Id);
            Assert.Equal("alpha", config.Name);
            Assert.Equal(1.25f, config.Ratio);
            Assert.Equal(9_000_000_000L, config.Count);
            Assert.True(config.Enabled);
            Assert.Equal(new[] { 1, 2, 3 }, config.Values);
        }

        [Fact]
        public void LoadTextRejectsMalformedKnownField()
        {
            const string Csv =
                "Id,Name,Ratio,Count,Enabled,Values\nnot-an-int,alpha,1.0,1,true,1;2\n";

            FormatException exception = Assert.Throws<FormatException>(
                () => SimpleCsvReader.LoadText<FixtureConfig>(Csv));

            Assert.Contains("Id", exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void LoadTextRejectsUnterminatedQuotedField()
        {
            const string Csv =
                "Id,Name,Ratio,Count,Enabled,Values\n1,\"unterminated,1.0,1,true,1;2\n";

            FormatException exception = Assert.Throws<FormatException>(
                () => SimpleCsvReader.LoadText<FixtureConfig>(Csv));

            Assert.Contains("未闭合", exception.InnerException?.Message ?? exception.Message, StringComparison.Ordinal);
        }

        [Fact]
        public void LoadTextRejectsUnknownMissingAndMisalignedColumns()
        {
            const string UnknownHeader =
                "Id,Nmae,Ratio,Count,Enabled,Values\n1,alpha,1.0,1,true,1;2\n";
            const string MissingHeader = "Id,Name,Ratio,Count,Enabled\n1,alpha,1.0,1,true\n";
            const string ShortRow =
                "Id,Name,Ratio,Count,Enabled,Values\n1,alpha,1.0,1,true\n";

            Assert.Throws<FormatException>(() => SimpleCsvReader.LoadText<FixtureConfig>(UnknownHeader));
            Assert.Throws<FormatException>(() => SimpleCsvReader.LoadText<FixtureConfig>(MissingHeader));
            Assert.Throws<FormatException>(() => SimpleCsvReader.LoadText<FixtureConfig>(ShortRow));
        }

        [Fact]
        public void ConfigManagerLoadsCompleteTextSetAtomically()
        {
            string repositoryRoot = FindRepositoryRoot();
            string tablesDirectory = Path.Combine(
                repositoryRoot,
                "client",
                "Assets",
                "StreamingAssets",
                "Tables");
            IReadOnlyList<string> fileNames = ConfigManager.Instance.GetRequiredFileNames();
            Dictionary<string, string> tableTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < fileNames.Count; index++)
            {
                string fileName = fileNames[index];
                tableTexts.Add(fileName, File.ReadAllText(Path.Combine(tablesDirectory, fileName)));
            }

            ConfigManager.Instance.LoadAll(tableTexts);
            int loadedMapCount = ConfigManager.GetAll<MapConfig>().Count;
            Dictionary<string, string> missingTable = new Dictionary<string, string>(
                tableTexts,
                StringComparer.Ordinal);
            missingTable.Remove(fileNames[0]);

            Assert.True(loadedMapCount > 0);
            Assert.Throws<FileNotFoundException>(() => ConfigManager.Instance.LoadAll(missingTable));
            Assert.Equal(loadedMapCount, ConfigManager.GetAll<MapConfig>().Count);
        }

        [Fact]
        public void ConfigManagerRejectsHeaderOnlyTableAndPreservesSnapshot()
        {
            string repositoryRoot = FindRepositoryRoot();
            string tablesDirectory = Path.Combine(
                repositoryRoot,
                "client",
                "Assets",
                "StreamingAssets",
                "Tables");
            IReadOnlyList<string> fileNames = ConfigManager.Instance.GetRequiredFileNames();
            Dictionary<string, string> tableTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < fileNames.Count; index++)
            {
                string fileName = fileNames[index];
                tableTexts.Add(fileName, File.ReadAllText(Path.Combine(tablesDirectory, fileName)));
            }

            ConfigManager.Instance.LoadAll(tableTexts);
            int loadedMapCount = ConfigManager.GetAll<MapConfig>().Count;
            string firstFileName = fileNames[0];
            int newlineIndex = tableTexts[firstFileName].IndexOf('\n');
            tableTexts[firstFileName] = newlineIndex >= 0
                ? tableTexts[firstFileName].Substring(0, newlineIndex + 1)
                : tableTexts[firstFileName];

            Assert.Throws<InvalidDataException>(() => ConfigManager.Instance.LoadAll(tableTexts));
            Assert.Equal(loadedMapCount, ConfigManager.GetAll<MapConfig>().Count);
        }

        [Fact]
        public void ConfigManagerRejectsDuplicateKeyAndPreservesSnapshot()
        {
            string repositoryRoot = FindRepositoryRoot();
            string tablesDirectory = Path.Combine(
                repositoryRoot,
                "client",
                "Assets",
                "StreamingAssets",
                "Tables");
            IReadOnlyList<string> fileNames = ConfigManager.Instance.GetRequiredFileNames();
            Dictionary<string, string> tableTexts = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < fileNames.Count; index++)
            {
                string fileName = fileNames[index];
                tableTexts.Add(fileName, File.ReadAllText(Path.Combine(tablesDirectory, fileName)));
            }

            ConfigManager.Instance.LoadAll(tableTexts);
            int loadedMapCount = ConfigManager.GetAll<MapConfig>().Count;
            string firstFileName = fileNames[0];
            string normalized = tableTexts[firstFileName].Replace("\r\n", "\n").Replace('\r', '\n');
            string[] lines = normalized.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(lines.Length > 1);
            tableTexts[firstFileName] = $"{normalized.TrimEnd()}\n{lines[1]}\n";

            Assert.Throws<InvalidDataException>(() => ConfigManager.Instance.LoadAll(tableTexts));
            Assert.Equal(loadedMapCount, ConfigManager.GetAll<MapConfig>().Count);
        }

        private static string FindRepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "global.json")))
            {
                directory = directory.Parent;
            }

            if (directory == null)
            {
                throw new DirectoryNotFoundException("无法从测试输出目录定位仓库根");
            }

            return directory.FullName;
        }

        private sealed class FixtureConfig
        {
            public int Id { get; set; }

            public string Name { get; set; } = string.Empty;

            public float Ratio { get; set; }

            public long Count { get; set; }

            public bool Enabled { get; set; }

            public int[] Values { get; set; } = Array.Empty<int>();
        }
    }
}
