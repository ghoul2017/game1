namespace Server.Core.Log;

/// <summary>
/// 日志接口 — 仿 Unity Debug 风格，共享模块可使用此接口打日志。
/// 服务端由 Logger 实现，Unity 端可简单转发到 UnityEngine.Debug.Log。
/// </summary>
public interface ILog
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Error(string message, Exception ex);
}
