using System.IO;
using Shared.Core.Log;
using UnityEngine;

namespace Client.View
{
    /// <summary>
    /// 客户端日志初始化。配置 UnityAppender + FileAppender。
    /// </summary>
    public static class LogClientInitializer
    {
        public static void Init()
        {
            var logDir = Path.Combine(Application.persistentDataPath, "logs");
            var formatter = new LogFormatter();

            // 先初始化 LogManager（不启用 Console appender，用 UnityAppender 替代）
            var options = new LogOptions
            {
                Level = "Debug",
                Console = new LogConsoleOptions { Enabled = false },
                File = new LogFileOptions
                {
                    Enabled = true,
                    Path = Path.Combine(logDir, "game.log"),
                    ErrorPath = Path.Combine(logDir, "error.log"),
                    MaxHistoryDays = 7,
                }
            };
            LogManager.Init(options);

            // 追加 Unity Appender
            LogManager.AddAppender(new UnityLogAppender(formatter));
        }
    }
}
