using System;

namespace Server.Core.Log;

public class ConsoleLogAppender : ILogAppender
{
    private readonly ILogFormatter _formatter;
    private readonly bool _colorful;

    public LogLevel Level { get; }

    public ConsoleLogAppender(ILogFormatter formatter, LogLevel level = LogLevel.Debug, bool colorful = true)
    {
        _formatter = formatter;
        _colorful = colorful;
        Level = level;
    }

    public void Append(LogEntry entry)
    {
        if (entry.Level < Level) return;

        var text = _formatter.Format(entry);

        if (_colorful)
        {
            var color = entry.Level switch
            {
                LogLevel.Debug => ConsoleColor.DarkGray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warn => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };

            var original = System.Console.ForegroundColor;
            System.Console.ForegroundColor = color;
            System.Console.WriteLine(text);
            System.Console.ForegroundColor = original;
        }
        else
        {
            System.Console.WriteLine(text);
        }
    }

    public void Flush() { }
    public void Close() { }
}
