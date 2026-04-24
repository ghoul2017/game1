using System;
using System.Collections.Generic;
using System.Threading;

#nullable enable
namespace Shared.Core.Log
{
    internal class Logger : ILog
    {
        private readonly string _name;
        private readonly LogLevel _level;
        private readonly List<ILogAppender> _appenders;

        public Logger(string name, LogLevel level, List<ILogAppender> appenders)
        {
            _name = name;
            _level = level;
            _appenders = appenders;
        }

        public void Debug(string message) => Write(LogLevel.Debug, message, null);
        public void Info(string message) => Write(LogLevel.Info, message, null);
        public void Warn(string message) => Write(LogLevel.Warn, message, null);
        public void Error(string message) => Write(LogLevel.Error, message, null);
        public void Error(string message, Exception ex) => Write(LogLevel.Error, message, ex);

        private void Write(LogLevel level, string message, Exception? ex)
        {
            if (level < _level) return;

            var entry = new LogEntry
            {
                Level = level,
                Time = DateTime.Now,
                LoggerName = _name,
                Message = message,
                Exception = ex,
                ThreadId = Thread.CurrentThread.ManagedThreadId,
            };

            foreach (var appender in _appenders)
            {
                try
                {
                    appender.Append(entry);
                }
                catch
                {
                    /* appender 异常不能影响业务 */
                }
            }
        }
    }
}