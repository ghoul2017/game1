using System;
using System.IO;
using System.Text;

#nullable enable
namespace Shared.Core.Log
{
    public class FileLogAppender : ILogAppender
    {
        private readonly ILogFormatter _formatter;
        private readonly string _filePath;
        private readonly string _errorFilePath;
        private readonly int _maxHistoryDays;

        private StreamWriter? _writer;
        private StreamWriter? _errorWriter;
        private string _currentDate = "";
        private readonly object _lock = new();

        public LogLevel Level { get; }

        public FileLogAppender(ILogFormatter formatter, string filePath, string errorFilePath,
            LogLevel level = LogLevel.Debug, int maxHistoryDays = 7)
        {
            _formatter = formatter;
            _filePath = filePath;
            _errorFilePath = errorFilePath;
            _maxHistoryDays = maxHistoryDays;
            Level = level;

            EnsureDirectory(filePath);
            EnsureDirectory(errorFilePath);
        }

        public void Append(LogEntry entry)
        {
            if (entry.Level < Level) return;

            var text = _formatter.Format(entry);
            var date = entry.Time.ToString("yyyyMMdd");

            lock (_lock)
            {
                // 按日期滚动：日期变化时切换文件
                if (date != _currentDate)
                {
                    _currentDate = date;
                    CloseWriters();

                    var path = ReplaceDate(_filePath, entry.Time);
                    _writer = new StreamWriter(
                        new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite), Encoding.UTF8);

                    if (entry.Level >= LogLevel.Error)
                    {
                        var errPath = ReplaceDate(_errorFilePath, entry.Time);
                        _errorWriter =
                            new StreamWriter(
                                new FileStream(errPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                                Encoding.UTF8);
                    }

                    CleanOldFiles();
                }
                else if (entry.Level >= LogLevel.Error && _errorWriter == null)
                {
                    var errPath = ReplaceDate(_errorFilePath, entry.Time);
                    _errorWriter =
                        new StreamWriter(
                            new FileStream(errPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                            Encoding.UTF8);
                }

                _writer?.WriteLine(text);

                if (entry.Level >= LogLevel.Error)
                {
                    _errorWriter?.WriteLine(text);
                }
            }
        }

        public void Flush()
        {
            lock (_lock)
            {
                _writer?.Flush();
                _errorWriter?.Flush();
            }
        }

        public void Close()
        {
            lock (_lock)
            {
                CloseWriters();
            }
        }

        private void CloseWriters()
        {
            _writer?.Dispose();
            _writer = null;
            _errorWriter?.Dispose();
            _errorWriter = null;
        }

        private static string ReplaceDate(string path, DateTime time)
        {
            var dir = Path.GetDirectoryName(path) ?? ".";
            var ext = Path.GetExtension(path);
            var name = Path.GetFileNameWithoutExtension(path);
            return Path.Combine(dir, $"{name}-{time:yyyyMMdd}{ext}");
        }

        private static void EnsureDirectory(string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }

        private void CleanOldFiles()
        {
            var cutoff = DateTime.Now.AddDays(-_maxHistoryDays).ToString("yyyyMMdd");

            CleanDir(_filePath, cutoff);
            CleanDir(_errorFilePath, cutoff);
        }

        private static void CleanDir(string filePath, string cutoff)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (dir == null) return;

            var name = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);

            try
            {
                foreach (var f in Directory.EnumerateFiles(dir, $"{name}-*{ext}"))
                {
                    var fileName = Path.GetFileNameWithoutExtension(f);
                    var datePart = fileName.Substring(name.Length + 1); // name-yyyyMMdd
                    if (datePart.CompareTo(cutoff) < 0)
                    {
                        File.Delete(f);
                    }
                }
            }
            catch
            {
                // 清理失败不影响运行
            }
        }
    }
}