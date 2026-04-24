using System.Text;

#nullable enable
namespace Shared.Core.Log
{
    public class LogFormatter : ILogFormatter
    {
        private readonly string _pattern;

        /// <param name="pattern">
        /// 支持占位符: {time} {level} {level,-5} {thread} {name} {message} {newline} {tab}
        /// 默认: "[{time} {level,-5}][{thread}]{name} {message}"
        /// </param>
        public LogFormatter(string? pattern = null)
        {
            _pattern = pattern ?? "[{time} {level,-5}][{thread}]{name} {message}";
        }

        public string Format(LogEntry entry)
        {
            var sb = new StringBuilder(_pattern.Length + 64);
            int i = 0;

            while (i < _pattern.Length)
            {
                if (_pattern[i] == '{')
                {
                    int end = _pattern.IndexOf('}', i);
                    if (end > i)
                    {
                        string token = _pattern.Substring(i + 1, end - i - 1);
                        AppendToken(sb, token, entry);
                        i = end + 1;
                        continue;
                    }
                }

                sb.Append(_pattern[i]);
                i++;
            }

            // 异常堆栈
            if (entry.Exception != null)
            {
                sb.AppendLine();
                sb.Append(entry.Exception.ToString());
            }

            return sb.ToString();
        }

        private static void AppendToken(StringBuilder sb, string token, LogEntry entry)
        {
            switch (token)
            {
                case "time":
                    sb.Append(entry.Time.ToString("yyyy-MM-dd HH:mm:ss.fff"));
                    break;
                case "level":
                    sb.Append(entry.Level.ToString());
                    break;
                case "level,-5":
                    sb.Append(entry.Level.ToString().PadRight(5));
                    break;
                case "thread":
                    sb.Append(entry.ThreadId);
                    break;
                case "name":
                    sb.Append(entry.LoggerName);
                    break;
                case "message":
                    sb.Append(entry.Message);
                    break;
                case "newline":
                    sb.AppendLine();
                    break;
                case "tab":
                    sb.Append('\t');
                    break;
                default:
                    sb.Append('{').Append(token).Append('}');
                    break;
            }
        }
    }
}