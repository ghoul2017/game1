namespace Server.Core.Log;

public struct LogEntry
{
    public LogLevel Level;
    public DateTime Time;
    public string LoggerName;
    public string Message;
    public Exception? Exception;
    public int ThreadId;
}

public interface ILogAppender
{
    LogLevel Level { get; }
    void Append(LogEntry entry);
    void Flush();
    void Close();
}
