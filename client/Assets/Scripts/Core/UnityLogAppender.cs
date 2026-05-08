using Shared.Core.Log;
using UnityEngine;

namespace Client.Core
{
    /// <summary>
    /// 将 ILog 日志转发到 UnityEngine.Debug。
    /// 放在客户端目录，不污染 shared 代码。
    /// </summary>
    public class UnityLogAppender : ILogAppender
    {
        private readonly ILogFormatter _formatter;

        public LogLevel Level { get; }

        public UnityLogAppender(ILogFormatter formatter, LogLevel level = LogLevel.Debug)
        {
            _formatter = formatter;
            Level = level;
        }

        public void Append(LogEntry entry)
        {
            if (entry.Level < Level) return;

            var text = _formatter.Format(entry);
            switch (entry.Level)
            {
                case LogLevel.Debug:
                case LogLevel.Info:
                    Debug.Log(text);
                    break;
                case LogLevel.Warn:
                    Debug.LogWarning(text);
                    break;
                case LogLevel.Error:
                case LogLevel.Fatal:
                    if (entry.Exception != null)
                        Debug.LogError(text + "\n" + entry.Exception);
                    else
                        Debug.LogError(text);
                    break;
            }
        }

        public void Flush()
        {
        }

        public void Close()
        {
        }
    }
}
