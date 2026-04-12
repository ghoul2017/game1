namespace Server.Core.Log;

public interface ILogFormatter
{
    string Format(LogEntry entry);
}
