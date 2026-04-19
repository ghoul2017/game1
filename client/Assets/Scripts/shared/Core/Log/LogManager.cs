using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Shared.Core.Log
{
    public static class LogManager
    {
        private static readonly ConcurrentDictionary<string, ILog> _loggers = new();
        private static readonly List<ILogAppender> _appenders = new();
        private static readonly ILogFormatter _defaultFormatter = new LogFormatter();

        private static LogLevel _rootLevel = LogLevel.Info;
        private static Dictionary<string, LogLevel> _loggerLevels = new();
        private static bool _initialized;

        public static void Init(LogOptions options)
        {
            if (_initialized) return;

            _rootLevel = ParseLevel(options.Level, LogLevel.Info);
            _loggerLevels = options.Loggers?.ToDictionary(
                kvp => kvp.Key,
                kvp => ParseLevel(kvp.Value, _rootLevel)
            ) ?? new Dictionary<string, LogLevel>();

            var formatter = !string.IsNullOrEmpty(options.Format)
                ? new LogFormatter(options.Format)
                : _defaultFormatter;

            // Console appender
            if (options.Console?.Enabled != false)
            {
                _appenders.Add(new ConsoleLogAppender(formatter, LogLevel.Debug, options.Console?.Colorful ?? true));
            }

            // File appender
            if (options.File?.Enabled != false)
            {
                var filePath = options.File?.Path ?? "log/game.log";
                var errorPath = options.File?.ErrorPath ?? "log/error.log";
                var maxHistory = options.File?.MaxHistoryDays ?? 7;
                _appenders.Add(new FileLogAppender(formatter, filePath, errorPath, LogLevel.Debug, maxHistory));
            }

            _initialized = true;
        }

        public static ILog GetLogger<T>() => GetLogger(typeof(T).Name);

        public static ILog GetLogger(string name)
        {
            return _loggers.GetOrAdd(name, CreateLogger);
        }

        public static void Shutdown()
        {
            foreach (var appender in _appenders)
            {
                try
                {
                    appender.Flush();
                    appender.Close();
                }
                catch
                {
                    /* 关闭异常忽略 */
                }
            }

            _appenders.Clear();
            _loggers.Clear();
            _initialized = false;
        }

        private static ILog CreateLogger(string name)
        {
            // 未初始化时提供空实现，避免启动前报错
            if (!_initialized)
            {
                return new ConsoleFallbackLogger(name);
            }

            var level = ResolveLevel(name);
            return new Logger(name, level, _appenders);
        }

        private static LogLevel ResolveLevel(string name)
        {
            // 精确匹配 > 前缀匹配（如 "Server.Network.TcpServer" 匹配 "Server.Network"）
            if (_loggerLevels.TryGetValue(name, out var exact))
                return exact;

            var bestMatch = "";
            var bestLevel = _rootLevel;

            foreach (var kvp in _loggerLevels)
            {
                if (name.StartsWith(kvp.Key) && kvp.Key.Length > bestMatch.Length)
                {
                    bestMatch = kvp.Key;
                    bestLevel = kvp.Value;
                }
            }

            return bestLevel;
        }

        private static LogLevel ParseLevel(string? value, LogLevel defaultLevel)
        {
            return Enum.TryParse<LogLevel>(value, ignoreCase: true, out var level) ? level : defaultLevel;
        }

        /// <summary>
        /// 启动前的兜底 logger，直接输出到控制台
        /// </summary>
        private class ConsoleFallbackLogger : ILog
        {
            private string _name;

            public ConsoleFallbackLogger(string name)
            {
                this._name = name;
            }

            public void Debug(string message) => Console.WriteLine($"[Debug][{_name}] {message}");
            public void Info(string message) => Console.WriteLine($"[Info][{_name}] {message}");
            public void Warn(string message) => Console.WriteLine($"[Warn][{_name}] {message}");
            public void Error(string message) => Console.WriteLine($"[Error][{_name}] {message}");
            public void Error(string message, Exception ex) => Console.WriteLine($"[Error][{_name}] {message}\n{ex}");
        }
    }

    public class LogOptions
    {
        public string Level { get; set; } = "Info";
        public string? Format { get; set; }
        public Dictionary<string, string>? Loggers { get; set; }
        public LogConsoleOptions? Console { get; set; }
        public LogFileOptions? File { get; set; }
    }


    public class LogConsoleOptions
    {
        public bool Enabled { get; set; } = true;
        public bool Colorful { get; set; } = true;
    }

    public class LogFileOptions
    {
        public bool Enabled { get; set; } = true;
        public string Path { get; set; } = "log/game.log";
        public string ErrorPath { get; set; } = "log/error.log";
        public int MaxHistoryDays { get; set; } = 7;
    }
}